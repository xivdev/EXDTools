using EXDCommon.SchemaModel.EXDSchema;
using Lumina.Data.Files.Excel;

namespace EXDCommon.Utility;

/// <summary>
/// Useful methods for working with the EXDSchema object model.
/// </summary>
public static class SchemaUtil
{
	public static int GetColumnCount(Sheet sheet)
	{
		var total = 0;
		foreach (var field in sheet.Fields)
			total += GetFieldCount(field);
		return total;
	}

	public static Sheet Flatten(Sheet sheet)
	{
		var newSheet = new Sheet { Name = sheet.Name, DisplayField = sheet.DisplayField };
		var fields = new List<Field>();
		
		foreach (var field in sheet.Fields)
			Emit(fields, field);

		newSheet.Fields = fields;
		for (int i = 0; i < newSheet.Fields.Count; i++)
			newSheet.Fields[i].OffsetBasedIndex = (uint)i;
		return newSheet;
	}

	/// <summary>
	/// Flattens a sheet and exh into a single list of DefinedColumns that match the definition to the colum.
	/// </summary>
	/// <param name="exh">The Excel Header containing column definitions.</param>
	/// <param name="sheet">The Sheet object containing field definitions.</param>
	/// <param name="usePath">When true, adds array indices into field names. When false, these indices are omitted.
	/// For example, when usePath is true, a field might be named Item.ReceiveCount, when true, that same field may be
	/// named Item[0].ReceiveCount[1].</param>
	/// <returns>A List of DefinedColumns with Excel columns matched with sheet definitions.</returns>
	public static List<DefinedColumn> Flatten(ExcelHeaderFile exh, Sheet sheet, bool usePath = false)
	{
		var fields = new List<Field>();
		foreach (var field in sheet.Fields)
			Emit(fields, field, null);

		var exhDefList = exh.ColumnDefinitions.ToList();
		exhDefList.Sort((c1, c2) => DefinedColumn.CalculateBitOffset(c1).CompareTo(DefinedColumn.CalculateBitOffset(c2)));
		
		var min = Math.Min(exhDefList.Count, fields.Count);
		
		var definedColumns = new List<DefinedColumn>();
		for(int i = 0; i < min; i++)
		{
			var field = fields[i];
			var def = exhDefList[i];
			definedColumns.Add(new DefinedColumn { Field = field, Definition = def });
		}

		return definedColumns;
	}

	private static void Emit(List<Field> list, Field field, List<string> hierarchy = null, List<string> arrayHierarchy = null, string nameOverride = "")
	{
		if (field.Type != FieldType.Array)
		{
			// Single field
			list.Add(CreateField(field, false, 0, hierarchy, arrayHierarchy, nameOverride));
		}
		else if (field.Type == FieldType.Array)
		{
			// We can have an array without fields, it's just scalars
			if (field.Fields == null)
			{
				for (int i = 0; i < field.Count.Value; i++)
				{
					list.Add(CreateField(field, true, i, hierarchy, arrayHierarchy, ""));
				}
			}
			else
			{
				for (int i = 0; i < field.Count.Value; i++)
				{
					foreach (var nestedField in field.Fields)
					{
						var usableHierarchy = hierarchy == null ? new List<string>() : new List<string>(hierarchy);
						var usableArrayHierarchy = hierarchy == null ? new List<string>() : new List<string>(hierarchy);
						var hierarchyName = $"{field.Name}";
						var arrayHierarchyName = $"{field.Name}[{i}]";
						usableHierarchy.Add(hierarchyName);
						usableArrayHierarchy.Add(arrayHierarchyName);
						Emit(list, nestedField, usableHierarchy, usableArrayHierarchy, field.Name);
					}	
				}
			}
		}
	}

	private static Field CreateField(Field baseField, bool fieldIsArrayElement, int index, List<string>? hierarchy, List<string>? arrayHierarchy, string nameOverride)
	{
		var addedField = new Field
		{
			Name = baseField.Name,
			Comment = baseField.Comment,
			Count = null,
			Type = baseField.Type == FieldType.Array ? FieldType.Scalar : baseField.Type,
			Fields = null,
			Condition = baseField.Condition,
			Targets = baseField.Targets,
		};

		var path = $"{baseField.Name}";
		var path2 = $"{baseField.Name}";
		
		if (fieldIsArrayElement)
		{
			path2 += $"[{index}]";
		}

		if (hierarchy != null && arrayHierarchy != null)
		{
			addedField.Path = string.Join(".", hierarchy);
			addedField.PathWithArrayIndices = string.Join(".", arrayHierarchy);
			if (!string.IsNullOrEmpty(path)) addedField.Path += $".{path}";
			if (!string.IsNullOrEmpty(path)) addedField.PathWithArrayIndices += $".{path2}";
		}
		else
		{
			addedField.Path = path;
			addedField.PathWithArrayIndices = path2;
		}
		
		// This is for unnamed inner fields of arrays such as arrays of links
		// We don't want to override the name of unnamed scalars though
		if (baseField.Name == null && baseField.Type != FieldType.Scalar && nameOverride != "")
			addedField.Name = nameOverride;
		
		return addedField;
	}

	public static int GetFieldCount(Field field)
	{
		if (field.Type == FieldType.Array)
		{
			var total = 0;
			if (field.Fields != null)
			{
				foreach (var nestedField in field.Fields)
					total += GetFieldCount(nestedField);
			}
			else
			{
				total = 1;
			}
			return total * field.Count.Value;
		}
		return 1;
	}

	public static Sheet Unflatten(Sheet sheet)
	{
		var newSheet = new Sheet { Name = sheet.Name, DisplayField = sheet.DisplayField };
		var fields = new List<Field>(sheet.Fields);

		var result = false;
		do
		{
			result = Unflatten(fields);
		} while (!result);

		newSheet.Fields = fields;
		return newSheet;
	}
	
	private static bool Unflatten(List<Field> fields)
	{
		if (!fields.Any()) return false;
		// if (!Unflatten(fields)) return false;

		int start = 0, end = 0;
		var found = false;
		
		for (int i = 0; i < fields.Count - 1; i++)
		{
			var currentFieldPath = "";
			var nextFieldPath = "";

			int j = 0;
			do
			{
				currentFieldPath = fields[i + j].Path;
				nextFieldPath = fields[i + j + 1].Path;
				j++;
			} while (currentFieldPath == nextFieldPath && i + j < fields.Count - 1);

			start = i;
			end = i + j - 1;
			
			// Array (defined by field path) is longer than 1 element
			if (j > 1)
			{
				found = true;
				break;
			}
		}

		if (found)
		{
			Console.WriteLine($"Found array of {fields[start].Name} from {start} to {end} with length {end - start + 1}");
			var fieldStart = fields[start];
			fieldStart = fields[start];
			fieldStart.Type = FieldType.Array;
			fieldStart.Count = end - start + 1;
			// fieldStart.Comment = 
			fields.RemoveRange(start + 1, end - start);
			
			return false;
		}

		return true;
	}
}