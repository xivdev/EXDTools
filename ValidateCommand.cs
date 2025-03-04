using System.Text.Json;
using DotMake.CommandLine;
using EXDTooler.BreakingValidators;
using EXDTooler.Schema;
using EXDTooler.Validators;
using Json.Schema;
using Lumina;
using Lumina.Data.Files.Excel;
using Yaml2JsonNode;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class ValidateCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = true, Description = "Path to the schema directory. Should be a folder with just .yml schemas.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string SchemaPath { get; set; }

    [CliOption(Required = true, Description = "Path to the game directory. Should be the root of the game's repository.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string GamePath { get; set; }

    [CliOption(Required = false, Description = "Path to the base schema directory. Should be a folder with just .yml schemas. Used for ensuring no breaking changes occured.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public string? BaseSchemaPath { get; set; }

    [CliOption(Required = false, Description = "Path to schema.json", ValidationRules = CliValidationRules.ExistingFile)]
    public string? JsonSchemaPath { get; set; }

    // [CliOption(Required = false, Description = "Path to sheetHashes.json file. Contains the hashes of all known sheets in the game in all versions.", ValidationRules = CliValidationRules.ExistingFile)]
    // public string? SheetHashesPath { get; set; }

    [CliOption(Required = false, Description = "List of schema file paths to verify", Arity = CliArgumentArity.OneOrMore, ValidationRules = CliValidationRules.ExistingFile)]
    public string[]? FilesToVerify { get; set; }

    public Task<int> RunAsync()
    {
        var token = Parent.Init();

        Log.Verbose("Loading game data");
        using var gameData = new GameData(GamePath, new LuminaOptions()
        {
            CacheFileResources = false
        });

        // Dictionary<string, Dictionary<string, uint>>? sheetHashes = null;
        // if (SheetHashesPath is not null)
        // {
        //     using var f = File.OpenRead(SheetHashesPath);
        //     sheetHashes = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, uint>>>(f).ConfigureAwait(false);
        // }

        var schema = JsonSchemaPath != null ? JsonSchema.FromFile(JsonSchemaPath) : null;
        var schemaOptions = new EvaluationOptions
        {
            ValidateAgainstMetaSchema = true,
            RequireFormatValidation = true,
            OutputFormat = OutputFormat.Flag,
            OnlyKnownFormats = true,
            AllowReferencesIntoUnknownKeywords = false,
        };
        var schemaEvaluator = (YamlDocument d) => schema!.Evaluate(d.ToJsonNode(), schemaOptions);
        if (schema == null)
            schemaEvaluator = null;

        var schemaDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

        if (FilesToVerify == null || FilesToVerify.Length == 0)
            FilesToVerify = [.. Directory.EnumerateFiles(SchemaPath, "*.yml")];
        else
            FilesToVerify = [.. FilesToVerify.Select(f => Path.GetFullPath(f, SchemaPath))];
        Log.Verbose($"Verifying {FilesToVerify.Length} files");

        var validatedFiles = 0;
        foreach (var (idx, sheetFile) in FilesToVerify.Index())
        {
            var baseSheetFile = BaseSchemaPath == null ? null : Path.Combine(BaseSchemaPath, Path.GetRelativePath(SchemaPath, sheetFile));
            if (BaseSchemaPath != null && !File.Exists(baseSheetFile))
            {
                Log.Warn($"Cannot verify {Path.GetFileNameWithoutExtension(sheetFile)} for breaking changes. {baseSheetFile} does not exist.");
                baseSheetFile = null;
            }

            if (Validate(sheetFile, gameData, schemaDeserializer, schemaEvaluator, baseSheetFile))
                validatedFiles++;

            if ((idx & 3) == 0)
                Log.VerboseProgress($"Verified {validatedFiles}/{idx + 1} files. ({(idx + 1) / (double)FilesToVerify.Length * 100:0.00}% done)");
        }
        Log.VerboseClearLine();
        Log.Verbose($"Verified {validatedFiles}/{FilesToVerify.Length} files. ({(FilesToVerify.Length == 0 ? 1 : (validatedFiles / (double)FilesToVerify.Length)) * 100:0.00}%)");
        return Task.FromResult(validatedFiles == FilesToVerify.Length ? 0 : 1);
    }

    private static bool Validate(string sheetFile, GameData gameData, IDeserializer schemaDeserializer, Func<YamlDocument, EvaluationResults>? evaluateSchema, string? baseSheetFile)
    {
        if (evaluateSchema != null)
        {
            using var f = File.OpenText(sheetFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(f);
            if (yamlStream.Documents.Count > 1)
            {
                Log.Error($"Multiple documents in {sheetFile} (??)");
                return false;
            }
            var results = evaluateSchema(yamlStream.Documents[0]);
            if (!results.IsValid)
            {
                Log.Error($"Failed to validate {sheetFile}: {results}");
                return false;
            }
        }
        Sheet sheet;
        {
            using var f = File.OpenText(sheetFile);
            sheet = schemaDeserializer.Deserialize<Sheet>(f);
        }
        if (sheet == null)
        {
            Log.Error($"Failed to deserialize {sheetFile}");
            return false;
        }

        if (Path.GetFileNameWithoutExtension(sheet.Name) != sheet.Name)
        {
            Log.Error($"Sheet {sheetFile} does not match its file name");
            return false;
        }

        var headerFile = gameData.GetFile<ExcelHeaderFile>($"exd/{sheet.Name}.exh");
        if (headerFile == null)
        {
            Log.Error($"Failed to load {sheet.Name}.exh");
            return false;
        }

        bool[] checks = [
            Validate<ColumnCount>(sheet, headerFile, gameData),
                Validate<ColumnTypes>(sheet, headerFile, gameData),
                Validate<DisplayField>(sheet, headerFile, gameData),
                Validate<LinkConditionType>(sheet, headerFile, gameData),
                Validate<LinkSwitchField>(sheet, headerFile, gameData),
                Validate<Relations>(sheet, headerFile, gameData),
                Validate<SheetRefs>(sheet, headerFile, gameData),
            ];

        var pendingChecks = checks;
        if (sheet.PendingFields != null)
        {
            sheet.Fields = sheet.PendingFields;
            pendingChecks = [
                Validate<ColumnCount>(sheet, headerFile, gameData),
                    Validate<ColumnTypes>(sheet, headerFile, gameData),
                    true,
                    Validate<LinkConditionType>(sheet, headerFile, gameData),
                    Validate<LinkSwitchField>(sheet, headerFile, gameData),
                    Validate<Relations>(sheet, headerFile, gameData),
                    Validate<SheetRefs>(sheet, headerFile, gameData),
                ];
        }

        if (checks.Any(x => !x) || pendingChecks.Any(x => !x))
            return false;

        if (baseSheetFile != null)
        {
            Sheet baseSheet;
            {
                using var f = File.OpenText(baseSheetFile);
                baseSheet = schemaDeserializer.Deserialize<Sheet>(f);
            }

            bool[] baseChecks = [
                Validate<FieldNamesAndTypes>(baseSheet, sheet, headerFile, gameData)
            ];

            if (!baseChecks.Any(x => x))
                return false;
        }
        return true;
    }

    private static bool Validate<T>(Sheet sheet, ExcelHeaderFile header, GameData data) where T : IValidator<T>
    {
        try
        {
            T.Validate(sheet, header, data);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to validate {sheet.Name} with {typeof(T).Name}: {ex.Message}");
            return false;
        }
    }

    private static bool Validate<T>(Sheet baseSheet, Sheet newSheet, ExcelHeaderFile header, GameData data) where T : IBreakingValidator<T>
    {
        try
        {
            T.Validate(baseSheet, newSheet, header, data);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to validate breaking changes {newSheet.Name} with {typeof(T).Name}: {ex.Message}");
            return false;
        }
    }
}