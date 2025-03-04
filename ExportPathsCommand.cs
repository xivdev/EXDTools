using System.Collections.Immutable;
using DotMake.CommandLine;
using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class ExportPathsCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to the schema directory. Should be a folder with just .yml schemas.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string SchemaPath { get; set; }

    [CliOption(Required = true, Description = "Path to the game directory. Should be the root of the game's repository.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string GamePath { get; set; }

    [CliOption(Required = true, Description = "Path to the output directory.")]
    public required string OutputPath { get; set; }

    public Task RunAsync()
    {
        var token = Parent.Init();

        Log.Verbose("Loading game data");
        using var gameData = new GameData(GamePath, new LuminaOptions()
        {
            CacheFileResources = false
        });

        Directory.CreateDirectory(OutputPath);

        var schemaDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var schemaSerializer = new SerializerBuilder()
            .DisableAliases()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(LowerCaseNamingConvention.Instance)
            .WithIndentedSequences()
            .Build();

        var files = Directory.EnumerateFiles(SchemaPath, "*.yml").ToArray();

        foreach (var sheetName in gameData.Excel.SheetNames)
        {
            if (sheetName.Contains('/'))
                continue;

            var header = gameData.GetFile<ExcelHeaderFile>($"exd/{sheetName}.exh")!;
            var hash = $"{header.GetColumnsHash():X8}";
            IEnumerable<string>? schemaPaths = null;
            var sheetFile = Path.Combine(SchemaPath, $"{sheetName}.yml");
            if (File.Exists(sheetFile))
            {
                using var f = File.OpenText(sheetFile);
                var sheet = schemaDeserializer.Deserialize<Sheet>(f);
                schemaPaths = [.. GenerateFieldPaths([], sheet.Fields)];
            }

            var orderedColumns = header.ColumnDefinitions.GroupBy(c => c.Offset).OrderBy(c => c.Key).SelectMany(g => g.OrderBy(c => c.Type)).ToArray();

            var orderedPaths = schemaPaths?.ToArray() ?? new string[orderedColumns.Length];
            Array.Resize(ref orderedPaths, orderedColumns.Length);

            var entries = orderedColumns.Zip(orderedPaths, (c, p) => new FieldEntry(c, p));

            var outPath = Path.Combine(OutputPath, $"{sheetName}.yml");

            {
                using var f = File.OpenWrite(outPath);
                f.SetLength(0);
                using var writer = new StreamWriter(f);
                schemaSerializer.Serialize(writer, entries.ToArray());
            }
        }
        return Task.CompletedTask;
    }

    public static IEnumerable<string> GenerateFieldPaths(ImmutableArray<string> scope, List<Field> fields, bool isArray = false)
    {
        foreach (var field in fields)
        {
            var fieldScope = scope;
            if (isArray)
            {
                if (field.Name != null)
                    fieldScope = fieldScope.Add($".{field.Name}");
            }
            else
                fieldScope = fieldScope.Add(field.Name ?? "Unk");
            if (field.Type == FieldType.Array)
            {
                var subfields = field.Fields ?? [new Field() { Type = FieldType.Scalar }];
                for (var i = 0; i < (field.Count ?? 1); ++i)
                {
                    foreach (var item in GenerateFieldPaths(fieldScope.Add($"[{i}]"), subfields, true))
                        yield return item;
                }
            }
            else
                yield return string.Join("", fieldScope);
        }
    }

    private sealed class FieldEntry(ExcelColumnDefinition definition, string? path)
    {
        public ushort Offset { get; } = definition.Offset;

        public ExcelColumnDataType Type { get; } = definition.Type;

        public string? Path { get; } = path;
    }
}