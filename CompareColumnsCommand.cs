using DotMake.CommandLine;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class CompareColumnsCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to a directory with columns yaml files.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string ColumnsPath { get; set; }

    public Task RunAsync()
    {
        var token = Parent.Init();

        var files = Directory.GetFiles(ColumnsPath, "*.yml").Select(f => (Version: Path.GetFileNameWithoutExtension(f), Path: f)).OrderBy(f => f.Version).ToArray();

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