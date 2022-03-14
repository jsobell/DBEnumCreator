# DBEnumCreator (NuGet tool)

## Generates a set of Enum definitions from a SQL Server database.

Syntax:
  `DBEnumCreator [enumsettings.json]`

(If no json file is specified the default file "enumsettings.json" will be used if present)

`OutputFile` refers to the location and name of either a C# class file or compiled .NET6 DLL of enums. If the name ends in `.dll` the code will be compiled, otherwise left as C# source code\
`DesignerOutputFile` refers to the location and name a .net4.72 DLL will be generated for inclusion in the LLBLGen Pro Designer\
`JavascriptOutputFile` refers to the location and name of a javascript file containing an object with all Enum values defined for use in a web client or SPA\
`TypeImportsFile`  refers to the location and name of an LLBLGen Pro Designer compatible typeimports file containing a reference to all of the Enums\

## Typical usage pattern for an LLBLGen Pro based project:

Use the command `dotnet tool install DBEnumCreator --global` to install the generator\
(The command `dotnet tool update DBEnumCreator --global` can be used at any time to update to the latest version)

Create an `enumsettings.json` file in the same folder as your `.llblgenproj` file, using the code below as a syntax example.\
Alternatively, type `DBEnumCreator --sample` to dump a sample settings file to the screen, or `DBEnumCreator --sample > enumsettings.json` to generate the sample file in the current folder.

Modify `enumsettings.json` to contain the appropriate database connection details, and the array of tables containing enum values to be read.\
`DescriptionField` and `IsFlags` are optional, but all other table fields are required.

It is simplest to create a simple library project, and use the `OutputFile` to write a .cs file directly into that folder,\
e.g. `"OutputFile": "DBLookupEnums/MyAppDbEnums.cs"`\
As the source code is version independent it enables a shared library to be used throughout the source solution, and this source will be updated whenever the generator is re-run.

Ensure the LLBLGen Pro Designer is closed before running the generator, as it will lock any previously generated version of the DLL.  

Run the command `DBEnumCreator` from the command prompt to have the files generated. An output will be shown such as:\
```
File exported: C:\git\MyApp\@Database\DBLookupEnums\MyAppDbEnums.cs
File exported: C:\git\MyApp\@Database\MyAppDbEnums.Designer.dll
File exported: C:\git\MyApp\@Database\MyAppDbEnums.Designer.typeimports
File exported: C:\git\MyApp\@Database\MyAppDbEnums.js
```

Opening the `.llblgenproj` file in the Designer will now automatically load the associated Designer dll, and make it available to assign to any column in any table.\
These features will flow down through all entity classes and any generated DTOs.

## Sample enumsettings.json file:
```
{
   "ConnectionString" : "Server=localhost;Database=myserver;Trusted_Connection=True;",
   "Namespace" : "MyProject.Enums",
   "OutputFile" : "DBLookupEnums\MyAppDbEnums.cs",
   "DesignerOutputFile" : "MyAppDbEnums.Designer.dll",
   "JavascriptOutputFile" : "MyAppDbEnums.js",
   "TypeImportsFile" : "MyAppDbEnums.Designer.typeimports",
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
       },
       {
           "EnumName" : "FeatureFlags",
           "TableName": "Feature_Flags",
           "NameField" : "name",
           "ValueField" : "id",
           "DescriptionField" : "description",
           "IsFlags" : true
       }
   ]
}
```

Generates source such as

```
using System.ComponentModel;
using System;
namespace MyProject.Enums {
  public enum AccountStatus {
    Unmatched = 0,
    Suggested = 1,
    Matched = 2,
    Warning = 3,
  }
  
  public enum CustomerStatus {
    [Description("Onboarded, not imported")] Initial = 0,
    [Description("Importing data")] Importing = 1,
    [Description("Data available")] Active = 2,
    [Description("Synchronisation paused")] Inactive = 3,
    [Description("Data in error state")] Errored = 4,
  }
  
  [Flags]
  public enum FeatureFlag {
    AllowReports = 1,
    AllowDetailedReports = 2,
    HasAdminRights = 4,
    CanSendEmails = 8,
  }
}
```