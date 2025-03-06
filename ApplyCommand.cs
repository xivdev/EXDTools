using DotMake.CommandLine;
using EXDTooler.Schema;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class ApplyCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to the schema directory. Should be a folder with just .yml schemas.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string SchemaPath { get; set; }

    [CliOption(Required = false, Description = "Do not apply pending fields/names. Apply formatting changes only.")]
    public bool FormatOnly { get; set; }

    public Task RunAsync()
    {
        var token = Parent.Init();

        var schemaDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var schemaSerializer = new SerializerBuilder()
            .DisableAliases()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .WithIndentedSequences()
            .WithEventEmitter(next => new SheetTargetsEmitter(next), w => w.OnTop())
            .Build();

        var files = Directory.EnumerateFiles(SchemaPath, "*.yml").ToArray();
        Log.Verbose($"Applying {files.Length} files");

        foreach (var (idx, sheetFile) in files.Index())
        {
            Sheet sheet;
            {
                using var f = File.OpenText(sheetFile);
                sheet = schemaDeserializer.Deserialize<Sheet>(f);
            }

            if (!FormatOnly)
            {
                (sheet.Fields, sheet.PendingFields) = (sheet.PendingFields ?? sheet.Fields, null);
                ApplyNames(sheet.Fields);
            }

            {
                using var f = File.OpenWrite(sheetFile);
                f.SetLength(0);
                using var writer = new StreamWriter(f);
                schemaSerializer.Serialize(writer, sheet);
            }

            if ((idx & 3) == 0)
                Log.VerboseProgress($"Applied {idx + 1}/{files.Length} files. ({(idx + 1) / (double)files.Length * 100:0.00}%)");
        }
        Log.VerboseProgressClear();
        Log.Info($"Applied {files.Length}/{files.Length} files. ({1 * 100:0.00}%)");
        return Task.CompletedTask;
    }

    private static void ApplyNames(List<Field> fields)
    {
        foreach (var field in fields)
        {
            if (field.PendingName != null)
                (field.Name, field.PendingName) = (field.PendingName, null);
            if (field.Fields != null)
                ApplyNames(field.Fields);
        }
    }

    public class SheetTargetsEmitter(IEventEmitter nextEmitter) : ChainedEventEmitter(nextEmitter)
    {
        public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
        {
            if (eventInfo.Source.Type == typeof(SheetTargets))
                eventInfo.Style = SequenceStyle.Flow;
            nextEmitter.Emit(eventInfo, emitter);
        }
    }
}