using System.Diagnostics;
using System.Text.Json;

namespace EXDWorker;

public class PatchGrabber
{
	private static readonly HttpClient _httpClient = new();
	
	private readonly string _outputDirectory;
	
	private Stopwatch _time;

	static PatchGrabber()
	{
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "FFXIV PATCH CLIENT");
		_httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
		_httpClient.Timeout = TimeSpan.FromSeconds(10);
	}
	
	public PatchGrabber(string outputDirectory)
	{
		_outputDirectory = outputDirectory;
	}

	public void Execute()
	{
		_time = Stopwatch.StartNew();
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Begin update!");
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Determining version...");

		var ourVersion = Directory
			.GetFiles(_outputDirectory, "*.json")
			.Select(File.ReadAllText)
			.Select(text => JsonSerializer.Deserialize<PatchDataDirectory>(text))
			.Select(directory => directory?.Version)
			.Max();

		if (ourVersion is null)
		{
			Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Failing because we couldn't determine our version.");
			return;
		}
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Got version: {ourVersion}");
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Getting latest versions...");
		
		var latestVersion = ThaliakClient.GetLatestVersion().Result;
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Got latest version: {latestVersion}");
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Determining updates.");
		
		if (ourVersion >= latestVersion)
		{
			Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] No updates needed.");
			return;
		}
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Update available for {ourVersion} to {latestVersion}.");	
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Getting patch URLs...");
		
		var patchFiles = ThaliakClient.GetPatchUrls().Result;
		var patchFilesNeeded = new HashSet<string>();
		foreach (var patchFile in patchFiles)
		{
			var version = GetVersion(patchFile);

			if (version <= ourVersion) continue;
			
			patchFilesNeeded.Add(patchFile);

			Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Adding patch file {patchFile} for update (version: {version})");
		}
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Got patch URLs.");
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Creating root directory...");

		var patchDownloadDir = Path.GetTempFileName();
		File.Delete(patchDownloadDir);
		Directory.CreateDirectory(patchDownloadDir);
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Created root: {patchDownloadDir}");
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Downloading patch files...");
		
		var downloadTasks = new List<Task>();
		foreach (var file in patchFilesNeeded)
		{
			var fileName = Path.GetFileName(file);
			Directory.CreateDirectory(patchDownloadDir);
			var filePath = new System.IO.FileInfo(Path.Combine(patchDownloadDir, fileName));
			var downloadTask = DownloadFileAsync(file, filePath);
			Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Adding task to download {file} to {filePath}");
			downloadTasks.Add(downloadTask.ContinueWith(task =>
			{
				if (task.IsFaulted)
					Console.Error.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Error downloading {file}.");
				else
					Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Downloaded {file} successfully.");
			}));
		}
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Awaiting downloads...");
		Task.WhenAll(downloadTasks).Wait();
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Patch file downloads complete.");
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Beginning patch processing.");
		
		var patches = new List<(string path, GameVersion version)>();
		
		foreach (var patchFile in Directory.GetFiles(patchDownloadDir, "*.patch"))
		{
			if (!Path.GetFileNameWithoutExtension(patchFile).StartsWith("D")) continue;
			var version = GameVersion.Parse(Path.GetFileNameWithoutExtension(patchFile)[1..]);
			patches.Add((patchFile, version));
		}
		
		var outputDir = @"C:\Users\Liam\Desktop\tmp\worker\output";
		var storageDir = @"C:\Users\Liam\Desktop\tmp\worker\storage";
		
		patches
			.GroupBy(patch => patch.version.Year * 10000 + patch.version.Month * 100 + patch.version.Day)
			.OrderBy(g => g.Key)
			.ToList()
			.ForEach(g =>
			{
				var extractor = new EXDExtractor();
				extractor.Process(g.Select(t => t.path).ToList(), storageDir, outputDir);
			});
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Patch import complete.");

		Directory.Delete(patchDownloadDir, true);
		
		Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Update complete!");
	}
	
	private static GameVersion GetVersion(string pUrl)
	{
		var url = pUrl.AsSpan();
		var fileName = Path.GetFileNameWithoutExtension(url);
		var isHist = fileName.StartsWith("H");
		var length = fileName.Length - (isHist ? 1 : 0);
		var version = fileName[1.. length];
		return GameVersion.Parse(new string(version));
	}
	
	private static async Task DownloadFileAsync(string url, System.IO.FileInfo targetFile)
	{
		using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
		if (!response.IsSuccessStatusCode) throw new Exception($"Download failed with response code {response.StatusCode}");

		await using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
		await using Stream streamToWriteTo = targetFile.Create();
		await streamToReadFrom.CopyToAsync(streamToWriteTo);
	}

	public static void TestThing()
	{
		var outputDirectory = @"C:\Users\Liam\Desktop\tmp\worker\output";
		new PatchGrabber(outputDirectory).Execute();
	}
}