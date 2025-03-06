using System.Numerics;
using EXDTooler.Schema;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.Validators;

public sealed class LinkConditionType : IValidator<LinkConditionType>
{
    private LinkConditionType() { }

    public static void Validate(Sheet sheet, IReadOnlyList<ExcelColumnDefinition> cols, ColDefReader colDefs) =>
        ValidateFields(sheet.Fields, Utils.OrderByOffset(cols));

    private static int GetColumnCount(Field field)
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

    private static void ValidateFields(List<Field> fields, ReadOnlyMemory<ExcelColumnDefinition> cols)
    {
        foreach (var field in fields)
        {
            var colCount = GetColumnCount(field);
            var colData = cols[..colCount];
            cols = cols[colCount..];
            if (field.Type == FieldType.Link && field.Condition?.Cases?.Keys is { } keys)
            {
                var colType = colData.Span[0].Type;
                (BigInteger Min, BigInteger Max) colIntType = colType switch
                {
                    ExcelColumnDataType.Int8 => (sbyte.MinValue, sbyte.MaxValue),
                    ExcelColumnDataType.UInt8 => (byte.MinValue, byte.MaxValue),
                    ExcelColumnDataType.Int16 => (short.MinValue, short.MaxValue),
                    ExcelColumnDataType.UInt16 => (ushort.MinValue, ushort.MaxValue),
                    ExcelColumnDataType.Int32 => (int.MinValue, int.MaxValue),
                    ExcelColumnDataType.UInt32 => (uint.MinValue, uint.MaxValue),
                    ExcelColumnDataType.Int64 => (long.MinValue, long.MaxValue),
                    ExcelColumnDataType.UInt64 => (ulong.MinValue, ulong.MaxValue),
                    _ => (0, 0)
                };
                if (colIntType == (0, 0))
                    throw new ValidationException("Invalid column type for link condition");

                foreach (var key in keys)
                {
                    if (key < colIntType.Min || key > colIntType.Max)
                        throw new ValidationException($"Link condition key {key} out of range for column type {colType}");
                }
            }
            else if (field.Type == FieldType.Array && field.Fields != null)
                ValidateFields(field.Fields, colData);
        }
    }
}