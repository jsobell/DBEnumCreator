using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

const string defaultSettingsFile = "enumsettings.json";

var filename = args.Length == 1 ? args[0] : defaultSettingsFile;

if (filename == "--sample")
{
    Console.WriteLine(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samplesettings.json")));
    return 99;
}

if (!File.Exists(filename) || args.Length > 1)
{
    Console.Error.WriteLine("Syntax:\nDBEnumCreator [enumsettings.json]"
                            + "\n This will read the settings from the specified json file and generate the associated enum. 'enumsettings.json' will be used if no settings file is specified\n"
                            + "\nDBEnumCreator --sample\n Displays a sample configuration file");
}

if (!File.Exists(filename))
{
    Console.Error.WriteLine($"Settings file not found at {Path.GetFullPath(filename)}");
    return 1;
}

var settings = JsonSerializer.Deserialize<EnumSettings>(File.ReadAllText(filename));
if (settings == null)
{
    Console.Error.WriteLine($"Unable to deserialise settings file {Path.GetFullPath(filename)}");
    return 2;
}

var sb = new StringBuilder();
sb.AppendLine("using System.ComponentModel;");

if (!string.IsNullOrWhiteSpace(settings.Namespace))
    sb.AppendLine($"namespace {settings.Namespace} " + "{");

foreach (var table in settings.Tables)
{
    var selectString = $"SELECT * FROM {table.TableName} order by {table.ValueField}";
    using SqlConnection connection = new SqlConnection(settings.ConnectionString);
    var command = new SqlCommand(selectString, connection);
    connection.Open();
    using SqlDataReader reader = command.ExecuteReader();
    if (table.IsFlags)
        sb.AppendLine("[Flags]");
    sb.AppendLine($"public enum {table.EnumName} {{");
    while (reader.Read())
    {
        var description = (!String.IsNullOrWhiteSpace(table.DescriptionField))
            ? $"[Description(\"{RemoveQuotes(reader[table.DescriptionField].ToString()!)}\")]"
            : "";
        sb.AppendLine($"{description} {reader[table.NameField]} = {reader[table.ValueField]},");
    }

    sb.AppendLine($"}}\n");
}

if (!string.IsNullOrWhiteSpace(settings.Namespace))
    sb.AppendLine("}");

if (!String.IsNullOrWhiteSpace(settings.OutputFile))
    SaveToFile(sb.ToString(), settings);
else
    Console.WriteLine(sb.ToString());
return 0;


string RemoveQuotes(string input)
{
    return input.Replace("\"", "\\\"");
}


int SaveToFile(string source, EnumSettings enumSettings)
{
    if (!settings.OutputFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
    {
        File.WriteAllText(settings.OutputFile, sb.ToString());
        return 0;
    }

    using var peStream = new MemoryStream();
    var fileName = enumSettings.OutputFile;
    var result = GenerateCode(source, fileName!, enumSettings.Namespace!).Emit(peStream);
    if (!result.Success)
    {
        Console.Error.WriteLine("Compilation errors");
        var failures = result.Diagnostics.Where(diagnostic =>
            diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
        foreach (var diagnostic in failures)
        {
            Console.Error.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
        }

        return 3;
    }

    Console.WriteLine($"File exported: {Path.GetFullPath(fileName!)}");

    peStream.Seek(0, SeekOrigin.Begin);

    File.WriteAllBytes(fileName!, peStream.ToArray());
    return 0;
}

static CSharpCompilation GenerateCode(string sourceCode, string filename, string assName)
{
    var codeString = SourceText.From(sourceCode);
    var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp6);
    var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);
    var references = new List<MetadataReference>
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.ComponentModel.DescriptionAttribute).Assembly.Location)
    };
    Assembly.GetEntryAssembly()
        ?.GetReferencedAssemblies().ToList()
        .ForEach(r => references.Add(MetadataReference.CreateFromFile(Assembly.Load(r).Location)));

    return CSharpCompilation.Create(assName,
        new[] {parsedSyntaxTree},
        references: references,
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
}
