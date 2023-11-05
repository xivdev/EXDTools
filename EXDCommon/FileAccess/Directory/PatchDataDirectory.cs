using Lumina.Data;
using Newtonsoft.Json;

namespace EXDCommon.FileAccess.Directory;

public class PatchDataDirectory : IComparable
{
	public GameVersion Version { get; set; }
	public Dictionary<uint, string> IndexFiles { get; set; } = new();
	public Dictionary<string, SqPackFile> SqPackFiles { get; set; } = new();
	
	public PatchDataDirectory(GameVersion version)
	{
		Version = version;
	}

	public void RecordIndexFile(uint id, string hash)
	{
		IndexFiles.Add(id, hash);
	}
	
	public void RecordSqPackFile(SqPackFile file)
	{
		SqPackFiles.Add(file.FileName, file);
	}

	public int CompareTo(object? obj)
	{
		if (obj is not PatchDataDirectory other)
			return 1;
		
		return Version.CompareTo(other.Version);
	}
}