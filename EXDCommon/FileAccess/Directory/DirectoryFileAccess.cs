using System.Reflection;
using EXDCommon.Sheets;
using Lumina.Data;
using Lumina.Data.Files.Excel;

namespace EXDCommon.FileAccess.Directory;

public class DirectoryFileAccess : IGameFileAccess
{
	private readonly string _storagePath;
	private readonly PatchDataDirectory _directory;
	private readonly Dictionary<uint, IndexFile> _indexFiles;
	private readonly Dictionary<string, RawExcelSheet> _sheets;

	public DirectoryFileAccess(PatchDataDirectory directory, string storagePath)
	{
		_directory = directory;
		_storagePath = storagePath;
		_indexFiles = new Dictionary<uint, IndexFile>();
		_sheets = new Dictionary<string, RawExcelSheet>();
	}

	public T? GetFile<T>(string path, string? origPath = null) where T : FileResource
	{
		if (!_directory.SqPackFiles.TryGetValue(path, out var sqpackFile)) return null;
		var realPath = Path.Combine(_storagePath, sqpackFile.Hash[..2], sqpackFile.Hash);
		
		if(!File.Exists(realPath))
		{
			throw new FileNotFoundException($"the file at the path '{realPath}' doesn't exist");
		}

		var fileContent = File.ReadAllBytes(realPath);

		var file = Activator.CreateInstance<T>();
        
		var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        
		var dataProperty = file.GetType().BaseType.GetProperty("Data", bindingFlags);
		dataProperty.SetValue(file, fileContent);
        
		if( origPath != null )
		{
			var filePathProperty = file.GetType().BaseType.GetProperty("FilePath", bindingFlags);
			filePathProperty.SetValue(file, fileContent);
		}
        
		var readerProperty = file.GetType().BaseType.GetProperty("Reader", bindingFlags);
		readerProperty.SetValue(file, new LuminaBinaryReader(file.Data));
        
		file.LoadFile();

		return file;
	}

	public RawExcelSheet? GetRawExcelSheet(string sheetName, Language sheetLanguage = Language.English)
	{
		if (_sheets.TryGetValue(sheetName, out var sheet))
		{
			return sheet;
		}
		
		var path = $"exd/{sheetName}.exh";
		var headerFile = GetFile<ExcelHeaderFile>(path);

		if (headerFile == null)
		{
			return null;
		}

		var newSheet = new RawExcelSheet(headerFile, sheetName, sheetLanguage, this);
		newSheet.GenerateFilePages();
		_sheets[sheetName] = newSheet;
		
		return newSheet;
	}

	public bool FileExists(string path)
	{
		var category = PathUtil.GetCategoryIdForPath(path);

		if (!_indexFiles.ContainsKey(category))
		{
			var indexHash = _directory.IndexFiles[category];
			using var exdIndexStream = new System.IO.FileInfo(Path.Combine(_storagePath, indexHash[..2], indexHash)).OpenRead();
			_indexFiles[category] = IndexFile.FromStream(exdIndexStream, category);
		}

		return _indexFiles[category].GetFileOffsetAndDat(path) != (0, 0);
	}

	public GameVersion GetVersion()
	{
		return _directory.Version;
	}
}