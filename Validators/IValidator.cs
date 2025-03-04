using EXDTooler.Schema;
using Lumina;
using Lumina.Data.Files.Excel;

namespace EXDTooler.Validators;

public interface IValidator<T> where T : IValidator<T>
{
    abstract static void Validate(Sheet sheet, ExcelHeaderFile header, GameData data);
}