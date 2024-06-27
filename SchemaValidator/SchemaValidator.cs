using EXDCommon.FileAccess;
using EXDCommon.FileAccess.Directory;
using EXDCommon.FileAccess.Lumina;
using EXDCommon.SchemaModel.EXDSchema;
using EXDCommon.Utility;
using Lumina;
using Lumina.Data.Files.Excel;
using Newtonsoft.Json;

using SchemaValidator.Validation;
using SchemaValidator.Validation.Validators;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SchemaValidator;

public class SchemaValidator
{
	public static void Main(string[] args)
	{
		if (args.Length is not 3 and not 4 and not 5)
		{
			Console.WriteLine("Usage: SchemaValidator.exe <game install directory> <json schema file> <schema directory> [CI]");
			Console.WriteLine("OR: SchemaValidator.exe <json directory file> <storage path> <json schema file> <schema directory> [CI]");
			return;
		}
		
		var schemaFile = string.Empty;
		var schemaDir = string.Empty;

		IGameFileAccess? accessor = null;

		if (args.Length == 3 || (args.Length == 4 && args[3] == "CI"))
		{
			accessor = new LuminaFileAccess(new GameData(args[0]));
			schemaFile = args[1];
			schemaDir = args[2];
		}
		else if (args.Length == 4 || args.Length == 5)
		{
			var directory = JsonSerializer.Deserialize<PatchDataDirectory>(File.ReadAllText(args[0]));
			if (directory == null)
			{
				Console.Error.WriteLine("Directory init failed.");
				return;
			}
			accessor = new DirectoryFileAccess(directory, args[1]);
			schemaFile = args[2];
			schemaDir = args[3];
		}

		if (accessor == null)
		{
			Console.Error.WriteLine("Accessor init failed.");
			return;
		}
		
		var schemaText = File.ReadAllText(schemaFile);

		var testDict = new Dictionary<string, List<int>>()
		{
			{"all", new() {0}},
		};
		
		var validators = new List<Validator>
		{
			new SchemaFileValidator(accessor, schemaText),
			new ColumnCountValidator(accessor),
			new IconTypeValidator(accessor),
			new NamedInnerNamedOuterValidator(accessor),
			new FieldNameValidator(accessor),
			new ModelIdTypeValidator(accessor),
			new ColorTypeValidator(accessor),
			new IconPathExistsValidator(accessor),
			new SingleLinkRefValidator(accessor, testDict),
			new MultiLinkRefValidator(accessor, testDict),
			new ConditionValidator(accessor),
			new ConditionRefValidator(accessor, testDict),
			new DuplicateFieldNameValidator(accessor),
			new ArrayTypingValidator(accessor),
		};

		var exl = accessor.GetFile<ExcelListFile>("exd/root.exl");
		var existingSheets = exl.ExdMap.Select(s => s.Key).ToHashSet();
		var results = new ValidationResults();
		
		foreach (var schemaPath in Directory.GetFiles(schemaDir, "*.yml"))
		{
			var sheetName = Path.GetFileNameWithoutExtension(schemaPath);
			var exh = accessor.GetFile<ExcelHeaderFile>($"exd/{sheetName}.exh");
			if (exh == null)
			{
				results.Add(ValidationResult.Error(sheetName, "SheetExistsValidator", "Schema exists but sheet does not!"));
				continue;
			}

			Sheet sheet;
			try
			{
				sheet = SerializeUtil.Deserialize<Sheet>(File.ReadAllText(schemaPath))!;
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"Sheet {sheetName} encountered an exception when deserializing!");
				Console.Error.WriteLine(e.Message);
				Console.Error.WriteLine(e.StackTrace);
				continue;
			}
			
			if (sheet == null)
			{
				Console.Error.WriteLine($"Sheet {sheetName} could not be deserialized!");
				continue;
			}

			// Console.WriteLine($"{sheetName}");

			foreach (var validator in validators)
				results.Add(validator.Validate(exh, sheet));
				
			existingSheets.Remove(sheetName);
		}

		foreach (var sheet in existingSheets)
		{
			if (sheet.Contains('/')) continue;
			results.Add(ValidationResult.Error(sheet, "SchemaDefinedValidator", "Sheet exists but has no schema!"));
		}
		
		// ---
		
		foreach (var result in results.Results.Where(r => r.Status == ValidationStatus.Warning))
		{
			var msgfmt = result.Message == "" ? "" : $" - ";
			Console.WriteLine($"{result.Status}: {result.SheetName} - {result.ValidatorName}{msgfmt}{result.Message}");
		}
		
		foreach (var result in results.Results.Where(r => r.Status == ValidationStatus.Failed))
		{
			var msgfmt = result.Message == "" ? "" : $" - ";
			Console.WriteLine($"{result.Status}: {result.SheetName} - {result.ValidatorName}{msgfmt}{result.Message}");
		}
		
		foreach (var result in results.Results.Where(r => r.Status == ValidationStatus.Error))
		{
			var msgfmt = result.Message == "" ? "" : $" - ";
			Console.WriteLine($"{result.Status}: {result.SheetName} - {result.ValidatorName}{msgfmt}{result.Message}");
		}
		
		var successCount = results.Results.Count(r => r.Status == ValidationStatus.Success);
		var warningCount = results.Results.Count(r => r.Status == ValidationStatus.Warning);
		var failureCount = results.Results.Count(r => r.Status == ValidationStatus.Failed);
		var errorCount = results.Results.Count(r => r.Status == ValidationStatus.Error);
		
		Console.WriteLine($"{successCount} success, {warningCount} warnings, {failureCount} failures, {errorCount} errors");

		// For CI
		if (args.Length == 5)
		{
			File.WriteAllText("message", $"{successCount} success, {warningCount} warnings, {failureCount} failures, {errorCount} errors");
			File.WriteAllText("success", $"{successCount}");
			File.WriteAllText("warning", $"{warningCount}");
			File.WriteAllText("failure", $"{failureCount}");
			File.WriteAllText("error", $"{errorCount}");
		}
	}
}