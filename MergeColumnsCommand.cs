using DotMake.CommandLine;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class MergeColumnsCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to base directory with folders.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string BasePath { get; set; }

    [CliOption(Required = false, Description = "Path to columns.yml file within a BasePath subfolder.")]
    public string ColumnsPath { get; set; } = ".github/columns.yml";

    public Task RunAsync()
    {
        var token = Parent.Init();

        var files = Directory.GetDirectories(BasePath).Select(p => (Version: Path.GetFileName(p), Path: Path.Combine(p, ColumnsPath))).OrderByDescending(f => f.Version).ToArray();

        ColDefReader? baseSheets = null;
        Dictionary<(string Sheet, uint Hash), string> latestFiles = [];
        foreach (var file in files)
        {
            Log.Info($"Version: {file.Version}");

            var sheets = ColDefReader.FromColumnFile(file.Path);
            Log.Info($"Hash: {sheets.HashString}");


            foreach (var sheet in sheets.Sheets)
            {
                var hash = sheets.GetColumnsHash(sheet.Key);
                if (latestFiles.TryGetValue((sheet.Key, hash), out var latestVersion))
                {
                    var latestSheet = Path.Combine(BasePath, latestVersion, $"{sheet.Key}.yml");
                    var oldSheet = Path.Combine(BasePath, file.Version, $"{sheet.Key}.yml");
                    File.Copy(latestSheet, oldSheet, true);
                }
                else
                    latestFiles.Add((sheet.Key, hash), file.Version);
            }

            baseSheets = sheets;
        }

        return Task.CompletedTask;
    }
}