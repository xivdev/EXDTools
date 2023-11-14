using System.Diagnostics;
using EXDCommon.FileAccess;
using EXDCommon.SchemaModel.EXDSchema;
using EXDCommon.Utility;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs.Excel;

namespace SchemaValidator.Validation.Validators;

public class ArrayTypingValidator : Validator
{
	public override string ValidatorName() => "ArrayTypingValidator";
	
	public ArrayTypingValidator(IGameFileAccess gameData) : base(gameData) { }

	public override ValidationResults Validate(ExcelHeaderFile exh, Sheet sheet)
	{
		if (sheet.Name == "SpecialShop")
			Debugger.Break();
		var results = new ValidationResults();
		var fields = SchemaUtil.Flatten(exh, sheet, true);

		var grouped = fields.GroupBy(f => f.Field.Name);
		foreach (var group in grouped)
		{
			ExcelColumnDataType? baseType = null;
			foreach (var column in group)
			{
				if (baseType == null)
				{
					baseType = column.Definition.Type;
				}
				else if (baseType != column.Definition.Type)
				{
					var msg = $"Column {column.Field.Name}@0x{column.Definition.Offset:X} type {column.Definition.Type} is not valid for its array. Expected: '{baseType}', actual: '{column.Definition.Type}'.";
					results.Results.Add(ValidationResult.Error(sheet.Name, ValidatorName(), msg));
				}
			}
		}
		
		if (results.Results.Count == 0)
			return ValidationResults.Success(sheet.Name, ValidatorName());
		return results;
	}
}