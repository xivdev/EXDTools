using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Data.Files.Excel;
using Lumina.Data.Structs;
using ZiPatchLib;
using ZiPatchLib.Chunk;
using ZiPatchLib.Chunk.SqpkCommand;

namespace EXDWorker;

public class EXDExtractor
{
    private readonly Dictionary<string, SparseDataCollection> _datStreams = new();
    private readonly Dictionary<uint, SparseDataCollection> _fileDatStreams = new();
    private readonly Dictionary<uint, SparseDataCollection> _fileIndexStreams = new();

    private Dictionary<uint, IndexFile> _indexFiles = new();
    
    private PatchDataDirectory _directory;
    private PatchDataDirectory? _previousDirectory;
    private List<PatchDataDirectory>? _previousDirectories;
    
    private string _fileStoragePath;
    
    private bool _used;
    private Stopwatch _time;

    private void ParseChunk(SqpkAddData chunk)
    {
        chunk.TargetFile.ResolvePath(ZiPatchConfig.PlatformId.Win32);
        if (!_datStreams.TryGetValue(chunk.TargetFile.ToString(), out var stream))
        {
            Debug.WriteLine($"New sparse entry for {chunk.TargetFile}");
            stream = new SparseDataCollection();
            _datStreams.Add(chunk.TargetFile.ToString(), stream);
        }

        stream.Position = chunk.BlockOffset;
        stream.Write(chunk.BlockData, 0, chunk.BlockData.Length);
    }

    private void ParseChunk(SqpkFile chunk)
    {
        if (chunk.TargetFile.RelativePath.StartsWith("movie")) return;
        var isDat = chunk.TargetFile.RelativePath.Contains(".dat");
        var isIndex = chunk.TargetFile.RelativePath.Contains(".index");
        if (chunk.TargetFile.RelativePath.Contains(".index2")) return;
        if (!isDat && !isIndex) return;

        var tmp = chunk.TargetFile.RelativePath;
        var lastSlash = tmp.LastIndexOf('/');
        var win32Index = tmp.IndexOf(".win32");
        var idStr = tmp[(lastSlash + 1)..win32Index];
        var id = uint.Parse(idStr, NumberStyles.HexNumber);
        
        if (isDat)
        {
            if (!_fileDatStreams.TryGetValue(id, out var stream))
            {
                Debug.WriteLine($"New sparse entry for {chunk.TargetFile}");
                stream = new SparseDataCollection();
                _fileDatStreams.Add(id, stream);
            }

            stream.Position = chunk.FileOffset;
            foreach (var data in chunk.CompressedData)
                data.DecompressInto(stream);
        }
        else if (isIndex)
        {
            if (!_fileIndexStreams.TryGetValue(id, out var stream))
            {
                Debug.WriteLine($"New sparse entry for {chunk.TargetFile}");
                stream = new SparseDataCollection();
                _fileIndexStreams.Add(id, stream);
            }

            stream.Position = chunk.FileOffset;
            foreach (var data in chunk.CompressedData)
                data.DecompressInto(stream);
        }
    }

