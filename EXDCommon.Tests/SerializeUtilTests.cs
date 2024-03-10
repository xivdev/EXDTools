using EXDCommon.SchemaModel.EXDSchema;
using EXDCommon.Utility;

namespace EXDCommon.Tests;

public class Tests
{
	[Test]
	public void TestDeserializeSheet()
	{
		var content = File.ReadAllText("Data/Schemas/Aetheryte.yml");
		var sheet = SerializeUtil.Deserialize<Sheet>(content);
        Assert.That(sheet, Is.Not.Null);
	}
	
	// [Test]
	// public void TestReserializeObjectBased()
	// {
	// 	var content = File.ReadAllText("Data/Schemas/Aetheryte.yml");
	// 	var sheet = SerializeUtil.Deserialize<Sheet>(content);
	// 	var content2 = SerializeUtil.Serialize(sheet);
	// 	var sheet2 = SerializeUtil.Deserialize<Sheet>(content2);
	// 	Assert.That(sheet, Is.EqualTo(sheet2));
	// }
	
	[Test]
	public void TestReserializeTextBased()
	{
		var content = File.ReadAllText("Data/Schemas/Aetheryte.yml");
		var sheet = SerializeUtil.Deserialize<Sheet>(content);
		var content2 = SerializeUtil.Serialize(sheet);
		Assert.That(content2, Is.EqualTo(content));
	}

	[Test]
	public void TestFlatten()
	{
		var content = File.ReadAllText("Data/Schemas/Aetheryte.yml");
		var sheet = SerializeUtil.Deserialize<Sheet>(content);
		var flattenedSheet = SchemaUtil.Flatten(sheet);
		var count = SchemaUtil.GetColumnCount(sheet);
		Assert.That(flattenedSheet.Fields, Has.Count.EqualTo(count));
	}
	
	// [Test]
	// public void TestUnflattenObjectBased()
	// {
	// 	var content = File.ReadAllText("Data/Schemas/AnimationLOD.yml");
	// 	var sheet = SerializeUtil.Deserialize<Sheet>(content);
	// 	var flattenedSheet = SchemaUtil.Flatten(sheet);
	// 	var unflattenedSheet = SchemaUtil.Unflatten(flattenedSheet);
	// 	Assert.That(sheet, Is.EqualTo(unflattenedSheet));
	// }
	
	// [TestCase("AnimationLOD")]
	[TestCase("Aetheryte")]
	public void TestUnflattenTextBased(string sheetName)
	{
		var content = File.ReadAllText($"Data/Schemas/{sheetName}.yml");
		var sheet = SerializeUtil.Deserialize<Sheet>(content);
		var flattenedSheet = SchemaUtil.Flatten(sheet);
		var unflattenedSheet = SchemaUtil.Unflatten(flattenedSheet);
		var content2 = SerializeUtil.Serialize(unflattenedSheet);
		Assert.That(content2, Is.EqualTo(content));
	}
}