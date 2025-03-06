using System.Collections.Immutable;
using DotMake.CommandLine;
using Lumina;
using Lumina.Data.Files.Excel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class ExportHashesCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to the game directory. Should be the root of the game's repository.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string GamePath { get; set; }

    [CliOption(Required = false, Description = "Path to the output file.")]
    public string? OutputPath { get; set; }

    public Task RunAsync()
    {
        var token = Parent.Init();

        Log.Verbose("Loading game data");
        using var gameData = new GameData(GamePath, new LuminaOptions()
        {
            CacheFileResources = false
        });

        var schemaDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var schemaSerializer = new SerializerBuilder()
            .DisableAliases()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(LowerCaseNamingConvention.Instance)
            .WithIndentedSequences()
            .Build();

        var hashes = gameData.Excel.SheetNames
            .Where(p => !p.Contains('/'))
            .Select(sheetName => (sheetName, gameData.GetFile<ExcelHeaderFile>($"exd/{sheetName}.exh")!))
            .Select(pair => KeyValuePair.Create(pair.sheetName, pair.Item2.GetColumnsHash()))
            .ToImmutableSortedDictionary();

        if (OutputPath != null)
        {
            using var f = File.OpenWrite(OutputPath);
            f.SetLength(0);
            using var writer = new StreamWriter(f);
            schemaSerializer.Serialize(writer, hashes);
        }
        else
            schemaSerializer.Serialize(Console.Out, hashes);
        return Task.CompletedTask;
    }
}