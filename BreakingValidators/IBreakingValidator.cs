using EXDTooler.Schema;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.BreakingValidators;

[Obsolete("Pending fields are no longer used, but this may be useful in the future")]
public interface IBreakingValidator<T> where T : IBreakingValidator<T>
{
    abstract static void Validate(Sheet baseSheet, Sheet newSheet, IReadOnlyList<ExcelColumnDefinition> cols, ColDefReader colDefs);
}