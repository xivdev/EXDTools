namespace EXDWorker;

public class SparseMemoryStream : Stream
{
	public override bool CanRead { get; }
	public override bool CanSeek { get; }
	public override bool CanWrite { get; }
	public override long Length { get; }
	public override long Position { get; set; }
		
	public Dictionary<long, MemoryStream> ChunkDictionary = new Dictionary<long, MemoryStream>();
	public long StartPosition { get; set; }

	public IEnumerable<long> GetPopulatedChunks()
	{
		return ChunkDictionary.Keys;
	}

	public override void Flush()
	{
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		if (!ChunkDictionary.TryGetValue(Position, out var stream))
			return 0;

		var r = stream.Read(buffer, offset, count);
		Position += count;

		return r;
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

	public override void SetLength(long value)
	{
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		if (!ChunkDictionary.TryGetValue(Position, out var stream))
		{
			stream = new MemoryStream();
			ChunkDictionary.Add(Position, stream);
		}

		stream.Write(buffer, offset, count);
		Position += count;
	}
}