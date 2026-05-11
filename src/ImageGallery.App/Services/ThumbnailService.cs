using System.Collections.Concurrent;
using System.Drawing;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Services;

public sealed class ThumbnailService : IDisposable
{
    private const int CacheCapacity = 300;
    private const int MaxPendingItems = 256;
    private static readonly int MaxConcurrentDecoders = Math.Max(2, Math.Min(4, Environment.ProcessorCount / 2));

    private readonly LruCache<string, Image> _cache = new(CacheCapacity, image => image.Dispose());
    private readonly ConcurrentDictionary<string, byte> _pendingKeys = new();
    private readonly ConcurrentDictionary<string, byte> _failedKeys = new();
    private readonly SemaphoreSlim _decoderGate = new(MaxConcurrentDecoders, MaxConcurrentDecoders);
    private readonly object _syncRoot = new();
    private volatile bool _disposed;

    public Image? GetOrQueue(ImageItem item, int thumbnailSize, Action invalidate)
    {
        if (_disposed)
        {
            return null;
        }

        var key = CreateKey(item, thumbnailSize);

        lock (_syncRoot)
        {
            if (_cache.TryGet(key, out var cached))
            {
                return cached;
            }
        }

        if (_failedKeys.ContainsKey(key) || _pendingKeys.Count >= MaxPendingItems)
        {
            return null;
        }

        if (_pendingKeys.TryAdd(key, 0))
        {
            _ = Task.Run(async () =>
                {
                    try
                    {
                        await _decoderGate.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            return _disposed ? null : LoadThumbnail(item, thumbnailSize);
                        }
                        finally
                        {
                            try
                            {
                                _decoderGate.Release();
                            }
                            catch (ObjectDisposedException)
                            {
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        return null;
                    }
                })
                .ContinueWith(task =>
                {
                    try
                    {
                        if (task.Status == TaskStatus.RanToCompletion && task.Result != null && !_disposed)
                        {
                            lock (_syncRoot)
                            {
                                _cache.Set(key, task.Result);
                            }
                        }
                        else if (task.Status == TaskStatus.RanToCompletion)
                        {
                            task.Result?.Dispose();
                            _failedKeys.TryAdd(key, 0);
                        }
                        else
                        {
                            _ = task.Exception;
                            _failedKeys.TryAdd(key, 0);
                        }
                    }
                    finally
                    {
                        _pendingKeys.TryRemove(key, out _);
                        SafeInvalidate(invalidate);
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

        _failedKeys.Clear();
    }

    public void Dispose()
    {
        _disposed = true;
        Clear();
        _decoderGate.Dispose();
    }

    private static void SafeInvalidate(Action invalidate)
    {
        try
        {
            invalidate();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
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
