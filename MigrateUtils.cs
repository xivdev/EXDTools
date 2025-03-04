using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using MinHashSharp;

namespace EXDTooler;

public static class MigrateUtils
{
    public static void AppendHashCode(this RawRow row, SimilarColumn code, ExcelColumnDefinition column)
    {
        _ = column.Type switch
        {
            ExcelColumnDataType.String => code.Add(row.ReadString(column.Offset).ExtractText()),
            ExcelColumnDataType.Bool => code.Add(row.ReadBool(column.Offset)),
            ExcelColumnDataType.Int8 => code.Add(row.ReadInt8(column.Offset)),
            ExcelColumnDataType.UInt8 => code.Add(row.ReadUInt8(column.Offset)),
            ExcelColumnDataType.Int16 => code.Add(row.ReadInt16(column.Offset)),
            ExcelColumnDataType.UInt16 => code.Add(row.ReadUInt16(column.Offset)),
            ExcelColumnDataType.Int32 => code.Add(row.ReadInt32(column.Offset)),
            ExcelColumnDataType.UInt32 => code.Add(row.ReadUInt32(column.Offset)),
            ExcelColumnDataType.Float32 => code.Add(row.ReadFloat32(column.Offset)),
            ExcelColumnDataType.Int64 => code.Add(row.ReadInt64(column.Offset)),
            ExcelColumnDataType.UInt64 => code.Add(row.ReadUInt64(column.Offset)),
            >= ExcelColumnDataType.PackedBool0 and <= ExcelColumnDataType.PackedBool7 =>
                code.Add(row.ReadPackedBool(column.Offset, (byte)(column.Type - ExcelColumnDataType.PackedBool0))),
            _ => throw new InvalidOperationException($"Unknown column type {column.Type}")
        };
    }

    internal static void AppendHashCode(this RawSubrow row, SimilarColumn code, ExcelColumnDefinition column)
    {
        _ = column.Type switch
        {
            ExcelColumnDataType.String => code.Add(row.ReadString(column.Offset).ExtractText()),
            ExcelColumnDataType.Bool => code.Add(row.ReadBool(column.Offset)),
            ExcelColumnDataType.Int8 => code.Add(row.ReadInt8(column.Offset)),
            ExcelColumnDataType.UInt8 => code.Add(row.ReadUInt8(column.Offset)),
            ExcelColumnDataType.Int16 => code.Add(row.ReadInt16(column.Offset)),
            ExcelColumnDataType.UInt16 => code.Add(row.ReadUInt16(column.Offset)),
            ExcelColumnDataType.Int32 => code.Add(row.ReadInt32(column.Offset)),
            ExcelColumnDataType.UInt32 => code.Add(row.ReadUInt32(column.Offset)),
            ExcelColumnDataType.Float32 => code.Add(row.ReadFloat32(column.Offset)),
            ExcelColumnDataType.Int64 => code.Add(row.ReadInt64(column.Offset)),
            ExcelColumnDataType.UInt64 => code.Add(row.ReadUInt64(column.Offset)),
            >= ExcelColumnDataType.PackedBool0 and <= ExcelColumnDataType.PackedBool7 =>
                code.Add(row.ReadPackedBool(column.Offset, (byte)(column.Type - ExcelColumnDataType.PackedBool0))),
            _ => throw new InvalidOperationException($"Unknown column type {column.Type}")
        };
    }

    public static IList<T> Shuffle<T>(this IEnumerable<T> sequence, Random randomNumberGenerator)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(randomNumberGenerator);

        var values = sequence.ToList();
        var currentlySelecting = values.Count;
        while (currentlySelecting > 1)
        {
            var selectedElement = randomNumberGenerator.Next(currentlySelecting);
            currentlySelecting--;
            if (currentlySelecting != selectedElement)
                (values[selectedElement], values[currentlySelecting]) = (values[currentlySelecting], values[selectedElement]);
        }
        return values;
    }

    public sealed class SimilarColumn : IComparable<SimilarColumn>
    {
        private HashCode hash;
        private readonly List<int> fields = [];
        private readonly MinHash sim = new(16384);

        public SimilarColumn Add<T>(T value) where T : notnull
        {
            hash.Add(value);
            fields.Add(value.GetHashCode());
            if (fields.Count < 512)
                sim.Update(value.ToString()!);
            return this;
        }

        public double Similarity(SimilarColumn other)
        {
            if (fields.Count != other.fields.Count)
                return 0;

            if (fields.Count == 0)
                return 1;

            var totalSimilarity = fields.Zip(other.fields).Count((v) => v.First == v.Second) / (double)fields.Count;
            var sampleSimilarity = Math.Max(sim.Jaccard(other.sim), totalSimilarity);
            return (totalSimilarity + sampleSimilarity) / 2;
        }

        public (List<int> Columns, double Score) FindBest(SimilarColumn[] columns, double threshold = 0, HashSet<int>? matchedColumns = null)
        {
            List<int> bestColumn = [];
            var bestScore = 0.0;
            for (var i = columns.Length - 1; i >= 0; i--)
            {
                if (matchedColumns?.Contains(i) ?? false)
                    continue;
                var score = Similarity(columns[i]);
                if (score >= threshold)
                {
                    if (score == bestScore)
                        bestColumn.Add(i);
                    else if (score > bestScore)
                    {
                        bestColumn.Clear();
                        bestColumn.Add(i);
                        bestScore = score;
                    }
                }
            }
            return (bestColumn, bestScore);
        }

        public int CompareTo(SimilarColumn? other) =>
            hash.ToHashCode().CompareTo(other?.hash.ToHashCode());
    }
}