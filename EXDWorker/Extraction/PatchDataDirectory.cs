namespace EXDWorker;

public class PatchDataDirectory(GameVersion version) : IComparable
{
	public GameVersion Version { get; set; } = version;
	public Dictionary<uint, string> IndexFiles { get; set; } = new();
	public Dictionary<string, SqPackFile> SqPackFiles { get; set; } = new();

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