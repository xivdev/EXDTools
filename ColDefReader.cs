using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security.Cryptography;
using Lumina;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;
using Lumina.Misc;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EXDTooler;

public sealed class ColDefReader(ImmutableSortedDictionary<string, ExcelColumnDefinition[]> dict,
    ImmutableSortedSet<string> subrows)
{
    public ImmutableSortedDictionary<string, ExcelColumnDefinition[]> Sheets { get; } = dict;
    private ImmutableSortedSet<string> SubrowSheets { get; } = subrows;

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
        var sheets = deserializer.Deserialize<Dictionary<string, ExcelColumnDefinition[]>>(f);

        var subrowSheets = sheets.Where(sheet => sheet.Key.EndsWith("@Subrow"))
            .Select(sheet => sheet.Key[..^7])
            .ToImmutableSortedSet();
        foreach (var sheet in subrowSheets)
        {
            sheets[sheet] = sheets[$"{sheet}@Subrow"];
            sheets.Remove($"{sheet}@Subrow");
        }

        if (subrowSheets.Count == 0)
        {
            // If no subrow sheets are defined, use the default set
            subrowSheets = [.. DefaultSubrowSheets.Where(sheets.ContainsKey)];
            Log.Warn("No subrow sheets defined in file, using default set.");
        }

        return new(sheets.ToImmutableSortedDictionary(), subrowSheets);
    }

    public static ColDefReader FromGameData(string gamePath)
    {
        Log.Verbose("Loading game data");
        using var gameData = new GameData(gamePath, new LuminaOptions()
        {
            CacheFileResources = false,
            LoadMultithreaded = true
        });

        var files = gameData.Excel.SheetNames
            .Where(p => !p.Contains('/'))
            .Select(sheetName => (sheetName, gameData.GetFile<ExcelHeaderFile>($"exd/{sheetName}.exh")!));

        return new(
            files.ToImmutableSortedDictionary(pair => pair.sheetName, pair => pair.Item2.ColumnDefinitions),
            [.. files.Where(pair => pair.Item2.Header.Variant == ExcelVariant.Subrows).Select(pair => pair.sheetName)]
        );
    }

    public ExcelColumnDefinition[] this[string sheetName] => Sheets[sheetName];

    public uint GetColumnsHash(string sheetName)
    {
        var data = MemoryMarshal.Cast<ExcelColumnDefinition, ushort>(Sheets[sheetName].AsSpan());

        // Column hashes are based on the file data, so we need to ensure the endianness matches
        if (BitConverter.IsLittleEndian)
        {
            var temp = data.ToArray();
            foreach (ref var el in temp.AsSpan())
                el = BinaryPrimitives.ReverseEndianness(el);
            data = temp.AsSpan();
        }

        return Crc32.Get(MemoryMarshal.Cast<ushort, byte>(data));
    }

    public void WriteTo(TextWriter writer)
    {
        var schemaSerializer = new SerializerBuilder()
            .DisableAliases()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEnumNamingConvention(LowerCaseNamingConvention.Instance)
            .WithIndentedSequences()
            .EnsureRoundtrip()
            .Build();

        schemaSerializer.Serialize(writer, Sheets.ToImmutableSortedDictionary(pair => $"{pair.Key}{(SubrowSheets.Contains(pair.Key) ? "@Subrow" : string.Empty)}", pair => pair.Value));
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

            foreach (var subrow in SubrowSheets)
            {
                w.Write(subrow);
            }

            w.Flush();
        }

        return SHA1.HashData(s.ToArray());
    }

    private static readonly ImmutableSortedSet<string> DefaultSubrowSheets =
    [
        "AkatsukiNote",
        "AnimaWeapon5SpiritTalk",
        "AnimaWeaponFUITalk",
        "AOZArrangement",
        "AOZContentBriefingObject",
        "ArchiveItem",
        "Behavior",
        "BGMSwitch",
        "ClassJobActionUI",
        "ClassJobResident",
        "CollectablesShopItem",
        "ContentDirectorBattleTalk",
        "ContentDirectorManagedSG",
        "ContentEventItem",
        "ContentRandomSelect",
        "ContentTodo",
        "ContentTourismConstruct",
        "Credit",
        "CreditBackImage",
        "CreditList",
        "CSBonusMission",
        "CustomTalkDefineClient",
        "CustomTalkNestHandlers",
        "CutSceneIncompQuest",
        "DawnContentParticipable",
        "DeepDungeonMap5X",
        "DescriptionPage",
        "DescriptionSection",
        "DisposalShopItem",
        "DynamicEventSet",
        "EmjCostume",
        "ENpcDressUpDress",
        "EurekaDungeonPortal",
        "FittingShopCategoryItem",
        "GathererReductionReward",
        "GatheringItemPoint",
        "GCScripShopItem",
        "GilShopItem",
        "GuidePage",
        "HousingEmploymentNpcList",
        "HousingMapMarkerInfo",
        "HWDDevLively",
        "HWDSharedGroup",
        "HWDSharedGroupControlParam",
        "InclusionShopSeries",
        "ItemBarterCheck",
        "LivelyActorGroup",
        "MapMarker",
        "MapReplace",
        "MassivePcContentBattleTalk",
        "MassivePcContentTodo",
        "MiniGameRANotes",
        "MiniGameTurnBreakPop",
        "MJIBuilding",
        "MJILivelyActor",
        "MJIStockyardManagementTable",
        "MJIVillageAppearanceUI",
        "MKDGrowDataSJob",
        "MobHuntOrder",
        "MultipleHelpPage",
        "PointMenuChoice",
        "PvPActionSort",
        "PvPSelectAction",
        "QuestAdditionalToolIcon",
        "QuestAroundPlayerHide",
        "QuestClassJobReward",
        "QuestClassJobSupply",
        "QuestDefineClient",
        "QuestEffectDefine",
        "QuestEventAreaEntranceInfo",
        "QuestLinkMarker",
        "QuestRedoIncompChapter",
        "QuestRedoValidCustomTalk",
        "QuestSceneAbortCondition",
        "QuestSetDefine",
        "QuestSubCommand",
        "RaidFinderParam",
        "ReactionEventObject",
        "ReactionEventObjectInfo",
        "ReplaceAction",
        "Resident",
        "RideShootingScheduler",
        "RideShootingTargetScheduler",
        "SatisfactionArbitration",
        "SatisfactionSupply",
        "SatisfactionSupplyRewardExp",
        "ScenarioTreeTipsClassQuest",
        "SequentialEventMultipleRange",
        "SnipeHitEvent",
        "SwitchTalkVariation",
        "SystemGraphicPreset",
        "TerritoryAetheryteList",
        "TreasureSpot",
        "VVDRouteData",
        "VVDVariantAction",
        "WeatherGroup",
        "WKSDevGradeBGSet",
        "WKSEmergencyInfo",
        "WKSEmergencyMission",
        "WKSEmergencyMissionGroup",
        "WKSMechaEventObjectGroup",
        "WKSMechaEventRewardUI",
        "WKSMechaLivelyActorGroup",
        "WKSMissionInfo",
        "WKSMissionToDoEvalutionItem",
        "WKSPioneeringTrail",
        "WKSPraiseHologram",
        "WKSZoneFloorCollision",
        "ZoneSharedGroup",
        "ZoneTimeline",
    ];
}