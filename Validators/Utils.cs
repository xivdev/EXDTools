using EXDTooler.Schema;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.Validators;

public static class Utils
{
    public static int GetColumnCount(Field field)
    {
        if (field.Type == FieldType.Array)
        {
            if (field.Fields == null)
                return field.Count!.Value;

            var count = 0;
            foreach (var f in field.Fields)
                count += GetColumnCount(f);
            return count * field.Count!.Value;
        }
        return 1;
    }

    public static int GetColumnCount(IEnumerable<Field> field)
    {
        var count = 0;
        foreach (var f in field)
            count += GetColumnCount(f);
        return count;
    }

    public static ExcelColumnDefinition[] OrderByOffset(IEnumerable<ExcelColumnDefinition> columns) =>
        [.. columns.GroupBy(c => c.Offset).OrderBy(c => c.Key).SelectMany(g => g.OrderBy(c => c.Type))];
}