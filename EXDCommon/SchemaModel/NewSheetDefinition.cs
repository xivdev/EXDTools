// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharpYaml;
using SharpYaml.Serialization;

namespace EXDCommon.SchemaModel.EXDSchema;

public enum FieldType
{
	Scalar,
	Array,
	Icon,
	ModelId,
	Color,
	Link,
}

public class Sheet
{
	[YamlMember(0)]
	public string Name { get; set; }

	[YamlMember(1)]
	public string? DisplayField { get; set; }

	[YamlMember(2)]
	public List<Field> Fields { get; set; }
}

public class Field
{
	[YamlMember(0)]
	public string? Name { get; set; }

	[YamlMember(2)]
	public int? Count { get; set; }

	[YamlMember(1)]
	[DefaultValue(FieldType.Scalar)]
	[JsonConverter(typeof(StringEnumConverter), true)]
	public FieldType Type { get; set; }

	[YamlMember(3)]
	public string? Comment { get; set; }

	[YamlMember(4)]
	public List<Field>? Fields { get; set; }

	[YamlMember(5)]
	public Condition? Condition { get; set; }

	[YamlMember(6)]
	[YamlStyle(YamlStyle.Flow)]
	public List<string>? Targets { get; set; }

	/// <summary>
	/// Useful for determining the order of fields in the sheet.
	/// </summary>
	[YamlIgnore]
	public uint OffsetBasedIndex;

	/// <summary>
	/// Useful for determining the total column count of a specific field.
	/// </summary>
	[YamlIgnore]
	public int FieldCount;

	/// <summary>
	/// Field path in the form "Field.Subfield.Subsubfield"
	/// </summary>
	[YamlIgnore]
	public string Path;
	
	/// <summary>
	/// Field path in the form "Field[0].Subfield[1].Subsubfield[2]"
	/// </summary>
	[YamlIgnore]
	public string PathWithArrayIndices;
	
	public override string ToString()
	{
		return $"{Name} ({Type})";
	}
}

public class Condition
{
	[YamlMember(0)]
	public string? Switch { get; set; }

	[YamlMember(1)]
	public Dictionary<int, List<string>>? Cases { get; set; }
}