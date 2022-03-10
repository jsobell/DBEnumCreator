using System.Collections.Generic;

public class EnumSettings
{
    public string? ConnectionString { get; set; }
    public string? OutputFile { get; set; }
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