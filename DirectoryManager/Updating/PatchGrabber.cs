using EXDCommon.FileAccess.Directory;
using Serilog;

namespace DirectoryManager.Updating;

/// <summary>
/// Places new patches into a provided directory based on an existing game version.
/// </summary>
public static class PatchGrabber
{
	private static readonly HttpClient _httpClient = new();
	
	static PatchGrabber()
	{
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "FFXIV PATCH CLIENT");
		_httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
		_httpClient.Timeout = TimeSpan.FromSeconds(10);
	}

	public static bool NeedsUpdate(GameVersion version)
	{
		var latestVersion = ThaliakClient.GetLatestVersion().Result;
		return version < latestVersion;
	} 

	public static void Execute(GameVersion ourVersion, string patchOutputDirectory)
	{
		Log.Information($"Begin update for version {ourVersion} @ {patchOutputDirectory}");
		Log.Information($"Getting latest versions...");
		
		var latestVersion = ThaliakClient.GetLatestVersion().Result;
		
		Log.Information($"Got latest version: {latestVersion}");
		Log.Information($"Determining updates.");
		
		if (ourVersion >= latestVersion)
		{
			Log.Information($"No updates needed.");
			return;
		}
		
		Log.Information($"Update available for {ourVersion} to {latestVersion}.");	
		Log.Information($"Getting patch URLs...");
		
		var patchFiles = ThaliakClient.GetPatchUrls().Result;
		var patchFilesNeeded = new HashSet<string>();
		foreach (var patchFile in patchFiles)
		{
			var version = GetVersion(patchFile);

			if (version <= ourVersion) continue;
			
			patchFilesNeeded.Add(patchFile);

			Log.Information($"Adding patch file {patchFile} for update (version: {version})");
		}
		
		Log.Information($"Got patch URLs.");
		Log.Information($"Downloading patch files...");
		
		var downloadTasks = new List<Task>();
		foreach (var file in patchFilesNeeded)
		{
			var fileName = Path.GetFileName(file);
			Directory.CreateDirectory(patchOutputDirectory);
			var filePath = new System.IO.FileInfo(Path.Combine(patchOutputDirectory, fileName));
			var downloadTask = DownloadFileAsync(file, filePath);
			Log.Information($"Adding task to download {file} to {filePath}");
			downloadTasks.Add(downloadTask.ContinueWith(task =>
			{
				if (task.IsFaulted)
					Log.Error($"Error downloading {file}.");
				else
					Log.Information($"Downloaded {file} successfully.");
			}));
		}
		
		Log.Information($"Awaiting downloads...");
		Task.WhenAll(downloadTasks).Wait();
		Log.Information($"Patch file downloads complete.");
	}
	
	private static GameVersion GetVersion(string pUrl)
	{
		var url = pUrl.AsSpan();
		var fileName = Path.GetFileNameWithoutExtension(url);
		var version = fileName[1.. GameVersion.PatchVersionStringLength];
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
}