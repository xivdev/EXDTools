using System.ComponentModel;
using YamlDotNet.Serialization;

namespace EXDTooler.Schema;

public record Sheet
{
    public required string Name { get; set; }

    public string? DisplayField { get; set; }

    public required List<Field> Fields { get; set; }

    public RelationsCollection? Relations { get; set; }
}

public record Field
{
    [YamlMember(Order = 0)]
    public string? Name { get; set; }

    [DefaultValue(FieldType.Scalar)]
    public FieldType Type { get; set; }

    public int? Count { get; set; }

    public string? Comment { get; set; }

    public List<Field>? Fields { get; set; }

    public RelationsCollection? Relations { get; set; }

    public Condition? Condition { get; set; }

    public SheetTargets? Targets { get; set; }

    public override string ToString()
    {
        return $"{Name} ({Type})";
    }
}

public enum FieldType
{
    Scalar,
    Link,
    Array,
    Icon,
    ModelId,
    Color,
}

public record Condition
{
    public string? Switch { get; set; }

    public Dictionary<int, SheetTargets>? Cases { get; set; }
}

public sealed class RelationsCollection : Dictionary<string, List<string>>
{

}

public sealed class SheetTargets : List<string>
{

}