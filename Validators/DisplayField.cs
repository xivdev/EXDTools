using EXDTooler.Schema;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.Validators;

public sealed class DisplayField : IValidator<DisplayField>
{
    private DisplayField() { }

    public static void Validate(Sheet sheet, IReadOnlyList<ExcelColumnDefinition> cols, ColDefReader colDefs)
    {
        if (sheet.DisplayField == null)
            return;

        if (!sheet.Fields.Any(f => f.Name == sheet.DisplayField))
            throw new ValidationException($"Display field '{sheet.DisplayField}' not found in sheet '{sheet.Name}'");
    }
}