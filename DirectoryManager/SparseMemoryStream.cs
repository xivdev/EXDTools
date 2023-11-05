namespace DirectoryManager;

public class SparseMemoryStream : Stream
{
	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;
	public override long Length { get; }
	public override long Position { get; set; }
		
	public readonly Dictionary<long, MemoryStream> ChunkDictionary = new();
	private readonly SortedSet<long> _chunks = new();

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

		// return Read2(buffer, offset, count);
	}
	
	public int Read2(byte[] buffer, int offset, int count)
	{
		int read = 0;
		if (ChunkDictionary.TryGetValue(Position, out var stream) && count < stream.Length)
		{
			stream.Position = 0;
			read = stream.Read(buffer, offset, count);
			Position += read;
		}
		else
		{
			// var chunks = ChunkDictionary.Keys.Where(k => k < Position + count).OrderByDescending(k => k).ToList();
			while (read < count)
			{
				var chunkToRead = long.MaxValue;
				foreach (var chunkOffset in _chunks)
				{
					if (chunkOffset <= Position)
					{
						chunkToRead = chunkOffset;
					}
				}
				
				var subPosition = Position - chunkToRead;
				var subStream = ChunkDictionary[chunkToRead];
				subStream.Position = subPosition;
				read += subStream.Read(buffer, offset + read, count - read);
				Position += read;
			}
		}
		
		return read;
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
			_chunks.Add(Position);
		}

		stream.Write(buffer, offset, count);
		Position += count;
	}
}