using System.Collections.Generic;

public class EnumSettings
{
    public string? ODBCConnectionString { get; set; }
    public string OutputFile { get; set; } = "";
    public string? DesignerOutputFile { get; set; }
    public string? JavascriptOutputFile { get; set; }
    public string? TypeImportsFile { get; set; }
    public string? Namespace { get; set; }
    public List<TableDetails> Tables { get; set; } = new List<TableDetails>();

    public class TableDetails
    {
        public string? EnumName { get; set; }
        public string? TableName { get; set; }
        public string? NameField { get; set; }
        public string? ValueField { get; set; }
        public string? DescriptionField { get; set; }
        public bool IsFlags { get; set; } = false;
    }
}