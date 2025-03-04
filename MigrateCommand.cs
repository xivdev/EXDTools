using System.Collections.Immutable;
using System.Text.RegularExpressions;
using DotMake.CommandLine;
using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using MinHashSharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static EXDTooler.MigrateUtils;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class MigrateCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to the schema directory. Should be a folder with just .yml schemas.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string SchemaPath { get; set; }

    [CliOption(Required = true, Description = "Path to the old game directory. Should be the root of the game's repository.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string OldGamePath { get; set; }

    [CliOption(Required = true, Description = "Path to the new game directory after the update. Should be the root of the game's repository.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string NewGamePath { get; set; }

    [CliOption(Required = false, Description = "Regex to filter out unwanted modified sheets.")]
    public string? SheetNameFilter { get; set; }

    [CliOption(Required = false, Description = "List of column indices to explicitly mark as \"new\". Columns that have been moved around will never replace a forced-upon new column.", AllowMultipleArgumentsPerToken = true)]
    public HashSet<int> ForceNewColumns { get; set; } = [];

    public Task RunAsync()
    {
        var token = Parent.Init();

        Log.Debug("Loading old data");
        using var dataOld = new GameData(OldGamePath);
        Log.Debug("Loading new data");
        using var dataNew = new GameData(NewGamePath);

        var sheetsOld = dataOld.Excel.SheetNames.Where(s => !s.Contains('/'));
        var sheetsNew = dataNew.Excel.SheetNames.Where(s => !s.Contains('/'));

        var addedSheets = sheetsNew.Except(sheetsOld).Order().ToArray();
        var deletedSheets = sheetsOld.Except(sheetsNew).Order().ToArray();

        if (addedSheets.Length != 0)
        {
            Log.Info($"New sheets ({addedSheets.Length}):");
            foreach (var sheet in addedSheets)
                Log.Info($"    {sheet}");
        }
        else
            Log.Info("No new sheets");
        Log.Info();
        if (deletedSheets.Length != 0)
        {
            Log.Info($"Deleted sheets ({deletedSheets.Length}):");
            foreach (var sheet in deletedSheets)
                Log.Info($"    {sheet}");
        }
        else
            Log.Info("No deleted sheets");
        Log.Info();

        var modifiedSheets = sheetsNew.Intersect(sheetsOld).Order().ToArray();

        var schemaDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var modifiedSheetCount = 0;
        foreach (var sheetName in modifiedSheets)
        {
            if (SheetNameFilter != null)
            {
                if (!Regex.IsMatch(sheetName, SheetNameFilter, RegexOptions.IgnoreCase | RegexOptions.Singleline))
                    continue;
            }

            string[]? schemaPaths = null;
            if (SchemaPath != null)
            {
                using var f = File.OpenText(Path.Combine(SchemaPath, $"{sheetName}.yml"));
                var sheet = schemaDeserializer.Deserialize<Sheet>(f);
                schemaPaths = [.. ExportPathsCommand.GenerateFieldPaths([], sheet.Fields)];
            }

            try
            {
                if (MigrateSheet(sheetName, dataOld, dataNew, schemaPaths))
                    modifiedSheetCount++;
            }
            catch (Exception value)
            {
                Log.Error($"Failed to migrate {sheetName}: {value.Message}");
            }
        }
        Log.Info($"{modifiedSheetCount}/{modifiedSheets.Length} sheets were modified. ({modifiedSheetCount / (double)modifiedSheets.Length * 100:0.00}%)");

        return Task.CompletedTask;
    }
    private bool MigrateSheet(string sheetName, GameData oldData, GameData newData, string[]? schemaPaths)
    {
        var oldHeader = oldData.GetFile<ExcelHeaderFile>($"exd/{sheetName}.exh")!;
        var newHeader = newData.GetFile<ExcelHeaderFile>($"exd/{sheetName}.exh")!;
        if (oldHeader.GetColumnsHash() == newHeader.GetColumnsHash())
            return false;

        Log.Info();
        Log.Info($"Modified sheet: {sheetName}");

        var orderedColumnsOld = oldHeader.ColumnDefinitions.GroupBy(c => c.Offset).OrderBy(c => c.Key).SelectMany(g => g.OrderBy(c => c.Type)).ToArray();
        var orderedColumnsNew = newHeader.ColumnDefinitions.GroupBy(c => c.Offset).OrderBy(c => c.Key).SelectMany(g => g.OrderBy(c => c.Type)).ToArray();

        var oldHashes = Enumerable.Range(0, orderedColumnsOld.Length).Select(i => new SimilarColumn()).ToArray();
        var newHashes = Enumerable.Range(0, orderedColumnsNew.Length).Select(i => new SimilarColumn()).ToArray();

        {
            void PopulateColumns(uint rowId, IExcelSheet sheet, ExcelColumnDefinition[] columns, SimilarColumn[] hashes)
            {
                switch (oldHeader.Header.Variant)
                {
                    case ExcelVariant.Default:
                        {
                            var row = ((ExcelSheet<RawRow>)sheet).GetRow(rowId);
                            for (var n = 0; n < columns.Length; n++)
                                row.AppendHashCode(hashes[n], columns[n]);
                            break;
                        }
                    case ExcelVariant.Subrows:
                        {
                            var row = ((SubrowExcelSheet<RawSubrow>)sheet).GetRow(rowId).First();
                            for (var m = 0; m < columns.Length; m++)
                                row.AppendHashCode(hashes[m], columns[m]);
                            break;
                        }
                }
            }

            uint ResolveRowIdAt(int i, IExcelSheet sheet) =>
                oldHeader.Header.Variant switch
                {
                    ExcelVariant.Default => ((ExcelSheet<RawRow>)sheet).GetRowAt(i).RowId,
                    ExcelVariant.Subrows => ((SubrowExcelSheet<RawSubrow>)sheet).GetRowAt(i).RowId,
                    _ => throw new InvalidDataException("Invalid variant")
                };

            var sheetType = oldHeader.Header.Variant switch
            {
                ExcelVariant.Default => typeof(RawRow),
                ExcelVariant.Subrows => typeof(RawSubrow),
                _ => throw new InvalidDataException("Invalid variant"),
            };

            var oldSheet = oldData.Excel.GetBaseSheet(sheetType, name: sheetName);
            var newSheet = newData.Excel.GetBaseSheet(sheetType, name: sheetName);

            var oldRowIds = Enumerable.Range(0, oldSheet.Count).Select(i => ResolveRowIdAt(i, oldSheet)).ToArray();
            var newRowIds = Enumerable.Range(0, newSheet.Count).Select(i => ResolveRowIdAt(i, newSheet)).ToArray();

            var comparableRowIds = oldRowIds.Intersect(newRowIds).Shuffle(new()).ToArray();

            if (comparableRowIds.Length == 0)
                throw new InvalidDataException("No rows to compare");

            foreach (var rowId in comparableRowIds)
            {
                PopulateColumns(rowId, oldSheet, orderedColumnsOld, oldHashes);
                PopulateColumns(rowId, newSheet, orderedColumnsNew, newHashes);
            }
        }

        var movedHashes = new (int OldColumnIdx, bool IsMixed)[newHashes.Length];
        var usedColumns = new HashSet<int>();
        var unusedColumns = new HashSet<int>(Enumerable.Range(0, oldHashes.Length));
        for (var i = 0; i < newHashes.Length; ++i)
        {
            var (oldCol, score) = ForceNewColumns.Contains(i) ? ([], 0) : newHashes[i].FindBest(oldHashes, 0.8f, usedColumns);
            var isMixed = oldCol.Count > 1;
            if (isMixed)
            {
                var chosen = oldCol.GroupBy(k => Math.Abs(i - k)).MinBy(k => k.Key)!.Min();
                Log.Warn($"Column {i} has equal candidates ({string.Join(", ", oldCol)}; {score:0.0000}; {chosen} was chosen)");
                oldCol = [chosen];
            }
            if (oldCol.Count != 0)
            {
                usedColumns.Add(oldCol[0]);
                unusedColumns.Remove(oldCol[0]);
            }
            movedHashes[i] = (oldCol.FirstOrDefault(-1), isMixed);
        }

        var deletedHashes = unusedColumns.Order().ToArray();
        if (deletedHashes.Length != 0)
        {
            Log.Info("Deleted columns:");
            foreach (var colIdx in deletedHashes)
            {
                var (newCols, similarity) = oldHashes[colIdx].FindBest(newHashes);
                Log.Info($"    {colIdx} ({(schemaPaths?[colIdx]) ?? "Unknown"}) => ({string.Join(", ", newCols)}; {similarity:0.0000})");
            }
            Log.Info();
        }

        Log.Info("Columns:");
        for (var l = 0; l < movedHashes.Length; l++)
        {
            var (oldIdx, isMixed) = movedHashes[l];
            var arrowChar = (oldIdx == l) ? '-' : '=';
            if (oldIdx == -1)
            {
                Log.Info($"    {l} <= New");
                continue;
            }
            Log.Info($"    {l} <{arrowChar} {oldIdx}{(isMixed ? "*" : "")} ({(schemaPaths?[oldIdx]) ?? "Unknown"}) ({newHashes[l].Similarity(oldHashes[oldIdx]):0.0000})");
        }

        return true;
    }
}