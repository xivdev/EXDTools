using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace EXDTooler;

public static class Log
{
    public static bool IsGHA;
    private static bool IsOnProgressLine;

    private static readonly List<(LogLevel, string, AnnotatedMetadata?)> WritableAnnotations = [];
    public static IReadOnlyList<(LogLevel, string, AnnotatedMetadata?)> Annotations => WritableAnnotations;

    static Log()
    {
        JsonOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
    }

    public enum LogLevel
    {
        Error,
        Warn,
        Info,
        Verbose,
        Debug
    }

    private static string GetName(this LogLevel level) =>
        level switch
        {
            LogLevel.Error => "ERROR",
            LogLevel.Warn => "WARN",
            LogLevel.Info => "INFO",
            LogLevel.Verbose => "VERBOSE",
            LogLevel.Debug => "DEBUG",
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };

    private static string GetGHAName(this LogLevel level) =>
        level switch
        {
            LogLevel.Error => "error",
            LogLevel.Warn => "warning",
            LogLevel.Info => "notice",
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };

    private static TextWriter GetWriter(this LogLevel level) =>
        level switch
        {
            LogLevel.Error => Console.Error,
            _ => Console.Out,
        };

    private static void LogMessage(LogLevel logLevel, string message)
    {
        var writer = logLevel.GetWriter();
        if (IsOnProgressLine)
        {
            writer.WriteLine();
            IsOnProgressLine = false;
        }
        writer.WriteLine($"[{logLevel.GetName()}] {message}");
    }

    private static void LogProgress(LogLevel logLevel, string message)
    {
        if (IsGHA)
            return;
        var writer = logLevel.GetWriter();
        writer.Write($"\r[{logLevel.GetName()}] {message}");
        IsOnProgressLine = true;
    }

    private static void LogProgressClear(LogLevel logLevel)
    {
        if (IsGHA)
            return;
        var writer = logLevel.GetWriter();
        writer.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
        IsOnProgressLine = false;
    }

    private static void LogAnnotated(LogLevel logLevel, string message, AnnotatedMetadata? metadata)
    {
        WritableAnnotations.Add((logLevel, message, metadata));

        if (IsGHA)
            Console.WriteLine($"::{logLevel.GetGHAName()}{(metadata ?? new()).FormatGHA(message)}");
        else
        {
            var writer = logLevel.GetWriter();
            writer.WriteLine($"[{logLevel.GetName()}]{(metadata ?? new()).FormatPlain(message)}");
        }
    }

    //

    public record struct AnnotatedMetadata
    {
        public string? Title;
        public string? File;
        public int? Line;
        public int? EndLine;
        public int? StartColumn;
        public int? EndColumn;

        // https://github.com/actions/toolkit/blob/dc22dc7cad322ab3cf9280133face378c63195f7/packages/core/src/command.ts#L80-L85
        private static string EscapeData(string data) =>
            data
            .Replace("%", "%25")
            .Replace("\r", "%0D")
            .Replace("\n", "%0A");

        // https://github.com/actions/toolkit/blob/dc22dc7cad322ab3cf9280133face378c63195f7/packages/core/src/command.ts#L87-94
        private static string EscapeProperty(string property) =>
            property
            .Replace("%", "%25")
            .Replace("\r", "%0D")
            .Replace("\n", "%0A")
            .Replace(":", "%3A")
            .Replace(",", "%2C");

        public readonly string FormatGHA(string message)
        {
            List<string> ret = [];
            if (Title != null)
                ret.Add($"title={EscapeProperty(Title)}");
            if (File != null)
                ret.Add($"file={EscapeProperty(File)}");
            if (Line != null)
                ret.Add($"line={EscapeProperty(Line.Value.ToString())}");
            if (EndLine != null)
                ret.Add($"endLine={EscapeProperty(EndLine.Value.ToString())}");
            if (StartColumn != null)
                ret.Add($"col={EscapeProperty(StartColumn.Value.ToString())}");
            if (EndColumn != null)
                ret.Add($"endColumn={EscapeProperty(EndColumn.Value.ToString())}");

            var msg = new StringBuilder();
            if (ret.Count != 0)
            {
                msg.Append(' ');
                msg.Append(string.Join(',', ret));
            }
            msg.Append("::");
            msg.Append(EscapeData(message));
            return msg.ToString();
        }

        public readonly string FormatPlain(string message)
        {
            var msg = new StringBuilder();
            if (Title != null)
            {
                msg.Append(' ');
                msg.Append(Title);
            }
            if (File != null)
            {
                msg.Append(' ');
                msg.Append(File);
            }
            if (Line != null)
            {
                msg.Append(' ');
                msg.Append(Line);
            }
            if (EndLine != null)
            {
                msg.Append('-');
                msg.Append(EndLine);
            }
            if (StartColumn != null)
            {
                msg.Append(':');
                msg.Append(StartColumn);
            }
            if (EndColumn != null)
            {
                msg.Append('-');
                msg.Append(EndColumn);
            }
            msg.Append(' ');
            msg.Append(message);
            return msg.ToString();
        }
    }

    //

    public static void AnnotatedError(string message, AnnotatedMetadata? metadata = null) =>
        LogAnnotated(LogLevel.Error, message, metadata);

    public static void Error(Exception e) =>
        Error(e.ToString());

    public static void Error(string message) =>
        LogMessage(LogLevel.Error, message);

    //

    public static void AnnotatedWarn(string message, AnnotatedMetadata? metadata = null) =>
        LogAnnotated(LogLevel.Warn, message, metadata);

    public static void Warn(string message) =>
        LogMessage(LogLevel.Warn, message);

    //

    public static void AnnotatedInfo(string message, AnnotatedMetadata? metadata = null) =>
        LogAnnotated(LogLevel.Info, message, metadata);

    public static void Info(string message) =>
        LogMessage(LogLevel.Info, message);

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
            LogMessage(LogLevel.Verbose, message);
    }

    public static void Verbose() =>
        Verbose(string.Empty);

    public static void VerboseProgress(DefaultInterpolatedStringHandler message)
    {
        if (IsVerboseEnabled)
            LogProgress(LogLevel.Verbose, message.ToStringAndClear());
    }

    public static void VerboseProgressClear()
    {
        if (IsVerboseEnabled)
            LogProgressClear(LogLevel.Verbose);
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
            LogMessage(LogLevel.Debug, message);
    }

    private static JsonSerializerOptions JsonOptions { get; }
    public static void DebugObject(object value)
    {
        if (IsDebugEnabled)
            LogMessage(LogLevel.Debug, JsonSerializer.Serialize(value, JsonOptions));
    }

    //

    public static void Output(string key, string value)
    {
        if (IsGHA && Environment.GetEnvironmentVariable("GITHUB_OUTPUT") is { } file)
        {
            using var writer = new StreamWriter(file, true, Encoding.UTF8);

            if (value.Contains('\n'))
            {
                var delimiter = Guid.NewGuid().ToString("N");
                writer.WriteLine($"{key}<<{delimiter}");
                writer.WriteLine(value);
                writer.WriteLine(delimiter);
            }
            else
                writer.WriteLine(value);
        }
        else
            Info($"GITHUB_OUTPUT => {key} = {value}");
    }
}