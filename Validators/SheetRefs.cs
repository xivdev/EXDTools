using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;

namespace EXDTooler.Validators;

public sealed class SheetRefs : IValidator<SheetRefs>
{
    private SheetRefs() { }

    public static void Validate(Sheet sheet, ExcelHeaderFile header, GameData data)
    {
        HashSet<string> sheets = [];
        foreach (var f in sheet.Fields)
            sheets.UnionWith(GetSheetRefs(f));

        foreach (var sheetName in sheets)
        {
            if (!data.FileExists($"exd/{sheetName}.exh"))
                throw new ValidationException($"Sheet reference not found: {sheetName}");
        }
    }

    private static HashSet<string> GetSheetRefs(Field field)
    {
        if (field.Type == FieldType.Link)
        {
            if (field.Targets != null)
            {
                if (field.Targets.Count == 0)
                    throw new ValidationException("Empty link targets");

                if (field.Targets.Distinct().Count() != field.Targets.Count)
                    throw new ValidationException("Duplicate link targets");

                return [.. field.Targets];
            }
            else
            {
                HashSet<string> ret = [];
                foreach (var condition in field.Condition!.Cases!.Values)
                {
                    if (condition.Count == 0)
                        throw new ValidationException("Empty link condition");

                    if (condition.Distinct().Count() != condition.Count)
                        throw new ValidationException("Duplicate link condition");

                    ret.UnionWith(condition);
                }
                return ret;
            }
        }
        else if (field.Type == FieldType.Array && field.Fields != null)
        {
            HashSet<string> ret = [];
            foreach (var f in field.Fields!)
                ret.UnionWith(GetSheetRefs(f));
            return ret;
        }
        return [];
    }
}