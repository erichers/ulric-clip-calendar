using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Ulric.ClipCalendar.Api.Media;

public sealed record FfmpegStatus(bool IsAvailable, string FfmpegPath, string ProbePath, string Message);

public static class FfmpegLocator
{
    public const string MissingMessage =
        "ffmpeg is not installed on this server. Files are saved, and thumbnails, trims, and generated previews wait until ffmpeg is available.";

    public static FfmpegStatus Resolve(string? ffmpeg, string? probe)
    {
        var ffmpegPath = ResolveBinary(ffmpeg, "ffmpeg");
        var probePath = ResolveBinary(probe, "ffprobe");
        if (ffmpegPath is null || probePath is null)
        {
            return new FfmpegStatus(false, ffmpegPath ?? "ffmpeg", probePath ?? "ffprobe", MissingMessage);
        }

        return new FfmpegStatus(true, ffmpegPath, probePath, "");
    }

    private static string? ResolveBinary(string? configured, string name)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return File.Exists(configured) ? Path.GetFullPath(configured) : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir, name);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }
}

public sealed record ProbeInfo(double? Duration, int? Width, int? Height, string? Codec, bool HasAudio);

public static class FfmpegRunner
{
    public static async Task WriteDemoAsync(string ffmpeg, string output, string lavfi, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var psi = Start(ffmpeg);
        Add(psi, "-y", "-f", "lavfi", "-i", lavfi, "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "33", "-an", output);
        await RunAsync(psi, ct);
    }

    public static async Task<ProbeInfo> ProbeAsync(string ffprobe, string input, CancellationToken ct)
    {
        var psi = Start(ffprobe);
        Add(psi, "-v", "error", "-print_format", "json", "-show_format", "-show_streams", input);
        var stdout = await RunAsync(psi, ct);
        using var doc = JsonDocument.Parse(stdout);
        double? duration = null;
        if (doc.RootElement.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var durationElement) &&
            double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            duration = parsed;
        }

        int? width = null;
        int? height = null;
        string? codec = null;
        var hasAudio = false;
        if (doc.RootElement.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var type = stream.TryGetProperty("codec_type", out var typeElement) ? typeElement.GetString() : null;
                if (type == "audio")
                {
                    hasAudio = true;
                }

                if (type == "video" && width is null)
                {
                    if (stream.TryGetProperty("width", out var widthElement))
                    {
                        width = widthElement.GetInt32();
                    }

                    if (stream.TryGetProperty("height", out var heightElement))
                    {
                        height = heightElement.GetInt32();
                    }

                    if (stream.TryGetProperty("codec_name", out var codecElement))
                    {
                        codec = codecElement.GetString();
                    }
                }
            }
        }

        return new ProbeInfo(duration, width, height, codec, hasAudio);
    }

    public static async Task ThumbnailAsync(string ffmpeg, string input, string output, CancellationToken ct)
    {
        try
        {
            await ThumbnailAtAsync(ffmpeg, input, output, "0.2", ct);
        }
        catch (InvalidOperationException)
        {
            await ThumbnailAtAsync(ffmpeg, input, output, "0", ct);
        }
    }

    public static Task RenderPreviewAsync(
        string ffmpeg,
        string input,
        string output,
        double? start,
        double? end,
        bool hasAudio,
        CancellationToken ct)
    {
        var psi = Start(ffmpeg);
        Add(psi, "-y", "-i", input);
        if (start is double startSeconds)
        {
            Add(psi, "-ss", startSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (end is double endSeconds)
        {
            Add(psi, "-to", endSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        }

        Add(psi, "-vf", "scale=720:1280:force_original_aspect_ratio=increase,crop=720:1280",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "28", "-pix_fmt", "yuv420p");
        if (hasAudio)
        {
            Add(psi, "-c:a", "aac");
        }
        else
        {
            Add(psi, "-an");
        }

        Add(psi, "-movflags", "+faststart", output);
        return RunAsync(psi, ct);
    }

    public static bool IsNineSixteen(int? width, int? height)
    {
        if (width is null || height is null || height == 0)
        {
            return false;
        }

        var ratio = width.Value / (double)height.Value;
        return Math.Abs(ratio - (9d / 16d)) < 0.03;
    }

    private static async Task ThumbnailAtAsync(string ffmpeg, string input, string output, string timestamp, CancellationToken ct)
    {
        var psi = Start(ffmpeg);
        Add(psi, "-y", "-ss", timestamp, "-i", input, "-frames:v", "1", "-vf",
            "scale=360:640:force_original_aspect_ratio=increase,crop=360:640", output);
        await RunAsync(psi, ct);
    }

    private static ProcessStartInfo Start(string file) => new()
    {
        FileName = file,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    private static void Add(ProcessStartInfo psi, params string[] args)
    {
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }
    }

    private static async Task<string> RunAsync(ProcessStartInfo psi, CancellationToken ct)
    {
        using var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start ffmpeg.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            var tail = stderr.Length > 400 ? stderr[^400..] : stderr;
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(tail) ? "ffmpeg failed." : tail.Trim());
        }

        return stdout;
    }
}
