using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;

namespace EXDTooler.Validators;

public sealed class DisplayField : IValidator<DisplayField>
{
    private DisplayField() { }

    public static void Validate(Sheet sheet, ExcelHeaderFile header, GameData data)
    {
        if (sheet.DisplayField == null)
            return;

        if (!sheet.Fields.Any(f => f.Name == sheet.DisplayField))
            throw new ValidationException($"Display field '{sheet.DisplayField}' not found in sheet '{sheet.Name}'");
    }
}