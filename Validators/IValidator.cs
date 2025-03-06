using EXDTooler.Schema;
using Lumina.Data.Structs.Excel;

namespace EXDTooler.Validators;

public interface IValidator<T> where T : IValidator<T>
{
    abstract static void Validate(Sheet sheet, IReadOnlyList<ExcelColumnDefinition> cols, ColDefReader colDefs);
}