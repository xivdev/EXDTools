using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Lumina;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;
using Lumina.Misc;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EXDTooler;

public sealed class ColDefReader(ImmutableSortedDictionary<string, List<ExcelColumnDefinition>> dict)
{
    public ImmutableSortedDictionary<string, List<ExcelColumnDefinition>> Sheets { get; } = dict;

    private byte[]? hash;
    public byte[] Hash => hash ??= CalcHash();
    public string HashString => Convert.ToHexStringLower(Hash);

    public static ColDefReader FromInputs(string? gamePath, string? file)
    {
        if (gamePath != null)
            return FromGameData(gamePath);

        ArgumentNullException.ThrowIfNull(file);
        return FromColumnFile(file);
    }

    public static ColDefReader FromColumnFile(string file)
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

        using var f = File.OpenText(file);
        var sheets = deserializer.Deserialize<Dictionary<string, List<ExcelColumnDefinition>>>(f);
        return new(sheets.ToImmutableSortedDictionary());
    }

    public static ColDefReader FromGameData(string gamePath)
    {
        Log.Verbose("Loading game data");
        using var gameData = new GameData(gamePath, new LuminaOptions()
        {
            CacheFileResources = false
        });

        return new(
            gameData.Excel.SheetNames
                .Where(p => !p.Contains('/'))
                .Select(sheetName => (sheetName, gameData.GetFile<ExcelHeaderFile>($"exd/{sheetName}.exh")!))
                .Select(pair => KeyValuePair.Create(pair.sheetName, pair.Item2.ColumnDefinitions.ToList()))
                .ToImmutableSortedDictionary()
        );
    }

    public uint GetColumnsHash(string sheetName) =>
        Crc32.Get(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(Sheets[sheetName])));

    public void WriteTo(TextWriter writer)
    {
        var schemaSerializer = new SerializerBuilder()
            .DisableAliases()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(LowerCaseNamingConvention.Instance)
            .WithIndentedSequences()
            .EnsureRoundtrip()
            .Build();

        schemaSerializer.Serialize(writer, Sheets);
    }

    private byte[] CalcHash()
    {
        using var s = new MemoryStream();
        {
            var w = new BinaryWriter(s);

            foreach (var sheet in Sheets)
            {
                w.Write(sheet.Key);
                foreach (var item in sheet.Value)
                {
                    w.Write((ushort)item.Type);
                    w.Write(item.Offset);
                }
            }

            w.Flush();
        }

        return SHA1.HashData(s.ToArray());
    }
}