using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;

namespace EXDTooler.BreakingValidators;

public interface IBreakingValidator<T> where T : IBreakingValidator<T>
{
    abstract static void Validate(Sheet baseSheet, Sheet newSheet, ExcelHeaderFile header, GameData data);
}