using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace EXDTooler;

public static class Log
{
    public static bool IsGHA;
    private static bool IsOnProgressLine;

    static Log()
    {
        JsonOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
    }

    private static void LogMessage(string logLevel, string message, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        if (IsOnProgressLine)
        {
            writer.WriteLine();
            IsOnProgressLine = false;
        }
        writer.WriteLine($"[{logLevel}] {message}");
    }

    private static void LogProgress(string logLevel, string message, TextWriter? writer = null)
    {
        if (IsGHA)
            return;
        writer ??= Console.Out;
        writer.Write($"\r[{logLevel}] {message}");
        IsOnProgressLine = true;
    }

    private static void LogProgressClear(TextWriter? writer = null)
    {
        if (IsGHA)
            return;
        writer ??= Console.Out;
        writer.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
        IsOnProgressLine = false;
    }

    public static void Error(Exception e) =>
        Error(e.ToString());

    public static void Error(string message) =>
        LogMessage("ERROR", message, Console.Error);

    public static void Warn(string message) =>
        LogMessage("WARN", message);

    public static void Info(string message) =>
        LogMessage("INFO", message);

    public static void Info() =>
        Info(string.Empty);

    //

    public static bool IsVerboseEnabled;
    public static void Verbose(DefaultInterpolatedStringHandler message)
    {
        if (IsVerboseEnabled)
            Verbose(message.ToStringAndClear());
    }

    public static void Verbose(string message)
    {
        if (IsVerboseEnabled)
            LogMessage("VERBOSE", message);
    }

    public static void Verbose() =>
        Verbose(string.Empty);

    public static void VerboseProgress(DefaultInterpolatedStringHandler message)
    {
        if (IsVerboseEnabled)
            LogProgress("VERBOSE", message.ToStringAndClear());
    }

    public static void VerboseProgressClear()
    {
        if (IsVerboseEnabled)
            LogProgressClear();
    }

    //

    public static bool IsDebugEnabled;
    public static void Debug(DefaultInterpolatedStringHandler handler)
    {
        if (IsDebugEnabled)
            Debug(handler.ToStringAndClear());
    }

    public static void Debug(string message)
    {
        if (IsDebugEnabled)
            LogMessage("DEBUG", message);
    }

    private static JsonSerializerOptions JsonOptions { get; }
    public static void DebugObject(object value)
    {
        if (IsDebugEnabled)
            LogMessage("DEBUG", JsonSerializer.Serialize(value, JsonOptions));
    }
}