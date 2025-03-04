using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;

namespace EXDTooler.Validators;

public sealed class ColumnCount : IValidator<ColumnCount>
{
    private ColumnCount() { }

    public static void Validate(Sheet sheet, ExcelHeaderFile header, GameData data)
    {
        var count = Utils.GetColumnCount(sheet.Fields);
        if (count != header.Header.ColumnCount)
            throw new ValidationException($"Column count mismatch: {count} != {header.Header.ColumnCount}");
    }
}