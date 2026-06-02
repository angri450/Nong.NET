using System.Diagnostics;
using System.Text.Json;
using System.CommandLine;

namespace Nong.Cli.Common;

/// <summary>
/// Shared CLI helpers: file validation, timing, JSON output, error responses.
/// </summary>
public static class CliHelpers
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Validate that a file path argument is a real .docx file.
    /// Returns null on success, or an ErrorEntry on failure.
    /// </summary>
    public static ErrorEntry? ValidateDocxFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ErrorCodes.MissingArgument with { Message = "File path is required." };
        if (!File.Exists(path))
            return ErrorCodes.FileNotFound with { Message = $"File not found: {path}" };
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".docx")
            return ErrorCodes.UnsupportedFormat with { Message = $"Expected .docx file, got: {ext}" };
        return null;
    }

    /// <summary>
    /// Validate that a file path is a real .txt file.
    /// </summary>
    public static ErrorEntry? ValidateTextFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ErrorCodes.MissingArgument with { Message = "File path is required." };
        if (!File.Exists(path))
            return ErrorCodes.FileNotFound with { Message = $"File not found: {path}" };
        return null;
    }

    /// <summary>
    /// Write a JSON error response to stdout and return exit code 1.
    /// </summary>
    public static int WriteError(string command, ErrorEntry error, bool json)
    {
        if (json)
        {
            var output = JsonOutput.Fail(command, new List<ErrorEntry> { error });
            Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts));
        }
        else
        {
            Console.Error.WriteLine($"[{error.Code}] {error.Name}: {error.Message}");
        }
        return 1;
    }

    /// <summary>
    /// Write a JSON success response to stdout and return exit code 0.
    /// </summary>
    public static int WriteSuccess(string command, string summary, object? data, Dictionary<string, object>? metrics, bool json)
    {
        if (json)
        {
            var output = JsonOutput.Ok(command, summary, data);
            if (metrics != null)
            {
                foreach (var kv in metrics) output.Metrics[kv.Key] = kv.Value;
            }
            Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts));
        }
        return 0;
    }

    /// <summary>
    /// Time an action and report duration in Meta.
    /// </summary>
    public static (T result, long elapsedMs) Time<T>(Func<T> action)
    {
        var sw = Stopwatch.StartNew();
        var result = action();
        sw.Stop();
        return (result, sw.ElapsedMilliseconds);
    }

    /// <summary>Time a void action, return elapsed ms.</summary>
    public static long Time(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    /// <summary>
    /// Shorthand to add a not-implemented handler to a command.
    /// </summary>
    public static void SetNotImplemented(Command cmd, string description, Option<bool> jsonOpt)
    {
        cmd.SetHandler((bool json) =>
        {
            var name = GetFullName(cmd);
            if (json)
            {
                var output = JsonOutput.Ok(name, $"(not yet implemented) {description}");
                Console.WriteLine(JsonSerializer.Serialize(output, JsonOpts));
            }
            else
            {
                Console.WriteLine($"[nong {name}] Not yet implemented. {description}");
            }
        }, jsonOpt);
    }

    public static string GetFullName(Command cmd)
    {
        var parts = new List<string>();
        var current = cmd;
        while (current != null && current is not RootCommand)
        {
            parts.Insert(0, current.Name);
            current = current.Parents.OfType<Command>().FirstOrDefault();
        }
        return string.Join(" ", parts);
    }
}
