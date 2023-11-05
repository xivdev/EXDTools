namespace DirectoryManager;

public class SparseSmallFileDataCollection : Stream
{
	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override long Length { get; }
	public override long Position { get; set; }

	private readonly string _base;
	private readonly SortedSet<long> _chunks;

	public SparseSmallFileDataCollection()
	{
		_base = Path.GetTempFileName();
		File.Delete(_base);
		Directory.CreateDirectory(_base);

		_chunks = new SortedSet<long>();
	}
	
	public override int Read(byte[] buffer, int offset, int count)
	{
		if (!_chunks.Contains(Position)) return 0;
		using var fs = new System.IO.FileInfo(Path.Combine(_base, Position.ToString())).OpenRead();
		var read = fs.Read(buffer, offset, count);
		Position += count;
		return read;
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		_chunks.Add(Position);
		using var fs = new System.IO.FileInfo(Path.Combine(_base, Position.ToString())).OpenWrite();
		fs.Write(buffer, offset, count);
		Position += count;
	}
	
	public bool TryGetStream(long offset, out Stream stream)
	{
		if (!_chunks.Contains(offset))
		{
			stream = Null;
			return false;
		}
		
		stream = new System.IO.FileInfo(Path.Combine(_base, offset.ToString())).OpenRead();
		return true;
	}
	
	public override long Seek(long offset, SeekOrigin origin)
	{
		switch (origin)
		{
			case SeekOrigin.Begin:
				Position = offset;
				break;
			case SeekOrigin.Current:
				Position += offset;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
		}

		return Position;
	}

	public new void Dispose()
	{
		base.Dispose();

		Directory.Delete(_base, true);
	}
	
	public override void Flush()
	{
	}
	
	public override void SetLength(long value)
	{
	}
}