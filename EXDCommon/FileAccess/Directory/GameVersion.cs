namespace EXDCommon.FileAccess.Directory;

public class GameVersion : IComparable
{
    public const int PatchVersionStringLength = 20;
    
    public uint Year { get; set; }
    public uint Month { get; set; }
    public uint Day { get; set; }
    public uint Part { get; set; }
    public uint Revision { get; set; }
    
    public static GameVersion Parse(string input)
    {
        try
        {
            var parts = input.Split('.');
            return new GameVersion
            {
                Year = uint.Parse(parts[0]),
                Month = uint.Parse(parts[1]),
                Day = uint.Parse(parts[2]),
                Part = uint.Parse(parts[3]),
                Revision = uint.Parse(parts[4])
            };
        }
        catch (FormatException e)
        {
            throw new Exception($"Failed to parse game version {input}", e);
        }
    }
    
    public override string ToString() => $"{Year:0000}.{Month:00}.{Day:00}.{Part:0000}.{Revision:0000}";

    public int CompareTo(object obj)
    {
        var other = obj as GameVersion;
        if (other == null)
            return 1;

        if (Year > other.Year)
            return 1;

        if (Year < other.Year)
            return -1;

        if (Month > other.Month)
            return 1;

        if (Month < other.Month)
            return -1;

        if (Day > other.Day)
            return 1;

        if (Day < other.Day)
            return -1;

        if (Revision > other.Revision)
            return 1;

        if (Revision < other.Revision)
            return -1;

        if (Part > other.Part)
            return 1;

        if (Part < other.Part)
            return -1;

        return 0;
    }

    public static bool operator <(GameVersion x, GameVersion y) => x.CompareTo(y) < 0;
    public static bool operator >(GameVersion x, GameVersion y) => x.CompareTo(y) > 0;
    public static bool operator <=(GameVersion x, GameVersion y) => x.CompareTo(y) <= 0;
    public static bool operator >=(GameVersion x, GameVersion y) => x.CompareTo(y) >= 0;

    public static bool operator ==(GameVersion x, GameVersion y)
    {
        if (x is null)
            return y is null;

        return x.CompareTo(y) == 0;
    }

    public static bool operator !=(GameVersion x, GameVersion y)
    {
        if (x is null)
            return y != null;

        return x.CompareTo(y) != 0;
    }
}