    public void Process(List<string> patchFilePaths, string fileStoragePath, string outputDirectory)
    {
        if (_used) throw new Exception("This instance has already been used.");
        _used = true;
        _time = Stopwatch.StartNew();
        
        // Housekeeping
        if (!patchFilePaths.Any()) return;
        if (patchFilePaths.Any(path => Path.GetFileNameWithoutExtension(path).StartsWith("H"))) return;
        
        var gameVersion = patchFilePaths.Select(path => GameVersion.Parse(Path.GetFileNameWithoutExtension(path)[1..])).Max()!;

        var outputFileName = Path.Combine(outputDirectory, $"{gameVersion}.json");
        if (File.Exists(outputFileName)) return;
        
        _directory = new PatchDataDirectory(gameVersion);
        _fileStoragePath = fileStoragePath;
        
        var patchChunks = new List<ZiPatchChunk>();
        
        Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Ready for {patchFilePaths.Count} patch files known as {gameVersion}.");
        
        // Extract patch chunks
        foreach (var patchFilePath in patchFilePaths)
        {
            using (var patchFile = ZiPatchFile.FromFileName(patchFilePath))
            {
                foreach (var chunk in patchFile.GetChunks())
                {
                    if (chunk is SqpkAddData addData)
                        ParseChunk(addData);
                    if (chunk is SqpkFile file)
                        ParseChunk(file);    
                }
            }
        }
        
        Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Extracted and processed {patchChunks.Count} chunks from {patchFilePaths.Count} patch files.");
        
        // Extract index files so we can set up our index file dictionary for the given patch
        foreach (var (id, stream) in _fileIndexStreams)
        {
            var hash = HashStream(stream);
            SaveFile(stream, fileStoragePath, hash);
            _directory.RecordIndexFile(id, hash);
        }
        
        Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Processed {_fileIndexStreams.Count} index files.");
        
        LoadPreviousDirectories(outputDirectory);
        
        // If we don't have full index data, look backwards for it
        if (_directory.IndexFiles.Count != 14) // 14 = all but ui script
        {
            var indexWeDontHave = _previousDirectory!.IndexFiles.Keys.Except(_directory.IndexFiles.Keys).ToList();
            foreach (var index in indexWeDontHave)
                _directory.RecordIndexFile(index, _previousDirectory.IndexFiles[index]);
            Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Filled in {indexWeDontHave.Count} missing index files from {_previousDirectory.Version}.");
        }
        
        // Get root.exl to figure out what to extract here
        var rootExlFile = GetSqPackFile<ExcelListFile>("exd/root.exl");
        if (rootExlFile?.TypedResource == null)
        {
            Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Failing processing as root.exl was not found.");
            return;
        }
        
        HandleSqPackFile(rootExlFile, fileStoragePath);
        
        Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Loaded EXD root EXL file. Contains {rootExlFile.TypedResource.ExdMap.Count} entries.");
        
        // Extract EXD files
        foreach (var (name, _) in rootExlFile.TypedResource.ExdMap)
        {
            if (name.Contains("/")) continue;
            var headerFilePath = $"exd/{name}.exh";
            var headerFile = GetSqPackFile<ExcelHeaderFile>(headerFilePath);

            if (headerFile?.TypedResource == null)
            {
                Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Skipped {headerFilePath}, for it was null.");
                continue;
            }
            
            HandleSqPackFile(headerFile, fileStoragePath);
            
            var dataFiles = GenerateDataFiles(name, headerFile.TypedResource);
            foreach (var dataFilePath in dataFiles)
            {
                var dataFile = GetSqPackFile<ExcelDataFile>(dataFilePath);
                if (dataFile == null || dataFile.TypedResource == null)
                {
                    // Console.WriteLine($"[{TimeSpan.FromMilliseconds(time.ElapsedMilliseconds):c}] Skipped {dataFilePath}, for it was null.");
                    continue;
                }
                
                HandleSqPackFile(dataFile, fileStoragePath);
            }
        }
        
        Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Extracted EXD files.");
        
        // Write directory file
        var json = JsonSerializer.Serialize(_directory, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputFileName, json);
        
        Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Wrote directory file.");

        foreach (var sparseDataCollection in _datStreams.Values)
            sparseDataCollection.Dispose();
        foreach (var sparseDataCollection in _fileDatStreams.Values)
            sparseDataCollection.Dispose();
        foreach (var sparseDataCollection in _fileIndexStreams.Values)
            sparseDataCollection.Dispose();
    }

    private static List<string> GenerateDataFiles(string sheetName, ExcelHeaderFile headerFile)
    {
        var pageFiles = new List<string>();
        foreach (var language in Enum.GetValues<Language>())
        {
            foreach( var bp in headerFile.DataPages )
            {
                if( language == Language.None )
                {
                    pageFiles.Add($"exd/{sheetName}_{bp.StartId}.exd");
                }

                var lang = LanguageUtil.GetLanguageStr( language );

                pageFiles.Add($"exd/{sheetName}_{bp.StartId}_{lang}.exd");
            }    
        }
        return pageFiles;
    }

    private void HandleSqPackFile(SqPackFile file, string fileStoragePath)
    {
        using var stream = new MemoryStream(file.Resource.Data);
        SaveFile(stream, fileStoragePath, file.Hash);
        _directory.RecordSqPackFile(file);
    }
    
