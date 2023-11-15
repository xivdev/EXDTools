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
		var fields = new List<Field>();
		foreach (var field in sheet.Fields)
			Emit(fields, field);

		sheet.Fields = fields;
		for (int i = 0; i < sheet.Fields.Count; i++)
			sheet.Fields[i].OffsetBasedIndex = (uint)i;
		return sheet;
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

	private static void Emit(List<Field> list, Field field, List<(string, string)> hierarchy = null, string nameOverride = "")
	{
		if (field.Type != FieldType.Array)
		{
			// Single field
			list.Add(CreateField(field, false, 0, hierarchy, nameOverride));
		}
		else if (field.Type == FieldType.Array)
		{
			// We can have an array without fields, it's just scalars
			if (field.Fields == null)
			{
				for (int i = 0; i < field.Count.Value; i++)
				{
					list.Add(CreateField(field, true, i, hierarchy, ""));
				}
			}
			else
			{
				for (int i = 0; i < field.Count.Value; i++)
				{
					foreach (var nestedField in field.Fields)
					{
						var usableHierarchy = hierarchy == null ? new List<(string, string)>() : new List<(string, string)>(hierarchy);
						var hierarchyName = $"{field.Name}";
						var hierarchyName2 = $"{field.Name}[{i}]";
						usableHierarchy.Add((hierarchyName, hierarchyName2));
						Emit(list, nestedField, usableHierarchy, field.Name);
					}	
				}
			}
		}
	}

	private static Field CreateField(Field baseField, bool fieldIsArrayElement, int index, List<(string, string)>? hierarchy, string nameOverride)
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

		if (hierarchy != null)
		{
			addedField.Path = string.Join(".", hierarchy);
			addedField.PathWithArrayIndices = string.Join(".", hierarchy);
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
}