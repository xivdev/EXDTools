using System.Text.Json.Serialization;
using Lumina.Data;

namespace EXDWorker;

public class SqPackFile
{
	public string FileName { get; set; }
	public string Hash { get; set; }
	public uint DataFileId { get; set; }
	public ulong Offset { get; set; }
	
	[JsonIgnore]
	public FileResource? Resource { get; set; }
}

public class SqPackFile<T> : SqPackFile where T : FileResource
{
	[JsonIgnore]
	public T? TypedResource => Resource as T;
}