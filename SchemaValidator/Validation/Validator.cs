using EXDCommon;
using EXDCommon.FileAccess;
using EXDCommon.SchemaModel.EXDSchema;
using EXDCommon.Sheets;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;

namespace SchemaValidator.Validation;

public abstract class Validator
{
	protected IGameFileAccess GameData;
	
	public Validator(IGameFileAccess gameData)
	{
		GameData = gameData;
	}
	
	public abstract string ValidatorName();
	public abstract ValidationResults Validate(ExcelHeaderFile exh, Sheet sheet);
	
	protected long? ReadColumnIntegerValue(RawExcelSheet sheet, RowParser parser, DefinedColumn column)
	{
		var offset = column.Definition.Offset;
		var type = column.Definition.Type;
		Int128? value = type switch
		{
			ExcelColumnDataType.Int8 => parser.ReadOffset<sbyte>(offset),
			ExcelColumnDataType.UInt8 => parser.ReadOffset<byte>(offset),
			ExcelColumnDataType.Int16 => parser.ReadOffset<short>(offset),
			ExcelColumnDataType.UInt16 => parser.ReadOffset<ushort>(offset),
			ExcelColumnDataType.Int32 => parser.ReadOffset<int>(offset),
			ExcelColumnDataType.UInt32 => parser.ReadOffset<uint>(offset),
			ExcelColumnDataType.Int64 => parser.ReadOffset<long>(offset),
			ExcelColumnDataType.UInt64 => parser.ReadOffset<ulong>(offset),
			_ => null,
		};

		if (value != null)
			return (long)value;
		return null;
	}
}