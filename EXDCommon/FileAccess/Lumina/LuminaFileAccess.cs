using EXDCommon.FileAccess.Directory;
using EXDCommon.Sheets;
using Lumina;
using Lumina.Data;
using Lumina.Data.Files.Excel;

namespace EXDCommon.FileAccess.Lumina;

public class LuminaFileAccess : IGameFileAccess
{
	private readonly GameData _gameData;
	
	public LuminaFileAccess(GameData gameData)
	{
		_gameData = gameData;
	}

	public T? GetFile<T>(string path, string? origPath = null) where T : FileResource => _gameData.GetFile<T>(path);
	public bool FileExists(string path) => _gameData.FileExists(path);

	public RawExcelSheet? GetRawExcelSheet(string sheetName, Language sheetLanguage = Language.English)
	{
		var path = $"exd/{sheetName}.exh";
		var headerFile = GetFile<ExcelHeaderFile>(path);

		if (headerFile == null)
		{
			return null;
		}

		var newSheet = new RawExcelSheet(headerFile, sheetName, sheetLanguage, this);
		newSheet.GenerateFilePages();

		return newSheet;
	}
	
	public GameVersion GetVersion()
	{
		var gamePath = _gameData.DataPath.Parent;
		var file = gamePath!.FullName + "\\ffxivgame.ver";
		var text = File.ReadAllText(file);
		var version = GameVersion.Parse(text);
		return version;
	}
}