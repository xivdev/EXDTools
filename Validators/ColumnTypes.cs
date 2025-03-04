using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.Validators;

public sealed class ColumnTypes : IValidator<ColumnTypes>
{
    private ColumnTypes() { }

    public static void Validate(Sheet sheet, ExcelHeaderFile header, GameData data)
    {
        ReadOnlyMemory<ExcelColumnDefinition> cols = Utils.OrderByOffset(header.ColumnDefinitions);
        foreach (var f in sheet.Fields)
            ValidateColumns(f, ref cols);
        if (!cols.IsEmpty)
            throw new ValidationException("Column count mismatch");
    }

    private static void ValidateColumns(Field field, ref ReadOnlyMemory<ExcelColumnDefinition> cols)
    {
        if (field.Type == FieldType.Scalar)
        {
            cols = cols[1..];
            return;
        }
        else if (field.Type == FieldType.Array)
        {
            var count = field.Count!.Value;
            var elementCount = Utils.GetColumnCount(field.Fields ?? [new() { Type = FieldType.Scalar }]);

            var arrayCols = cols[..(elementCount * count)];
            cols = cols[(elementCount * count)..];

            var firstElementTypes = arrayCols[..elementCount].ToArray().Select(x => x.Type).ToArray();
            for (var i = 0; i < count; i++)
            {
                var element = arrayCols[(i * elementCount)..((i + 1) * elementCount)].ToArray().Select(x => x.Type);
                if (!firstElementTypes.SequenceEqual(element))
                    throw new ValidationException($"Array element type mismatch: ({string.Join(", ", element)}) != ({string.Join(", ", firstElementTypes)})");
            }

            if (field.Fields != null)
            {
                var c = arrayCols[..elementCount];
                foreach (var f in field.Fields)
                    ValidateColumns(f, ref c);
                if (!c.IsEmpty)
                    throw new ValidationException("Array element count mismatch");
            }
        }
        else if (field.Type == FieldType.Icon)
        {
            var type = cols.Span[0].Type;
            cols = cols[1..];
            if (type is not (ExcelColumnDataType.UInt32 or ExcelColumnDataType.Int32 or ExcelColumnDataType.UInt16))
                throw new ValidationException($"Icon type mismatch for {field.Name} ({type})");
        }
        else if (field.Type == FieldType.ModelId)
        {
            var type = cols.Span[0].Type;
            cols = cols[1..];
            if (type is not (ExcelColumnDataType.UInt32 or ExcelColumnDataType.UInt64))
                throw new ValidationException($"Model ID type mismatch for {field.Name} ({type})");
        }
        else if (field.Type == FieldType.Color)
        {
            var type = cols.Span[0].Type;
            cols = cols[1..];
            if (type != ExcelColumnDataType.UInt32)
                throw new ValidationException($"Color type mismatch for {field.Name} ({type})");
        }
        else if (field.Type == FieldType.Link)
        {
            var type = cols.Span[0].Type;
            cols = cols[1..];
            if (type is not (
                ExcelColumnDataType.UInt8 or ExcelColumnDataType.Int8 or
                ExcelColumnDataType.UInt16 or ExcelColumnDataType.Int16 or
                ExcelColumnDataType.UInt32 or ExcelColumnDataType.Int32 or
                ExcelColumnDataType.UInt64 or ExcelColumnDataType.Int64))
                throw new ValidationException($"Link type mismatch for {field.Name} ({type})");
        }
        else
            throw new ValidationException("Unknown field type");
    }
}