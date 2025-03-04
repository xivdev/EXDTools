using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;

namespace EXDTooler.Validators;

public sealed class Relations : IValidator<Relations>
{
    private Relations() { }

    public static void Validate(Sheet sheet, ExcelHeaderFile header, GameData data) =>
        ValidateFields(sheet.Fields, sheet.Relations?.Values);

    private static void ValidateFields(IEnumerable<Field> fields, IEnumerable<List<string>>? relations)
    {
        if (relations == null)
            return;

        HashSet<string> fieldNames = [];
        foreach (var relation in relations)
        {
            int? count = null;
            foreach (var fieldName in relation)
            {
                if (!fieldNames.Add(fieldName))
                    throw new ValidationException($"Duplicate field name '{fieldName}' in relations");

                var field = fields.FirstOrDefault(x => x.Name == fieldName);
                if (field == null)
                    throw new ValidationException($"Field '{fieldName}' not found in sheet");

                if (field.Type != FieldType.Array)
                    throw new ValidationException($"Field '{fieldName}' is not an array");

                if (count == null)
                    count = field.Count!.Value;
                else if (count != field.Count!.Value)
                    throw new ValidationException($"Field '{fieldName}' count mismatch");
            }
            if (count == null)
                throw new ValidationException("Empty relation");
        }

        foreach (var field in fields)
        {
            if (field.Type == FieldType.Array && field.Fields != null)
                ValidateFields(field.Fields, field.Relations?.Values);
        }
    }
}