    private static void SaveFile(Stream stream, string fileStoragePath, string hash)
    { 
        var fileName = Path.Combine(fileStoragePath, hash[..2], hash);
        // if (File.Exists(fileName)) return;
        if (!Directory.Exists(Path.Combine(fileStoragePath, hash[..2])))
            Directory.CreateDirectory(Path.Combine(fileStoragePath, hash[..2]));
        using var file = File.Create(fileName);
        stream.Position = 0;
        stream.CopyTo(file);
    }

    private static string HashStream(Stream stream)
    {
        using var sha256 = SHA256.Create();
        stream.Position = 0;
        var hash = sha256.ComputeHash(stream);
        var sb = new StringBuilder();
        foreach (var b in hash)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }

    private static Stream GetFile(string fileStoragePath, string hash)
    {
        return new System.IO.FileInfo(Path.Combine(fileStoragePath, hash[..2], hash)).OpenRead();
    }

    private SqPackFile<T>? GetSqPackFile<T>(string path) where T : FileResource
    {
        var category = Utils.GetCategoryIdForPath(path);

        if (!_indexFiles.ContainsKey(category))
        {
            var indexHash = _directory.IndexFiles[category];
            using var exdIndexStream = GetFile(_fileStoragePath, indexHash);
            _indexFiles[category] = IndexFile.FromStream(exdIndexStream, category);
        }

        var fileParams = _indexFiles[category].GetFileOffsetAndDat(path);
        if (fileParams is { dataFileId: 0, offset: 0 }) return null;
        
        var datFile = $"/sqpack/ffxiv/{category:x6}.win32.dat{fileParams.dataFileId}";

        var offset = (long)fileParams.offset;
        T? file;

        if (_datStreams.TryGetValue(datFile, out var stream) && stream.TryGetStream(offset, out var subStream))
        {
            var sqStream = new SqPackStream(subStream, PlatformId.Win32);
            file = sqStream.ReadFile<T>(0);
            subStream.Close();
            // Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] New file: {path}");
        }
        else if (_fileDatStreams.TryGetValue(category, out var datStream))
        {
            var sqStream = new SqPackStream(datStream, PlatformId.Win32);
            file = sqStream.ReadFile<T>(offset);
            // Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] New file: {path}");
        }
        else
        {
            // Assume unchanged. Yeah this is stupid
            SqPackFile? sqPackFile = null;
            int i = _previousDirectories.Count - 1;
            while (sqPackFile == null && i >= 0)
            {
                sqPackFile = _previousDirectories[i].SqPackFiles.Select(f => f.Value).FirstOrDefault(f => f.FileName == path);
                i--;
            }
            
            if (sqPackFile == null) return null;
            
            // Console.WriteLine($"[{TimeSpan.FromMilliseconds(_time.ElapsedMilliseconds):c}] Took {path} from {_previousDirectories[i + 1].Version}");
            file = GetFileFromDisk<T>(Path.Combine(_fileStoragePath, sqPackFile.Hash[..2], sqPackFile.Hash));
        }
        
        var hash = "";
        
        if (file != null)
            hash = HashStream(new MemoryStream(file.Data));
        
        return new SqPackFile<T>
        {
            FileName = path,
            DataFileId = fileParams.dataFileId,
            Offset = (ulong)offset,
            Hash = hash,
            Resource = file,
        };
    }

    private void LoadPreviousDirectories(string outputDirectory)
    {
        if (_previousDirectory != null && _previousDirectories != null) return;

        _previousDirectories =
            Directory
                .GetFiles(outputDirectory, "*.json")
                .Select(File.ReadAllText)
                .Select(g => JsonSerializer.Deserialize<PatchDataDirectory>(g))
                .Where(v => v!.Version < _directory.Version)
                .ToList()!;
        
        _previousDirectory = _previousDirectories.Max();
    }
    
    public T GetFileFromDisk<T>(string path, string? origPath = null) where T : FileResource
    {
        if(!File.Exists(path))
        {
            throw new FileNotFoundException($"the file at the path '{path}' doesn't exist");
        }

        var fileContent = File.ReadAllBytes(path);

        var file = Activator.CreateInstance<T>();
        
        var bindingFlags = System.Reflection.BindingFlags.Instance | BindingFlags.Public;
        
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
}