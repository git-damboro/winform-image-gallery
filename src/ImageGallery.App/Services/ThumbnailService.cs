using System.Collections.Concurrent;
using System.Drawing;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Services;

public sealed class ThumbnailService : IDisposable
{
    private const int CacheCapacity = 600;

    private readonly LruCache<string, Image> _cache = new(CacheCapacity, image => image.Dispose());
    private readonly ConcurrentDictionary<string, byte> _pendingKeys = new();
    private readonly object _syncRoot = new();

    public Image? GetOrQueue(ImageItem item, int thumbnailSize, Action invalidate)
    {
        var key = CreateKey(item, thumbnailSize);

        lock (_syncRoot)
        {
            if (_cache.TryGet(key, out var cached))
            {
                return cached;
            }
        }

        if (_pendingKeys.TryAdd(key, 0))
        {
            _ = Task.Run(() => LoadThumbnail(item, thumbnailSize))
                .ContinueWith(task =>
                {
                    try
                    {
                        if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
                        {
                            lock (_syncRoot)
                            {
                                _cache.Set(key, task.Result);
                            }
                        }
                    }
                    finally
                    {
                        _pendingKeys.TryRemove(key, out _);
                        invalidate();
                    }
                }, TaskScheduler.Default);
        }

        return null;
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _cache.Clear();
        }
    }

    public void Dispose()
    {
        Clear();
    }

    private static string CreateKey(ImageItem item, int thumbnailSize)
    {
        try
        {
            var lastWriteTicks = File.GetLastWriteTimeUtc(item.FilePath).Ticks;
            return $"{item.FilePath}|{lastWriteTicks}|{thumbnailSize}";
        }
        catch
        {
            return $"{item.FilePath}|missing|{thumbnailSize}";
        }
    }

    private static Image? LoadThumbnail(ImageItem item, int thumbnailSize)
    {
        if (item.HasError || !File.Exists(item.FilePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

            var scale = Math.Min(thumbnailSize / (double)source.Width, thumbnailSize / (double)source.Height);
            var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

            var bitmap = new Bitmap(thumbnailSize, thumbnailSize);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            var x = (thumbnailSize - targetWidth) / 2;
            var y = (thumbnailSize - targetHeight) / 2;
            graphics.DrawImage(source, x, y, targetWidth, targetHeight);

            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
