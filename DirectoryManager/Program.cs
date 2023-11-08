using System.Text.Json;
using DirectoryManager.Extraction;
using DirectoryManager.Updating;
using DirectoryManager.Utility;
using EXDCommon.FileAccess.Directory;
using Serilog;

namespace DirectoryManager;

public class Program
{
	private static readonly GameVersion _baseVersion = GameVersion.Parse("2012.01.01.0000.0000");
	
	public static void Main(string[] args)
	{
		if (args.Length != 2)
			throw new Exception("Usage: EXDWorker.exe <output directory> <storage directory>");
		
		// serilog logger setup with file, console output, and rolling file with format YYYY-MM-DD
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()
			.WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
			.CreateLogger();
		
		var outputDirectory = args[0];
		var storageDirectory = args[1];
		
		Log.Information($"Starting up with output directory {outputDirectory} and storage directory {storageDirectory}.");
		
		var versions = Directory
			.GetFiles(outputDirectory, "*.json")
			.Select(File.ReadAllText)
			.Select(text => JsonSerializer.Deserialize<PatchDataDirectory>(text))
			.Select(directory => directory?.Version)
			.ToList();
		var currentVersion = versions is not { Count: 0 } ? versions.Max()! : _baseVersion;
		Log.Information($"Our version is: {currentVersion}");

		if (!PatchGrabber.NeedsUpdate(currentVersion))
		{
			Log.Information($"{currentVersion} is latest, exiting.");
			return;
		}
		
		Log.Information($"Downloading patches!");
		using var patchDirectory = new TempDirectory();
		PatchGrabber.Execute(currentVersion, patchDirectory.Path);
		
		var patches = new List<(string path, GameVersion version)>();
		
		foreach (var patchFile in Directory.GetFiles(patchDirectory.Path, "*.patch"))
		{
			if (!Path.GetFileNameWithoutExtension(patchFile).StartsWith("D")) continue;
			var version = GameVersion.Parse(Path.GetFileNameWithoutExtension(patchFile)[1..]);
			patches.Add((patchFile, version));
		}
		
		patches
			.GroupBy(patch => patch.version.Year * 10000 + patch.version.Month * 100 + patch.version.Day)
			.OrderBy(g => g.Key)
			.ToList()
			.ForEach(g =>
			{
				var extractor = new EXDExtractor();
				extractor.Process(g.Select(t => t.path).ToList(), storageDirectory, outputDirectory);
			});
		Log.Information($"Done.");
	}
}