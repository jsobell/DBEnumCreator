using System.Data.Odbc;
using System.Data.SqlClient;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
if (string.IsNullOrWhiteSpace(settings.ODBCConnectionString))
{
    Console.Error.WriteLine($"No ODBC connectionstring provided in settings file {Path.GetFullPath(filename)}");
    return 3;
}

var sb = new StringBuilder();
sb.AppendLine("using System.ComponentModel;\nusing System;");

if (!string.IsNullOrWhiteSpace(settings.Namespace))
    sb.AppendLine($"namespace {settings.Namespace} " + "{");

foreach (var table in settings.Tables)
{
    using OdbcConnection connection = new OdbcConnection(settings.ODBCConnectionString);
    var selectString = $"SELECT * FROM {table.TableName} order by {table.ValueField}";
    var reader = ExecuteSQL(connection, selectString);

    //using SqlConnection connection = new SqlConnection(settings.ConnectionString);
    //var command = new SqlCommand(selectString, connection);
    //connection.Open();
    //using SqlDataReader reader = command.ExecuteReader();
    if (table.IsFlags)
        sb.AppendLine("[Flags]");
    sb.AppendLine($"public enum {table.EnumName} {{");
    while (reader.Read())
    {
        var description = (!String.IsNullOrWhiteSpace(table.DescriptionField))
            ? $"[Description(\"{RemoveQuotes(reader[table.DescriptionField].ToString()!)}\")]"
            : "";
        sb.AppendLine(
            $"{description} {Variableify(reader[table.NameField!].ToString()!)} = {reader[table.ValueField!]},");
    }

    sb.AppendLine($"}}\n");
    connection.Close();
}

if (!string.IsNullOrWhiteSpace(settings.Namespace))
    sb.AppendLine("}");

if (!String.IsNullOrWhiteSpace(settings.OutputFile))
    SaveToFile(sb.ToString(), settings.Namespace!, settings.OutputFile);
else
    Console.WriteLine(sb.ToString());

if (!String.IsNullOrWhiteSpace(settings.JavascriptOutputFile))
{
    var jsb = new StringBuilder();
    jsb.AppendLine($"var {Variableify(settings.Namespace ?? "DB")} = {{");
    foreach (var table in settings.Tables)
    {
        using OdbcConnection connection = new OdbcConnection(settings.ODBCConnectionString);
        var selectString = $"SELECT * FROM {table.TableName} order by {table.ValueField}";
        var reader = ExecuteSQL(connection, selectString);
        // using SqlConnection connection = new SqlConnection(settings.ConnectionString);
        // var command = new SqlCommand(selectString, connection);
        // connection.Open();
        // using SqlDataReader reader = command.ExecuteReader();
        if (table.IsFlags)
            jsb.AppendLine("// Binary Flag based values:");
        jsb.AppendLine($"{table.EnumName}: {{");
        while (reader.Read())
        {
            var description = (!String.IsNullOrWhiteSpace(table.DescriptionField))
                ? $"  // {reader[table.DescriptionField].ToString()!}"
                : "";
            jsb.AppendLine(
                $"{Variableify(reader[table.NameField!].ToString()!)} : {reader[table.ValueField!]}, {description}");
        }
        jsb.AppendLine($"}},\n");
        connection.Close();
    }
    jsb.AppendLine($"}}\n");
    File.WriteAllText(settings.JavascriptOutputFile, jsb.ToString());
    Console.WriteLine($"File exported: {Path.GetFullPath(settings.JavascriptOutputFile!)}");
}
Console.WriteLine($"Done");
return 0;


OdbcDataReader ExecuteSQL(OdbcConnection connection, string sql)
{
    OdbcCommand command = new OdbcCommand(sql, connection);
    connection.Open();
    return command.ExecuteReader();
}


string Variableify(string fieldName)
{
    if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
    return Regex.Replace(fieldName, @"^[^A-Za-z_]+|\W+", String.Empty);
}

string RemoveQuotes(string input)
{
    return input.Replace("\"", "\\\"");
}


int SaveToFile(string source, string nameSpace, string fileName)
{
    if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
    {
        File.WriteAllText(fileName, sb.ToString());
        Console.WriteLine($"File exported: {Path.GetFullPath(settings.OutputFile!)}");
    }
    else
    {
        using var peStream = new MemoryStream();
        var result = GenerateCode(source, fileName!, nameSpace!, false).Emit(peStream);
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
    }

    // Version for designer to use (.NET4.72)
    if (!string.IsNullOrWhiteSpace(settings.DesignerOutputFile))
    {
        var designerFileName = Path.Combine(settings.DesignerOutputFile);
        using var peStream = new MemoryStream();
        var result = GenerateCode(source, designerFileName!, nameSpace!, true).Emit(peStream);
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

        Console.WriteLine($"File exported: {Path.GetFullPath(designerFileName!)}");
        peStream.Seek(0, SeekOrigin.Begin);
        File.WriteAllBytes(designerFileName!, peStream.ToArray());
    }

    GenerateTypeImports(settings);

    return 0;
}

static bool GenerateTypeImports(EnumSettings settings)
{
    if (string.IsNullOrWhiteSpace(settings.TypeImportsFile))
        return false;
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("<typeImports>");
    foreach (var table in settings.Tables)
    {
        sb.AppendLine(
            $"<typeImport typeName=\"{settings.Namespace}.{table.EnumName}\" assemblyFile=\"{settings.DesignerOutputFile}\"/>");
    }

    sb.AppendLine("</typeImports>");
    File.WriteAllText(settings.TypeImportsFile, sb.ToString());
    Console.WriteLine($"File exported: {Path.GetFullPath(settings.TypeImportsFile!)}");
    return true;
}

static CSharpCompilation GenerateCode(string sourceCode, string filename, string assName, bool forLLBLGen)
{
    var codeString = SourceText.From(sourceCode);
    var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp6);
    var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);
    List<MetadataReference>? references;
    if (forLLBLGen)
    {
        references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(
                @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll"),
            MetadataReference.CreateFromFile(
                @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\System.dll"),
            MetadataReference.CreateFromFile(
                @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\System.ComponentModel.Composition.dll")
        };
    }
    else
    {
        references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.ComponentModel.DescriptionAttribute).Assembly.Location)
        };
        Assembly.GetEntryAssembly()
            ?.GetReferencedAssemblies().ToList()
            .ForEach(r => references.Add(MetadataReference.CreateFromFile(Assembly.Load(r).Location)));
    }

    return CSharpCompilation.Create(assName,
        new[] {parsedSyntaxTree},
        references: references,
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
}