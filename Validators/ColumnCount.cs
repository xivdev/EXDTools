using EXDTooler.Schema;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.Validators;

public sealed class ColumnCount : IValidator<ColumnCount>
{
    private ColumnCount() { }

    public static void Validate(Sheet sheet, IReadOnlyList<ExcelColumnDefinition> cols, ColDefReader colDefs)
    {
        var count = Utils.GetColumnCount(sheet.Fields);
        if (count != cols.Count)
            throw new ValidationException($"Column count mismatch: {count} != {cols.Count}");
    }
}