using System.Collections.Concurrent;
using System.Drawing;
using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

namespace ImageGallery.App.Services;

public sealed class ThumbnailService : IDisposable
{
    private const int BaseCacheCapacity = 300;
    private const int MaxPendingItems = 256;
    private const long SuperLargePixelThreshold = 50_000_000;
    private static readonly int MaxConcurrentDecoders = Math.Max(2, Math.Min(4, Environment.ProcessorCount / 2));

    private readonly LruCache<string, Image> _cache;
    private readonly ConcurrentDictionary<string, byte> _pendingKeys = new();
    private readonly ConcurrentDictionary<string, byte> _failedKeys = new();
    private readonly SemaphoreSlim _decoderGate = new(MaxConcurrentDecoders, MaxConcurrentDecoders);
    private readonly object _syncRoot = new();
    private volatile bool _disposed;
    private CancellationTokenSource? _prefetchCts;

    private int _currentThumbnailSize = 128;
    private int _currentCacheCapacity = BaseCacheCapacity;

    public ThumbnailService()
    {
        _cache = new LruCache<string, Image>(BaseCacheCapacity, image => image.Dispose());
    }

    public Image? GetOrQueue(ImageItem item, int thumbnailSize, Action invalidate)
    {
        if (_disposed)
        {
            return null;
        }

        AdjustCacheCapacity(thumbnailSize);

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

    public void SchedulePrefetch(IReadOnlyList<(ImageItem Item, int ThumbnailSize)> backlog)
    {
        if (_disposed || backlog.Count == 0)
        {
            return;
        }

        CancelPrefetchCore();

        var cts = new CancellationTokenSource();
        _prefetchCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Yield();

            foreach (var (item, thumbnailSize) in backlog)
            {
                if (token.IsCancellationRequested || _disposed)
                {
                    return;
                }

                var key = CreateKey(item, thumbnailSize);

                lock (_syncRoot)
                {
                    if (_cache.Contains(key))
                    {
                        continue;
                    }
                }

                if (_failedKeys.ContainsKey(key))
                {
                    continue;
                }

                if (_pendingKeys.TryAdd(key, 0))
                {
                    try
                    {
                        await _decoderGate.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            if (_disposed || token.IsCancellationRequested)
                            {
                                return;
                            }

                            var image = LoadThumbnail(item, thumbnailSize);
                            if (image != null && !_disposed)
                            {
                                lock (_syncRoot)
                                {
                                    _cache.Set(key, image);
                                }
                            }
                            else
                            {
                                image?.Dispose();
                                _failedKeys.TryAdd(key, 0);
                            }
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
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    finally
                    {
                        _pendingKeys.TryRemove(key, out _);
                    }
                }
            }
        }, token);
    }

    public void CancelPrefetch()
    {
        CancelPrefetchCore();
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
        CancelPrefetchCore();
        Clear();
        _decoderGate.Dispose();
    }

    private void CancelPrefetchCore()
    {
        var cts = Interlocked.Exchange(ref _prefetchCts, null);
        if (cts != null)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void AdjustCacheCapacity(int thumbnailSize)
    {
        _currentThumbnailSize = thumbnailSize;
        var scale = thumbnailSize / 128.0;
        var newCapacity = scale > 8.0
            ? Math.Max(30, (int)(BaseCacheCapacity / (scale / 4.0)))
            : BaseCacheCapacity;

        if (newCapacity != _currentCacheCapacity)
        {
            _currentCacheCapacity = newCapacity;
            lock (_syncRoot)
            {
                _cache.Capacity = newCapacity;
            }
        }
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
            using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

            if ((long)source.Width * source.Height > SuperLargePixelThreshold)
            {
                return LoadSuperLargeThumbnail(source, thumbnailSize);
            }

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
        catch (OutOfMemoryException)
        {
            return CreatePlaceholder(thumbnailSize);
        }
        catch
        {
            return null;
        }
    }

    private static Image? LoadSuperLargeThumbnail(Image source, int thumbnailSize)
    {
        try
        {
            var thumb = source.GetThumbnailImage(thumbnailSize, thumbnailSize, null, IntPtr.Zero);
            if (thumb.Width == thumbnailSize && thumb.Height == thumbnailSize)
            {
                return thumb;
            }

            var bitmap = new Bitmap(thumbnailSize, thumbnailSize);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            var x = (thumbnailSize - thumb.Width) / 2;
            var y = (thumbnailSize - thumb.Height) / 2;
            graphics.DrawImage(thumb, x, y, thumb.Width, thumb.Height);
            thumb.Dispose();

            return bitmap;
        }
        catch (OutOfMemoryException)
        {
            return CreatePlaceholder(thumbnailSize);
        }
    }

    private static Image? CreatePlaceholder(int thumbnailSize)
    {
        try
        {
            var bitmap = new Bitmap(thumbnailSize, thumbnailSize);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.FromArgb(248, 250, 252));
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
