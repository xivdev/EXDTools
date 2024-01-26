using EXDCommon.Sheets;
using Lumina.Data;

namespace EXDCommon.FileAccess;

public interface IGameFileAccess
{
	T? GetFile<T>(string path, string? origPath = null) where T : FileResource;
	RawExcelSheet? GetRawExcelSheet(string sheetName, bool sortByOffset = false, Language sheetLanguage = Language.English);
	bool FileExists(string path);
}