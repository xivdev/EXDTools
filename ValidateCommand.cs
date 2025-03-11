using System.Reflection;
using System.Text;
using DotMake.CommandLine;
using EXDTooler.BreakingValidators;
using EXDTooler.Schema;
using EXDTooler.Validators;
using Json.Schema;
using Lumina.Data.Structs.Excel;
using Yaml2JsonNode;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EXDTooler;

[CliCommand(Parent = typeof(MainCommand))]
public sealed class ValidateCommand
{
    public required MainCommand Parent { get; set; }

    [CliOption(Required = false, Description = "Path to the game directory. Should be the root of the game's repository.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public string? GamePath { get; set; }

    [CliOption(Required = false, Description = "Path to the columns file generated by export-columns.", ValidationRules = CliValidationRules.ExistingFile)]
    public string? ColumnsFile { get; set; }

    [CliOption(Required = true, Description = "Path to the schema directory. Should be a folder with just .yml schemas.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public required string SchemaPath { get; set; }

    [CliOption(Required = false, Description = "Path to the base schema directory. Should be a folder with just .yml schemas. Used for ensuring no breaking changes occured.", ValidationRules = CliValidationRules.ExistingDirectory)]
    public string? BaseSchemaPath { get; set; }

    [CliOption(Required = false, Description = "Path to a schema.json. If omitted, the built-in one will be used.", ValidationRules = CliValidationRules.ExistingFile)]
    public string? JsonSchemaPath { get; set; }

    // [CliOption(Required = false, Description = "Path to sheetHashes.json file. Contains the hashes of all known sheets in the game in all versions.", ValidationRules = CliValidationRules.ExistingFile)]
    // public string? SheetHashesPath { get; set; }

    public async Task<int> RunAsync()
    {
        var token = Parent.Init();

        var sheets = ColDefReader.FromInputs(GamePath, ColumnsFile);

        // Dictionary<string, Dictionary<string, uint>>? sheetHashes = null;
        // if (SheetHashesPath is not null)
        // {
        //     using var f = File.OpenRead(SheetHashesPath);
        //     sheetHashes = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, uint>>>(f).ConfigureAwait(false);
        // }

        JsonSchema schema;
        if (JsonSchemaPath != null)
            schema = JsonSchema.FromFile(JsonSchemaPath);
        else
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("EXDTooler.schema.json")!;
            schema = await JsonSchema.FromStream(s).ConfigureAwait(false);
        }

        var schemaOptions = new EvaluationOptions
        {
            ValidateAgainstMetaSchema = true,
            RequireFormatValidation = true,
            OutputFormat = OutputFormat.List,
            OnlyKnownFormats = true,
            AllowReferencesIntoUnknownKeywords = false,
        };

        var schemaDeserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

        string[] filesToVerify = [.. Directory.EnumerateFiles(SchemaPath, "*.yml")];
        var existingSchemas = filesToVerify.Select(f => Path.GetFileNameWithoutExtension(f));
        var requiredSchemas = sheets.Sheets.Keys;

        var missingSchemas = requiredSchemas.Except(existingSchemas);
        var extraSchemas = existingSchemas.Except(requiredSchemas);

        if (missingSchemas.Any())
        {
            foreach (var missingSchema in missingSchemas)
                Log.AnnotatedError($"Missing Schema: {missingSchema}");
        }

        if (extraSchemas.Any())
        {
            foreach (var extraSchema in extraSchemas)
                Log.AnnotatedError($"Redundant Schema: {extraSchema}", new() { File = $"{extraSchema}.yml" });
        }

        Log.Verbose($"Verifying {filesToVerify.Length} files");

        var validatedFiles = 0;
        foreach (var (idx, sheetFile) in filesToVerify.Index())
        {
            var baseSheetFile = BaseSchemaPath == null ? null : Path.Combine(BaseSchemaPath, Path.GetRelativePath(SchemaPath, sheetFile));
            if (BaseSchemaPath != null && !File.Exists(baseSheetFile))
            {
                Log.AnnotatedWarn("Cannot verify sheet for breaking changes. Base sheet does not exist.", new() { File = Path.GetFileName(sheetFile) });
                baseSheetFile = null;
            }

            if (Validate(sheetFile, sheets, schemaDeserializer, d => schema.Evaluate(d.ToJsonNode(), schemaOptions), baseSheetFile))
                validatedFiles++;

            if ((idx & 3) == 0)
                Log.VerboseProgress($"Verified {validatedFiles}/{idx + 1} files. ({(idx + 1) / (double)filesToVerify.Length * 100:0.00}% done)");
        }
        Log.VerboseProgressClear();
        Log.Info($"Verified {validatedFiles}/{filesToVerify.Length} files. ({(filesToVerify.Length == 0 ? 1 : (validatedFiles / (double)filesToVerify.Length)) * 100:0.00}%)");

        var failed = missingSchemas.Any() || extraSchemas.Any() || validatedFiles != filesToVerify.Length;

        var summary = CreateSummary(failed);
        Log.Output("summary", summary);

        return failed ? 1 : 0;
    }

    private static string CreateSummary(bool failed)
    {
        var s = new StringBuilder();

        if (failed)
            s.AppendLine("## ❌ Validation failed");
        else
            s.AppendLine("## ✅ Validation succeeded");
        s.AppendLine();

        foreach (var group in Log.Annotations.GroupBy(a => Path.GetFileNameWithoutExtension(a.Item3?.File)).OrderBy(a => a.Key))
        {
            s.AppendLine($"## {group.Key ?? "Untagged"}");
            s.AppendLine();
            foreach (var level in group.GroupBy(a => a.Item1).OrderBy(a => a.Key))
            {
                s.AppendLine("<details>");
                s.AppendLine();

                s.Append("<summary><h3>");
                var name = level.Key switch
                {
                    Log.LogLevel.Error => "❌ Errors",
                    Log.LogLevel.Warn => "⚠️ Warnings",
                    Log.LogLevel.Info => "💬 Info",
                    Log.LogLevel.Verbose => "💭 Verbose",
                    Log.LogLevel.Debug => "📝 Debug",
                    _ => "❓ Unknown",
                };
                s.Append(name);
                s.AppendLine("</h3></summary>");
                s.AppendLine();

                foreach (var note in group)
                {
                    var title = note.Item3?.Title ?? note.Item2;
                    var text = note.Item3.HasValue ? note.Item2 : null;

                    s.AppendLine($"**{title}**");
                    if (text != null)
                        s.AppendLine(text);
                    s.AppendLine();
                }

                s.AppendLine();
                s.AppendLine("</details>");
                s.AppendLine();
            }

            s.AppendLine();
        }

        return s.ToString();
    }

    private static bool Validate(string sheetFile, ColDefReader colDefs, IDeserializer schemaDeserializer, Func<YamlDocument, EvaluationResults> evaluateSchema, string? baseSheetFile)
    {
        {
            using var f = File.OpenText(sheetFile);
            var yamlStream = new YamlStream();
            yamlStream.Load(f);
            if (yamlStream.Documents.Count > 1)
            {
                Log.AnnotatedError("Multiple YAML documents in file", new() { File = Path.GetFileName(sheetFile) });
                return false;
            }
            var results = evaluateSchema(yamlStream.Documents[0]);
            if (!results.IsValid)
            {
                foreach (var result in results.Details)
                {
                    if (result.IsValid)
                        continue;
                    if (!result.HasErrors)
                        continue;

                    foreach (var error in result.Errors!.Values)
                    {
                        var s = new StringBuilder("Schema: ");
                        if (result.InstanceLocation.Count > 0)
                            s.Append(result.InstanceLocation);
                        else
                            s.Append('/');
                        s.Append(result.EvaluationPath);
                        var isInfo = result.EvaluationPath.Contains("allOf");
                        if (isInfo)
                            Log.AnnotatedInfo(error, new() { Title = s.ToString(), File = Path.GetFileName(sheetFile) });
                        else
                            Log.AnnotatedError(error, new() { Title = s.ToString(), File = Path.GetFileName(sheetFile) });
                    }
                }
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
            Log.AnnotatedError("Failed to deserialize", new() { File = Path.GetFileName(sheetFile) });
            return false;
        }

        if (Path.GetFileNameWithoutExtension(sheet.Name) != sheet.Name)
        {
            Log.AnnotatedError($"Sheet name ({sheet.Name}) does not match file name", new() { File = Path.GetFileName(sheetFile) });
            return false;
        }

        if (!colDefs.Sheets.TryGetValue(sheet.Name, out var cols))
        {
            Log.AnnotatedError("Failed to load columns", new() { File = Path.GetFileName(sheetFile) });
            return false;
        }

        bool[] checks = [
            Validate<ColumnCount>(sheet, cols, colDefs),
            Validate<ColumnTypes>(sheet, cols, colDefs),
            Validate<DisplayField>(sheet, cols, colDefs),
            Validate<LinkConditionType>(sheet, cols, colDefs),
            Validate<LinkSwitchField>(sheet, cols, colDefs),
            Validate<Relations>(sheet, cols, colDefs),
            Validate<SheetRefs>(sheet, cols, colDefs),
        ];

        var pendingChecks = checks;
        if (sheet.PendingFields != null)
        {
            sheet.Fields = sheet.PendingFields;
            pendingChecks = [
                Validate<ColumnCount>(sheet, cols, colDefs, true),
                Validate<ColumnTypes>(sheet, cols, colDefs, true),
                true,
                Validate<LinkConditionType>(sheet, cols, colDefs, true),
                Validate<LinkSwitchField>(sheet, cols, colDefs, true),
                Validate<Relations>(sheet, cols, colDefs, true),
                Validate<SheetRefs>(sheet, cols, colDefs, true),
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
                Validate<FieldNamesAndTypes>(baseSheet, sheet, cols, colDefs)
            ];

            if (!baseChecks.Any(x => x))
                return false;
        }
        return true;
    }

    private static bool Validate<T>(Sheet sheet, List<ExcelColumnDefinition> cols, ColDefReader colDefs, bool pending = false) where T : IValidator<T>
    {
        try
        {
            T.Validate(sheet, cols, colDefs);
            return true;
        }
        catch (Exception ex)
        {
            Log.AnnotatedError($"{typeof(T).Name}: {ex.Message}", new() { Title = $"Failed to validate{(pending ? " pending fields" : string.Empty)}", File = $"{sheet.Name}.yml" });
            return false;
        }
    }

    private static bool Validate<T>(Sheet baseSheet, Sheet newSheet, List<ExcelColumnDefinition> cols, ColDefReader colDefs) where T : IBreakingValidator<T>
    {
        try
        {
            T.Validate(baseSheet, newSheet, cols, colDefs);
            return true;
        }
        catch (Exception ex)
        {
            Log.AnnotatedError(ex.Message, new() { Title = $"Failed to validate breaking changes", File = $"{newSheet.Name}.yml" });
            return false;
        }
    }
}