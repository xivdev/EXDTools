using EXDCommon.FileAccess.Directory;
using EXDCommon.Sheets;
using Lumina.Data;

namespace EXDCommon.FileAccess;

public interface IGameFileAccess
{
	T? GetFile<T>(string path, string? origPath = null) where T : FileResource;
	RawExcelSheet? GetRawExcelSheet(string sheetName, Language sheetLanguage = Language.English);
	bool FileExists(string path);
	GameVersion GetVersion();
}