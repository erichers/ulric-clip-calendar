using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Data;
using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Api.Media;

public sealed class StorageLayout
{
    public StorageLayout(IConfiguration configuration)
    {
        Root = Path.GetFullPath(configuration["Storage:Root"] ?? "data");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string ClipDirectory(Guid id)
    {
        var dir = Path.Combine(Root, "media", id.ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string? Resolve(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        var root = Path.GetFullPath(Root);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return full;
    }

    public string Relative(string absolute)
    {
        var relative = Path.GetRelativePath(Root, absolute);
        return relative.Replace('\\', '/');
    }
}

public sealed class MediaQueue
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid id) => channel.Writer.TryWrite(id);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => channel.Reader.ReadAllAsync(ct);

    public async Task RequeueOutstandingAsync(AppDbContext db, CancellationToken ct = default)
    {
        var ids = await db.Clips
            .Where(clip => clip.MediaState == MediaJobState.Pending || clip.MediaState == MediaJobState.Running)
            .Select(clip => clip.Id)
            .ToListAsync(ct);
        foreach (var id in ids)
        {
            Enqueue(id);
        }
    }
}

public sealed class MediaWorker(
    MediaQueue queue,
    IServiceScopeFactory scopes,
    ILogger<MediaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<MediaProcessor>();
                await processor.ProcessAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Media job {ClipId} failed", id);
            }
        }
    }
}

public sealed class MediaProcessor(AppDbContext db, StorageLayout storage, FfmpegStatus ffmpeg, ILogger<MediaProcessor> logger)
{
    public async Task ProcessAsync(Guid clipId, CancellationToken ct)
    {
        var clip = await db.Clips.FirstOrDefaultAsync(item => item.Id == clipId, ct);
        if (clip is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(clip.OriginalPath))
        {
            clip.MediaState = string.IsNullOrWhiteSpace(clip.SourceLink) ? MediaJobState.NotRequested : MediaJobState.Unavailable;
            clip.MediaMessage = string.IsNullOrWhiteSpace(clip.SourceLink)
                ? "Add a video file or a link."
                : "Linked clips stay on their original URL. Upload a file if you want a thumbnail or a trim.";
            clip.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var original = storage.Resolve(clip.OriginalPath);
        if (original is null || !File.Exists(original))
        {
            clip.MediaState = MediaJobState.Failed;
            clip.MediaMessage = "The original file is missing from storage.";
            clip.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (!ffmpeg.IsAvailable)
        {
            clip.MediaState = MediaJobState.Unavailable;
            clip.MediaMessage = ffmpeg.Message;
            clip.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        clip.MediaState = MediaJobState.Running;
        clip.MediaMessage = "Reading the file.";
        clip.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            var probe = await FfmpegRunner.ProbeAsync(ffmpeg.ProbePath, original, ct);
            clip.DurationSeconds = probe.Duration;
            clip.Width = probe.Width;
            clip.Height = probe.Height;
            clip.VideoCodec = probe.Codec;

            var directory = Path.GetDirectoryName(original)!;
            var wantsTrim = clip.TrimStartSeconds is double start && clip.TrimEndSeconds is double end && end > start;
            string previewSource = original;
            if (wantsTrim)
            {
                var processed = Path.Combine(directory, "processed.mp4");
                await FfmpegRunner.RenderPreviewAsync(
                    ffmpeg.FfmpegPath,
                    original,
                    processed,
                    clip.TrimStartSeconds,
                    clip.TrimEndSeconds,
                    probe.HasAudio,
                    ct);
                clip.ProcessedPath = storage.Relative(processed);
                previewSource = processed;
                clip.MediaMessage = "Trimmed preview is ready. The original file is unchanged.";
            }
            else if (!FfmpegRunner.IsNineSixteen(probe.Width, probe.Height))
            {
                var preview = Path.Combine(directory, "preview.mp4");
                await FfmpegRunner.RenderPreviewAsync(ffmpeg.FfmpegPath, original, preview, null, null, probe.HasAudio, ct);
                clip.ProcessedPath = storage.Relative(preview);
                previewSource = preview;
                clip.MediaMessage = "A 9:16 preview is ready. The original file is unchanged.";
            }
            else
            {
                clip.ProcessedPath = null;
                clip.MediaMessage = "Preview is ready.";
            }

            var thumb = Path.Combine(directory, "thumb.jpg");
            await FfmpegRunner.ThumbnailAsync(ffmpeg.FfmpegPath, previewSource, thumb, ct);
            clip.ThumbnailPath = storage.Relative(thumb);
            clip.MediaState = MediaJobState.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "ffmpeg failed for clip {ClipId}", clip.Id);
            clip.MediaState = MediaJobState.Failed;
            var message = ex.Message.Replace('\n', ' ').Trim();
            clip.MediaMessage = message.Length > 280 ? message[..280] : message;
        }

        clip.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
