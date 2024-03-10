using Lumina.Data.Structs.Excel;

namespace SchemaConverter;

public static class Util
{
	public static string FirstCharToUpper(this string input) =>
		input switch
		{
			null => throw new ArgumentNullException(nameof(input)),
			"" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
			_ => string.Concat(input[0].ToString().ToUpper(), input.AsSpan(1))
		};
	
	public static string StripDefinitionName(string str) 
	{
		if( string.IsNullOrWhiteSpace(str))
			return null;

		str = str
			.Replace("<", "")
			.Replace(">", "")
			.Replace("{", "")
			.Replace("}", "")
			.Replace("(", "")
			.Replace(")", "")
			.Replace("/", "")
			.Replace("[", "")
			.Replace("]", "")
			.Replace(" ", "")
			.Replace("'", "")
			.Replace("-", "")
			.Replace("%", "Pct");

		if(char.IsDigit(str[0]))
		{
			var index = str[0] - '0';
			var words = new[] {"zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"};

			str = $"{words[index]}{str[1..]}";
		}

		return str;
	}
	
	public static int GetBitOffset(int offset, ExcelColumnDataType dataType)
	{
		var bitOffset = offset * 8;
		return dataType switch
		{
			ExcelColumnDataType.PackedBool0 => bitOffset + 0,
			ExcelColumnDataType.PackedBool1 => bitOffset + 1,
			ExcelColumnDataType.PackedBool2 => bitOffset + 2,
			ExcelColumnDataType.PackedBool3 => bitOffset + 3,
			ExcelColumnDataType.PackedBool4 => bitOffset + 4,
			ExcelColumnDataType.PackedBool5 => bitOffset + 5,
			ExcelColumnDataType.PackedBool6 => bitOffset + 6,
			ExcelColumnDataType.PackedBool7 => bitOffset + 7,
			_ => bitOffset,
		};
	}
	
	public static bool DictionaryEqual<T, TU>(Dictionary<T, List<TU>> oldDict, Dictionary<T, List<TU>> newDict) where T : notnull
	{
		// Simple check, are the counts the same?
		if (!oldDict.Count.Equals(newDict.Count)) return false;

		// Verify the keys
		if (!oldDict.Keys.SequenceEqual(newDict.Keys)) return false;

		// Verify the values for each key
		foreach (var key in oldDict.Keys)
			if (!oldDict[key].SequenceEqual(newDict[key]))
				return false;

		return true;
	}
}