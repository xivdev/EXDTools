using System.Numerics;
using EXDCommon.FileAccess;
using EXDCommon.SchemaModel.EXDSchema;
using EXDCommon.Sheets;
using EXDCommon.Utility;
using Lumina.Data.Structs.Excel;
using Lumina.Text;
using Newtonsoft.Json;

namespace SchemaUpdater;

public class SchemaUpdater
{
    private const float COL_SIM_PERCENTAGE = 0.98f;
    private const float COL_DIST_PERCENTAGE = 1f - COL_SIM_PERCENTAGE;
    private const float NEW_INDEX_THRESHOLD = 0.65f;
    private const float SAME_COL_THRESHOLD = 0.65f;
    private const uint INVALID_INDEX = uint.MaxValue;
    private const int DISTANCE_THRESHOLD = 50;

    private readonly IGameFileAccess _oldGameData;
    private readonly IGameFileAccess _newGameData;
    private readonly string _schemaDirectory;
    public HashSet<string> QuestionableSheets { get; init; }

    public SchemaUpdater(IGameFileAccess oldAccess, IGameFileAccess newAccess, string schemaDirectory)
    {
        _oldGameData = oldAccess;
        _newGameData = newAccess;
        _schemaDirectory = schemaDirectory;
        
        QuestionableSheets = new HashSet< string >();
    }

    public bool DefinitionExists(string name)
    {
        return File.Exists(Path.Combine(_schemaDirectory, $"{name}.yml"));
    }

