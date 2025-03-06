using EXDTooler.Schema;
using EXDTooler.Validators;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.BreakingValidators;

public sealed class FieldNamesAndTypes : IBreakingValidator<FieldNamesAndTypes>
{
    private FieldNamesAndTypes() { }

    public static void Validate(Sheet baseSheet, Sheet newSheet, IReadOnlyList<ExcelColumnDefinition> cols, ColDefReader colDefs)
    {
        ValidateFields(baseSheet.Fields, newSheet.Fields);
        ValidateRelations(baseSheet.Relations, newSheet.Relations);
    }

    private static void ValidateFields(List<Field> baseFields, List<Field> newFields)
    {
        if (baseFields.Count != newFields.Count)
            throw new ValidationException("Field count mismatch");

        foreach (var field in baseFields)
        {
            var newField = newFields.FirstOrDefault(x => x.Name == field.Name);
            if (newField == null)
                throw new ValidationException($"Field '{field.Name}' not found in new sheet");

            if (field.Type != newField.Type)
                throw new ValidationException($"Field '{field.Name}' type mismatch");

            if (field.Type == FieldType.Array)
            {
                if (field.Count != newField.Count)
                    throw new ValidationException($"Field '{field.Name}' count mismatch");

                var baseF = field.Fields ?? [new() { Type = FieldType.Scalar }];
                var newF = newField.Fields ?? [new() { Type = FieldType.Scalar }];
                ValidateFields(baseF, newF);
                ValidateRelations(field.Relations, newField.Relations);
            }
            else if (field.Type == FieldType.Link)
            {
                if ((field.Targets?.Count == 1) != (newField.Targets?.Count == 1))
                    throw new ValidationException($"Field '{field.Name}' cannot be converted into (or away from) a non-exhaustive enum");

                var targetList = field.Targets?.ToHashSet() ?? [.. field.Condition!.Cases!.Values.SelectMany(x => x)];
                var newTargetList = newField.Targets?.ToHashSet() ?? [.. newField.Condition!.Cases!.Values.SelectMany(x => x)];
                targetList.ExceptWith(newTargetList);
                if (targetList.Count > 0)
                    throw new ValidationException($"Field '{field.Name}' cannot remove targets ({string.Join(", ", targetList)})");
            }
        }
    }

    private static void ValidateRelations(Dictionary<string, List<string>>? baseRelations, Dictionary<string, List<string>>? newRelations)
    {
        baseRelations ??= [];
        newRelations ??= [];

        if (baseRelations.Count != newRelations.Count)
            throw new ValidationException("Relation count mismatch");

        foreach (var (name, fields) in baseRelations)
        {
            if (!newRelations.TryGetValue(name, out var newFields))
                throw new ValidationException($"Relation '{name}' not found in new sheet");

            if (!fields.SequenceEqual(newFields))
                throw new ValidationException($"Relation '{name}' field list mismatch");
        }
    }
}