using System.Diagnostics;

namespace DirectoryManager;

public class SparseDataCollection : Stream
{
	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => true;
	public override long Length { get; }
	public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

	private readonly System.IO.FileInfo _baseFile;
	private readonly Stream _baseStream;
	private readonly SortedDictionary<long, long> _chunks;

	public SparseDataCollection()
	{
		_baseFile = new System.IO.FileInfo(Path.GetTempFileName());
		_baseStream = _baseFile.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
		// _baseStream.SetLength(1024L * 1024L * 1024L * 8L); // 8GB
		_chunks = new SortedDictionary<long, long>();
	}
	
	// idk why this doesn't work i give up
	public override int Read(byte[] buffer, int offset, int count)
	{
		long chunkOffset = long.MaxValue;
		foreach (var (pos, length) in _chunks)
		{
			if (Position >= pos && Position < pos + length)
			{
				chunkOffset = pos;
				break;
			}
		}
		if (chunkOffset == long.MaxValue) return 0;
		
		var read = _baseStream.Read(buffer, offset, count);
		return read;
	}
	
	public override void Write(byte[] buffer, int offset, int count)
	{
		if (count == 0) return;
		if (!_chunks.ContainsKey(Position))
			_chunks.Add(Position, count);
		_baseStream.Write(buffer, offset, count);
		_baseStream.Flush();
	}
	
	public bool TryGetStream(long offset, out Stream stream)
	{
		if (!_chunks.TryGetValue(offset, out var length))
		{
			stream = Null;
			return false;
		}
		
		_baseStream.Position = offset;
		var data = new byte[length];
		var read = _baseStream.Read(data, 0, (int)length);
		stream = new MemoryStream(data);
		
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
		_baseStream.Dispose();
		_baseFile.Delete();
	}
	
	public override void Flush()
	{
	}
	
	public override void SetLength(long value)
	{
	}
}