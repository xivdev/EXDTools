using System.Text.RegularExpressions;
using Lumina;
using Lumina.Data.Files.Excel;

namespace UnknownFixer;

public partial class UnknownFixer
{
	[GeneratedRegex("^Unknown[0-9]+$")]
	private static partial Regex _unknownRegex();

	public static void Main(string[] args)
	{
		// we need 2 args
		if (args.Length != 2)
		{
			Console.WriteLine("Usage: UnknownFixer.exe <exdschema directory> <output directory>");
			return;
		}
		
		var schemaDir = args[0];
		var outputDir = args[1];
		
		foreach (var file in Directory.GetFiles(schemaDir, "*.yml"))
		{
			Console.WriteLine(file);
			var preStr = File.ReadAllText(file);
			var sheet = SerializeUtil.Deserialize<Sheet>(preStr);

			int i = 0;
			foreach (var field in sheet.Fields)
			{
				if (field.Name != null && _unknownRegex().IsMatch(field.Name))
					field.Name = $"Unknown{i++}";
				if (field.Fields != null && field.Fields.Count > 1)
					CheckUnknowns(field);
			}
			
			var postStr = SerializeUtil.Serialize(sheet);
			if (preStr != postStr)
			{
				Console.WriteLine($"Fixing {sheet.Name}...");
				File.WriteAllText(Path.Combine(outputDir, $"{sheet.Name}.yml"), postStr);
			}
				
		}
	}

	private static void CheckUnknowns(Field field)
	{
		int i = 0;
		foreach (var subField in field.Fields)
		{
			if (subField.Name != null && _unknownRegex().IsMatch(subField.Name))
				subField.Name = $"Unknown{i++}";
			if (subField.Fields != null && subField.Fields.Count > 1)
				CheckUnknowns(subField);
		}
	}
	
}