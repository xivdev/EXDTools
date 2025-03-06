using EXDTooler.Schema;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.BreakingValidators;

public interface IBreakingValidator<T> where T : IBreakingValidator<T>
{
    abstract static void Validate(Sheet baseSheet, Sheet newSheet, IReadOnlyList<ExcelColumnDefinition> cols, ColDefReader colDefs);
}