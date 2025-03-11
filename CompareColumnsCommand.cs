using DotMake.CommandLine;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class CompareColumnsCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to base directory with folders.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string BasePath { get; set; }

    [CliOption(Required = false, Description = "Path to columns.yml file within a BasePath subfolder.")]
    public string ColumnsPath { get; set; } = ".github/columns.yml";

    public Task RunAsync()
    {
        var token = Parent.Init();

        var files = Directory.GetDirectories(BasePath).Select(p => (Version: Path.GetFileName(p), Path: Path.Combine(p, ColumnsPath))).OrderBy(f => f.Version).ToArray();

        ColDefReader? baseSheets = null;
        foreach (var file in files)
        {
            var sheets = ColDefReader.FromColumnFile(file.Path);

            var baseSheetNames = baseSheets?.Sheets.Keys;
            var sheetNames = sheets.Sheets.Keys;

            var deleted = baseSheetNames?.Except(sheetNames) ?? [];
            var added = sheetNames.Except(baseSheetNames ?? []);
            var overlap = baseSheetNames?.Intersect(sheetNames) ?? [];

            Log.Info($"Version: {file.Version}");
            Log.Info($"Hash: {sheets.HashString}");

            foreach (var sheet in deleted)
                Log.Info($"- {sheet}");

            foreach (var sheet in added)
                Log.Info($"+ {sheet}");

            foreach (var sheet in overlap)
            {
                if (sheets.GetColumnsHash(sheet) != baseSheets?.GetColumnsHash(sheet))
                    Log.Info($"* {sheet}");
            }

            baseSheets = sheets;
        }

        return Task.CompletedTask;
    }
}