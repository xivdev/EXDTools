using System.Text.Json;
using EXDCommon.FileAccess;
using EXDCommon.FileAccess.Directory;
using EXDCommon.FileAccess.Lumina;
using EXDCommon.SchemaModel.EXDSchema;
using EXDCommon.Utility;
using Lumina;
using Lumina.Data.Files.Excel;

namespace BasicSchemaGenerator;

public class BasicSchemaGenerator
{
	public static void Main(string[] args)
	{
		if (args.Length is not 3 and not 4)
		{
			Console.WriteLine("Usage: SchemaValidator.exe <game install directory> <schema directory> <output directory>");
			Console.WriteLine("OR: SchemaValidator.exe <json directory file> <storage path> <schema directory> <output directory>");
			return;
		}
		
		var outputDir = string.Empty;
		var schemaDir = string.Empty;

		IGameFileAccess? accessor = null;

		if (args.Length == 3)
		{
			accessor = new LuminaFileAccess(new GameData(args[0]));
			schemaDir = args[1];
			outputDir = args[2];
		}
		else if (args.Length == 4)
		{
			var directory = JsonSerializer.Deserialize<PatchDataDirectory>(File.ReadAllText(args[0]));
			if (directory == null)
			{
				Console.Error.WriteLine("Directory init failed.");
				return;
			}
			accessor = new DirectoryFileAccess(directory, args[1]);
			schemaDir = args[2];
			outputDir = args[3];
		}

		if (accessor == null)
		{
			Console.Error.WriteLine("Accessor init failed.");
			return;
		}

		var exl = accessor.GetFile<ExcelListFile>("exd/root.exl");
		var existingSheets = exl.ExdMap.Select(s => s.Key).Where(s => !s.Contains('/')).ToHashSet();
		var definedSheets = Directory.GetFiles(schemaDir, "*.yml").Select(Path.GetFileNameWithoutExtension).ToHashSet();
		var missingSheets = existingSheets.Except(definedSheets).ToHashSet();

		foreach (var sheet in missingSheets)
		{
			var newSchemaPath = Path.Combine(outputDir, $"{sheet}.yml");
			Directory.CreateDirectory(Path.GetDirectoryName(newSchemaPath));
			var exh = accessor.GetFile<ExcelHeaderFile>($"exd/{sheet}.exh");
			if (exh == null)
			{
				Console.Error.WriteLine($"Sheet {sheet} does not exist!... How?");
				continue;
			}
			var result = Generate(sheet, exh, newSchemaPath);
			var strResult = result ? "succeeded!" : "failed...";
			Console.WriteLine($"Generation of {sheet} {strResult}");
		}
	}

	public static bool Generate(string name, ExcelHeaderFile exh, string newSchemaPath)
	{
		var sheet = new Sheet { Name = name, Fields = new List<Field>() };
		for (int i = 0; i < exh.ColumnDefinitions.Length; i++)
		{
			sheet.Fields.Add(new Field { Name = $"Unknown{i}" });
		}
		
		var newSchemaStr = SerializeUtil.Serialize(sheet);
		File.WriteAllText(newSchemaPath, newSchemaStr);
		
		return true;
	}
}