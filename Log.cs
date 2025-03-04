using System.Runtime.CompilerServices;
using System.Text.Json;

namespace EXDTooler;

public static class Log
{
    public static bool IsGHA;

    static Log()
    {
        JsonOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
    }

    public static void Error(Exception e)
    {
        Console.Error.WriteLine($"[ERROR] {e}");
    }

    public static void Error(string message)
    {
        Console.Error.WriteLine($"[ERROR] {message}");
    }

    public static void Warn(string message)
    {
        Console.WriteLine($"[WARN] {message}");
    }

    public static void Info(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public static void Info()
    {
        Info(string.Empty);
    }

    //

    public static bool IsVerboseEnabled;
    public static void Verbose(DefaultInterpolatedStringHandler message)
    {
        if (IsVerboseEnabled)
            Console.WriteLine($"[VERBOSE] {message.ToStringAndClear()}");
    }

    public static void Verbose(string message)
    {
        if (IsVerboseEnabled)
            Console.WriteLine($"[VERBOSE] {message}");
    }

    public static void Verbose()
    {
        Verbose(string.Empty);
    }

    public static void VerboseProgress(DefaultInterpolatedStringHandler message)
    {
        if (IsVerboseEnabled && !IsGHA)
            Console.Write($"\r[VERBOSE] {message.ToStringAndClear()}");
    }

    public static void VerboseClearLine()
    {
        if (IsVerboseEnabled && !IsGHA)
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
    }

    //

    public static bool IsDebugEnabled;
    public static void Debug(DefaultInterpolatedStringHandler handler)
    {
        if (IsDebugEnabled)
            Console.WriteLine($"[DEBUG] {handler.ToStringAndClear()}");
    }

    public static void Debug(string message)
    {
        if (IsDebugEnabled)
            Console.WriteLine($"[DEBUG] {message}");
    }

    private static JsonSerializerOptions JsonOptions { get; }
    public static void DebugObject(object value)
    {
        if (IsDebugEnabled)
            Console.WriteLine($"[DEBUG] {JsonSerializer.Serialize(value, JsonOptions)}");
    }
}