    private static Type ExcelTypeToManaged(ExcelColumnDataType type)
    {
        return type switch
        {
            ExcelColumnDataType.String => typeof(SeString),
            ExcelColumnDataType.Bool => typeof(bool),
            ExcelColumnDataType.Int8 => typeof(sbyte),
            ExcelColumnDataType.UInt8 => typeof(byte),
            ExcelColumnDataType.Int16 => typeof(short),
            ExcelColumnDataType.UInt16 => typeof(ushort),
            ExcelColumnDataType.Int32 => typeof(int),
            ExcelColumnDataType.UInt32 => typeof(uint),
            ExcelColumnDataType.Float32 => typeof(float),
            ExcelColumnDataType.Int64 => typeof(long),
            ExcelColumnDataType.UInt64 => typeof(ulong),
            ExcelColumnDataType.PackedBool0 => typeof(bool),
            ExcelColumnDataType.PackedBool1 => typeof(bool),
            ExcelColumnDataType.PackedBool2 => typeof(bool),
            ExcelColumnDataType.PackedBool3 => typeof(bool),
            ExcelColumnDataType.PackedBool4 => typeof(bool),
            ExcelColumnDataType.PackedBool5 => typeof(bool),
            ExcelColumnDataType.PackedBool6 => typeof(bool),
            ExcelColumnDataType.PackedBool7 => typeof(bool),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static BigInteger ToBi(object o) => o switch
    {
        sbyte x => new BigInteger(x),
        byte x => new BigInteger(x),
        ushort x => new BigInteger(x),
        short x => new BigInteger(x),
        uint x => new BigInteger(x),
        int x => new BigInteger(x),
        ulong x => new BigInteger(x),
        long x => new BigInteger(x),
        // We don't actually care about the value of the floats so we
        // can transform them, as long as we *always* transform them...
        float x => new BigInteger( x * 100 ),
        double x => new BigInteger( x * 100 ),
        _ => throw new NotImplementedException(),
    };

    private static bool IsNumeric(Type t)
    {
        return 
            t == typeof(sbyte) ||
            t == typeof(byte) ||
            t == typeof(short) ||
            t == typeof(ushort) ||
            t == typeof(int) ||
            t == typeof(uint) ||
            t == typeof(long) ||
            t == typeof(ulong) ||
            t == typeof(float) ||
            t == typeof(double);
    }
        
    private bool IsPossiblySameColumn(ExcelSheetImpl oldSheet, uint oldColumnIdx, ExcelSheetImpl newSheet, uint newColumnIdx)
    {
        var oldType = oldSheet.Columns[oldColumnIdx].Type;
        var newType = newSheet.Columns[newColumnIdx].Type;

        var oldMgd = ExcelTypeToManaged(oldType);
        var newMgd = ExcelTypeToManaged(newType);
            
        if (IsNumeric(oldMgd) != IsNumeric(newMgd))
            return false;

        if ((oldMgd == typeof(bool) || newMgd == typeof (bool)) && oldMgd != newMgd)
            return false;
            
        if ((oldMgd == typeof(SeString) || newMgd == typeof (SeString)) && oldMgd != newMgd)
            return false;
        return true;
    }

    private int SeStringSim(SeString one, SeString two)
    {
        return DamerauLevenshteinDistance(one.RawData.ToArray(), two.RawData.ToArray(), DISTANCE_THRESHOLD);
    }
        
    private float Compare(object o1, object o2)
    {
        // Console.WriteLine($"values {o1} and {o2}...");
            
        if (IsNumeric(o1.GetType()) && IsNumeric(o2.GetType()))
        {
            try
            {
                var l1 = ToBi(o1);
                var l2 = ToBi(o2);

                var equals = l1.Equals(l2);
                var zero = l1.Equals(0);

                if (equals && !zero)
                    return 1f;
                // if( equals )
                // return 0.33f;
            } catch (NotImplementedException) {}
        }

        if (o1.Equals(o2))
        {
            return 1f;
        }

        if (o1 is SeString oldStr && o2 is SeString newStr)
        {
            return (1f / DISTANCE_THRESHOLD) * (DISTANCE_THRESHOLD - SeStringSim(oldStr, newStr));
        }
            
        // Console.WriteLine($"values {o1} and {o2} do not match...");
        return 0f;
    }

    private float SimilarityCalc(ExcelSheetImpl oldSheet, uint oldColumnIdx, ExcelSheetImpl newSheet, uint newColumnIdx)
    {
        // Fail faster if these can't even possibly be the same column
        if (!IsPossiblySameColumn(oldSheet, oldColumnIdx, newSheet, newColumnIdx))
            return 0f;
            
        float sim = 0f;

        uint startIdx = oldSheet.DataPages[0].StartId;
        uint rowsProcessed = 0;

        for(uint rowIdx = startIdx; rowsProcessed < oldSheet.RowCount; rowIdx++)
        {
            var oldParser = oldSheet.GetRowParser(rowIdx);
            var newParser = newSheet.GetRowParser(rowIdx);
                
            // Some sheets with subrows actually skip row numbers...
            if (oldParser == null || newParser == null)
            {
                // If the old parser had a row, we should count this row 
                if (oldParser == null) 
                    rowsProcessed++;
                continue;
            }
            
            if (oldParser.RowCount == 1)
            {
                var oldValue = oldParser.ReadColumnRaw((int) oldColumnIdx);
                var newValue = newParser.ReadColumnRaw((int) newColumnIdx);
                sim += Compare(oldValue, newValue);
                rowsProcessed++;
            }
            else
            {
                uint subRowIdx = 0;
                for (; subRowIdx < oldParser.RowCount && subRowIdx < newParser.RowCount; subRowIdx++)
                {
                    oldParser.SeekToRow(rowIdx, subRowIdx);
                    newParser.SeekToRow(rowIdx, subRowIdx);
                    var oldValue = oldParser.ReadColumnRaw((int) oldColumnIdx);
                    var newValue = newParser.ReadColumnRaw((int) newColumnIdx);
                    sim += Compare(oldValue, newValue);
                    rowsProcessed++;
                }

                // Only add if we skipped counts that existed in the old parser
                // and do not exist in the new one, because the old parser is the
                // basis for the max similarity count
                if (subRowIdx < oldParser.RowCount)
                    rowsProcessed += oldParser.RowCount - subRowIdx;
                // if( subRowIdx < newParser.RowCount )
                // rowsProcessed += oldParser.RowCount - subRowIdx;
            }
        }

        return sim;
    }

    private float GetSimilarity(ExcelSheetImpl oldSheet, uint oldColumnIdx, ExcelSheetImpl newSheet, uint newColumnIdx)
    {
        var maxSim = SimilarityCalc(oldSheet, oldColumnIdx, oldSheet, oldColumnIdx);
        var sim = SimilarityCalc(oldSheet, oldColumnIdx, newSheet, newColumnIdx);
        
        return sim / maxSim;
    }

    private uint FindNewIndex(ExcelSheetImpl oldSheet, uint oldColumnIdx, ExcelSheetImpl newSheet, Dictionary<uint, uint>.ValueCollection mappedSheets)
    {
        float bestCalc = 0f;
        uint bestIdx = INVALID_INDEX;
        uint retIdx = INVALID_INDEX;

        for (uint i = 0; i < newSheet.ColumnCount; i++)
        {
            if (mappedSheets.Contains(i)) continue;
            var simCalc = GetSimilarity(oldSheet, oldColumnIdx, newSheet, i);
            var distCalc = i == oldColumnIdx ? 1f : 1f - Math.Abs((int) oldColumnIdx - (int) i) / (float) newSheet.ColumnCount;
            var tmpCalc = (simCalc * COL_SIM_PERCENTAGE) + (distCalc * COL_DIST_PERCENTAGE);

            if (tmpCalc > bestCalc)
            {
                bestCalc = tmpCalc;
                bestIdx = i;
            }
                
            // Console.WriteLine($"\t\t\tcolumn {i} has sim {simCalc:F} dist {distCalc:F} calc {tmpCalc:F}, best is {bestCalc:F} @ {bestIdx}");
        }

        if (bestCalc > NEW_INDEX_THRESHOLD)
            retIdx = bestIdx;
                
        // if (retIdx == INVALID_INDEX)
        //     Console.WriteLine($"\t\t\t\t[WARNING] failed on {oldSheet.Name} col {oldColumnIdx}, highest match was {bestCalc:F} @ {bestIdx}");
        return retIdx;
    }

    public string? ProcessDefinition(string name)
    {
        if (!DefinitionExists(name))
        {
            // TODO: Generate - BasicSchemaGenerator has support for this 
            return null;
        }

        var path = Path.Combine(_schemaDirectory, $"{name}.yml");
        var def = File.ReadAllText(path);
        
        var oldSheet = _oldGameData.GetRawExcelSheet(name, true);
        if (oldSheet == null)
        {
            Console.WriteLine($"--- sheet {name} has no file in the old gamever! ---");
            return null;
        }
        
        var newSheet = _newGameData.GetRawExcelSheet(name, true);
        if (newSheet == null)
        {
            Console.WriteLine($"--- sheet {name} no longer exists! ---");
            return null;
        }
        
        var oldHash = oldSheet.HeaderFile.GetColumnsHash();
        var newHash = newSheet.HeaderFile.GetColumnsHash();

        if (oldHash == newHash)
        {
            Console.WriteLine($"--- sheet {name} is unchanged! ---");
            return def;
        }
        
        var oldSchema = SerializeUtil.Deserialize<Sheet>(def);
        var newSchema = new Sheet { Name = oldSchema.Name, DisplayField = oldSchema.DisplayField, Fields = new List<Field>() };

        if (oldSchema is null || newSchema is null)
        {
            Console.WriteLine($"--- sheet {name} failed to deserialize! ---");
            return null;
        }

        Console.WriteLine($"processing {name}");
        // var columnMap = GenerateColumnMap(oldSheet, newSheet);
        var columnMap = LoadColumnMap(name, @"C:\Users\Liam\Documents\repos\EXDTools\SchemaUpdater\Mappings.json");
        if (columnMap.Count == 0)
        {
            Console.WriteLine($"--- sheet {name} failed to generate column map! ---");
            return null;
        }

        var createdSchema = GenerateNewSchema(oldSchema, newSchema, columnMap);

        return "";
    }
    
    private Sheet GenerateNewSchema(Sheet oldSchema, Sheet newSchema, Dictionary<int, int> columnMap)
    {
        var flattenedOldSchema = SchemaUtil.Flatten(oldSchema);
        
    }

    // private Dictionary<int, int> LoadColumnMap(string sheet, string mapFile)
    // {
    //     var json = File.ReadAllText(mapFile);
    //     var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, int>>>(json);
    //     return data[sheet];
    // }

    private Dictionary<uint, uint> GenerateColumnMap(RawExcelSheet oldSheet, RawExcelSheet newSheet) 
    {
        var newColumnIndices = new Dictionary<uint, uint>();
        
        // By maintaining a shift value based on remapped columns, we can more accurately predict
        // the new column index that should match the current column's contents
        int currentShift = 0;
        
        // Console.WriteLine($"sheet {oldSheet.Name}");

        uint columnIndex = 0;
        foreach (var column in oldSheet.Columns)
        {
            uint columnIdx = columnIndex++;
            
            // We know the previous column's shift - let's try to start looking there, then
            uint firstCheckIdx = columnIdx;
            uint possibleFirstCheckIdx = (uint)((int)columnIdx + currentShift);
            if (possibleFirstCheckIdx < newSheet.ColumnCount)
                firstCheckIdx = possibleFirstCheckIdx;
                
            // Console.WriteLine($"\tfirst check column is {firstCheckIdx} as shift is {currentShift}!");

            var similarity = 0f;

            if(newSheet.ColumnCount > firstCheckIdx)
                similarity = GetSimilarity(oldSheet, columnIdx, newSheet, firstCheckIdx);
                
            // Console.WriteLine($"\tcol {columnIdx} match with col {firstCheckIdx} pct {similarity:F}");

            if (similarity < SAME_COL_THRESHOLD || (similarity > SAME_COL_THRESHOLD && newColumnIndices.ContainsValue(firstCheckIdx)))
            {
                // if (newColumnIndices.ContainsValue(firstCheckIdx))
                //     Console.WriteLine( $"\t\tsimilarity past threshold but the column was already assigned, finding new index for column {columnIdx} from old sheet..." );
                // else
                //     Console.WriteLine($"\t\tsimilarity was below threshold {SAME_COL_THRESHOLD}, finding new index for column {columnIdx} from old sheet..." );
                    
                var newIdx = FindNewIndex(oldSheet, columnIdx, newSheet, newColumnIndices.Values);
                newColumnIndices[columnIdx] = newIdx;
                if (newIdx == INVALID_INDEX)
                {
                    QuestionableSheets.Add(oldSheet.Name);
                    // Console.WriteLine( $"\t\t\t[WARNING] failed to find named column {columnIdx}! maybe it was removed or a new column was added in its place? you may have to adjust links manually!" );
                }
                else
                {
                    // Console.WriteLine($"\t\tnew index for {columnIdx} is {newIdx}");
                        
                    // Only set current shift if we found a candidate index
                    currentShift = (int) newIdx - (int) columnIdx;
                }
            }
            else
            {
                newColumnIndices[columnIdx] = firstCheckIdx;
            }
            
            // if (newColumnIndices[columnIdx] != INVALID_INDEX)
            //     Console.WriteLine($"\t\tnew column is saved as {newColumnIndices[columnIdx]}");
            // else
            //     Console.WriteLine("\t\tcolumn has no match so it wasn't added");
        }

        Console.WriteLine($"new indices for {oldSheet.Name}:");
        Console.WriteLine(JsonConvert.SerializeObject(newColumnIndices));
        // foreach(var key in newColumnIndices.Keys)
            // Console.Write($"({key}: {newColumnIndices[key]})");
        Console.WriteLine();
        return newColumnIndices;
    }
    
#region stackoverflow
    // https://stackoverflow.com/a/9454016
    private static int DamerauLevenshteinDistance(byte[] source, byte[] target, int threshold) {

        int length1 = source.Length;
        int length2 = target.Length;

        if (length1 == 0)
            return threshold;
        if (length2 == 0)
            return threshold;

        // Return trivial case - difference in string lengths exceeds threshhold
        if (Math.Abs(length1 - length2) > threshold) { return threshold; }

        // Ensure arrays [i] / length1 use shorter length 
        if (length1 > length2)
        {
            (target, source) = (source, target);
            (length1, length2) = (length2, length1);
        }

        int maxi = length1;
        int maxj = length2;

        int[] dCurrent = new int[maxi + 1];
        int[] dMinus1 = new int[maxi + 1];
        int[] dMinus2 = new int[maxi + 1];
        int[] dSwap;

        for (int i = 0; i <= maxi; i++) { dCurrent[i] = i; }

        int jm1 = 0, im1 = 0, im2 = -1;

        for (int j = 1; j <= maxj; j++) {

            // Rotate
            dSwap = dMinus2;
            dMinus2 = dMinus1;
            dMinus1 = dCurrent;
            dCurrent = dSwap;

            // Initialize
            int minDistance = int.MaxValue;
            dCurrent[0] = j;
            im1 = 0;
            im2 = -1;

            for (int i = 1; i <= maxi; i++) {

                int cost = source[im1] == target[jm1] ? 0 : 1;

                int del = dCurrent[im1] + 1;
                int ins = dMinus1[i] + 1;
                int sub = dMinus1[im1] + cost;

                //Fastest execution for min value of 3 integers
                int min = (del > ins) ? (ins > sub ? sub : ins) : (del > sub ? sub : del);

                if (i > 1 && j > 1 && source[im2] == target[jm1] && source[im1] == target[j - 2])
                    min = Math.Min(min, dMinus2[im2] + cost);

                dCurrent[i] = min;
                if (min < minDistance) { minDistance = min; }
                im1++;
                im2++;
            }
            jm1++;
            if (minDistance > threshold) { return threshold; }
        }

        int result = dCurrent[maxi];
        return (result > threshold) ? threshold : result;
    }
#endregion
}