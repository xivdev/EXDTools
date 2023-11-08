namespace DirectoryManager.Utility;

public class TempDirectory : IDisposable
{
	public string Path { get; }

	public TempDirectory()
	{
		Path = System.IO.Path.GetTempFileName();
		File.Delete(Path);
		Directory.CreateDirectory(Path);
	}
	
	public void Dispose()
	{
		Directory.Delete(Path, true);
	}
}