# DBEnumCreator

## Generates a set of Enum definitions from a SQL Server database.

Syntax:
  `DBEnumCreator [enumsettings.json]`

If no json file is specified the default file "enumsettings.json" will be used if present.

If the outputFile ends with `.dll` the code will be compiled to a .NET6 DLL

e.g.
````
{
   "ConnectionString" : "Server=localhost;Database=myserver;Trusted_Connection=True;",
   "OutputFile" : "g_MyNewEnums.cs",
   "Namespace" : "MyProject.Enums",
   "Tables" : [
       {
           "EnumName" : "AccountStatus",
           "TableName": "Account_Status",
           "NameField" : "name",
           "ValueField" : "id",
           "IsFlags" : false
       },
       {
           "EnumName" : "CustomerStatus",
           "TableName": "Customer_Status",
           "NameField" : "name",
           "ValueField" : "id",
           "DescriptionField" : "description",
           "IsFlags" : false
       }
   ]
}
````

