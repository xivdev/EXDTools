using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;

namespace EXDTooler.Validators;

public sealed class LinkSwitchField : IValidator<LinkSwitchField>
{
    private LinkSwitchField() { }

    public static void Validate(Sheet sheet, ExcelHeaderFile header, GameData data) =>
        ValidateFields(sheet.Fields);

    private static void ValidateFields(List<Field> fields, List<Field>? parentFields = null)
    {
        if (parentFields != null && fields.Count != 1)
            throw new ArgumentException("Parent fields should only be provided when the subfield is standalone.");
        foreach (var field in fields)
        {
            if (field.Type == FieldType.Link && field.Condition?.Switch is { } fieldName)
            {
                if (string.IsNullOrWhiteSpace(fieldName))
                    throw new ValidationException("Link switch field name is empty");

                var c = parentFields ?? fields;
                if (!c.Any(x => x.Name == fieldName))
                    throw new ValidationException($"Link switch field '{fieldName}' not found in sheet");
            }
            else if (field.Type == FieldType.Array && field.Fields != null)
                ValidateFields(field.Fields, field.Fields.Count == 1 ? fields : null);
        }
    }
}