using EXDCommon.FileAccess;
using EXDCommon.FileAccess.Directory;
using EXDCommon.FileAccess.Lumina;
using Lumina;
using Newtonsoft.Json;

namespace SchemaUpdater
{
	public class Program
	{
		static void Main(string[] args)
		{
			IGameFileAccess? oldAccess = null;
			IGameFileAccess? newAccess = null;
			GameVersion? oldGameVer = null;
			GameVersion? newGameVer = null;
			string? schemaDirectory = null;
			string? outputDirectory = null;
			
			// Lumina-based:
			// args[0] = old path
			// args[1] = new path
			// args[2] = schema directory
			// args[3] = output directory (a folder named after new gamever will be created here)
			if (args.Length == 4)
			{
				var oldData = new GameData(args[0]);
				var newData = new GameData(args[1]);

				oldGameVer = GameVersion.Parse(oldData.Repositories["ffxiv"].Version);
				newGameVer = GameVersion.Parse(newData.Repositories["ffxiv"].Version);
				
				oldAccess = new LuminaFileAccess(oldData);
				newAccess = new LuminaFileAccess(newData);
				
				schemaDirectory = args[2];
				outputDirectory = args[3];
			}
			
			// Directory-based:
			// args[0] = output directory (from directory manager)
			// args[1] = storage directory
			// args[2] = old gamever
			// args[3] = new gamever
			// args[4] = schema directory
			// args[5] = output directory (a folder named after new gamever will be created here)
			if (args.Length == 6)
			{
				oldGameVer = GameVersion.Parse(args[2]);
				newGameVer = GameVersion.Parse(args[3]);

				var oldDirFilePath = Path.Combine(args[0], $"{oldGameVer}.json");
				var newDirFilePath = Path.Combine(args[0], $"{newGameVer}.json");

				var oldDirFile = JsonConvert.DeserializeObject<PatchDataDirectory>(File.ReadAllText(oldDirFilePath));
				var newDirFile = JsonConvert.DeserializeObject<PatchDataDirectory>(File.ReadAllText(newDirFilePath));
				
				oldAccess = new DirectoryFileAccess(oldDirFile, args[1]);
				newAccess = new DirectoryFileAccess(newDirFile, args[1]);

				schemaDirectory = args[4];
				outputDirectory = args[5];
			}

			if (oldAccess == null || newAccess == null || oldGameVer is null || newGameVer is null || schemaDirectory == null || outputDirectory == null)
				throw new Exception("Invalid arguments");

			var su = new SchemaUpdater(oldAccess, newAccess, schemaDirectory);
			Directory.CreateDirectory(outputDirectory);
			
			foreach (var schemaFile in Directory.EnumerateFiles(schemaDirectory, "*.yml"))
			{
				var sheetName = Path.GetFileNameWithoutExtension(schemaFile);
				var result = su.ProcessDefinition(sheetName);
                
				if (string.IsNullOrEmpty(result)) continue;
				var path = Path.Combine(outputDirectory, $"{sheetName}.yml");
				File.WriteAllText(path, result);
			}
			
			Console.WriteLine();
			Console.WriteLine("Questionable sheets (may not have updated properly):");
			foreach (var s in su.QuestionableSheets)
			{
				Console.WriteLine(s);
			}
		}
	}
}