using ImageGallery.Core.Models;
using ImageGallery.Core.Services;

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}

static void AssertSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string name)
{
    var expectedArray = expected.ToArray();
    var actualArray = actual.ToArray();

    if (!expectedArray.SequenceEqual(actualArray))
    {
        throw new InvalidOperationException(
            $"{name}: expected [{string.Join(", ", expectedArray)}], got [{string.Join(", ", actualArray)}]");
    }
}

AssertEqual("512 B", HumanReadableSizeFormatter.Format(512), "bytes");
AssertEqual("1.5 KB", HumanReadableSizeFormatter.Format(1536), "kilobytes");
AssertEqual("2 MB", HumanReadableSizeFormatter.Format(2 * 1024 * 1024), "megabytes");

AssertEqual(true, FileFormatPolicy.IsSupported("photo.JPG"), "jpg extension");
AssertEqual(true, FileFormatPolicy.IsSupported(@"C:\Images\scan.tiff"), "tiff extension");
AssertEqual(true, FileFormatPolicy.IsRecognized("modern.webp"), "webp recognized");
AssertEqual(true, FileFormatPolicy.IsRecognized("phone.heic"), "heic recognized");
AssertEqual(true, FileFormatPolicy.IsRecognized("camera.avif"), "avif recognized");
AssertEqual(false, FileFormatPolicy.IsNativelyDecodable("phone.heic"), "heic not native");
AssertEqual(false, FileFormatPolicy.IsSupported("notes.txt"), "txt extension");

var layout = GalleryLayoutCalculator.Calculate(
    itemCount: 100,
    viewportWidth: 500,
    viewportHeight: 300,
    scrollX: 0,
    scrollY: 0,
    options: new GalleryLayoutOptions(ThumbnailSize: 100, TextAreaHeight: 44, Padding: 8, Gap: 12));

AssertEqual(4, layout.Columns, "layout columns");
AssertEqual(25, layout.Rows, "layout rows");
AssertEqual(0, layout.VisibleRange.FirstIndex, "first visible index");
AssertEqual(7, layout.VisibleRange.LastIndex, "last visible index");

var scrolledLayout = GalleryLayoutCalculator.Calculate(
    itemCount: 100,
    viewportWidth: 500,
    viewportHeight: 300,
    scrollX: 0,
    scrollY: 360,
    options: new GalleryLayoutOptions(ThumbnailSize: 100, TextAreaHeight: 44, Padding: 8, Gap: 12));

AssertEqual(8, scrolledLayout.VisibleRange.FirstIndex, "scrolled first visible index");

var largeLayout = GalleryLayoutCalculator.Calculate(
    itemCount: 10_000,
    viewportWidth: 1200,
    viewportHeight: 800,
    scrollX: 0,
    scrollY: 0,
    options: new GalleryLayoutOptions(ThumbnailSize: 128, TextAreaHeight: 58, Padding: 10, Gap: 14));

AssertEqual(7, largeLayout.Columns, "large layout columns");
AssertEqual(1429, largeLayout.Rows, "large layout rows");
AssertEqual(0, largeLayout.VisibleRange.FirstIndex, "large first visible index");
AssertEqual(true, largeLayout.VisibleRange.LastIndex < 100, "large visible range stays bounded");

var selection = new SelectionManager();
selection.Select(index: 2, itemCount: 10, ctrl: false, shift: false);
AssertSequence(new[] { 2 }, selection.SelectedIndexes, "single select");

selection.Select(index: 4, itemCount: 10, ctrl: true, shift: false);
AssertSequence(new[] { 2, 4 }, selection.SelectedIndexes, "ctrl add");

selection.Select(index: 7, itemCount: 10, ctrl: false, shift: true);
AssertSequence(new[] { 4, 5, 6, 7 }, selection.SelectedIndexes, "shift range");
AssertSequence(new[] { 7, 6, 5, 4 }, selection.GetSelectedIndexesDescending(), "delete order");

var evicted = new List<string>();
var cache = new LruCache<string, string>(capacity: 2, onEvicted: item => evicted.Add(item));
cache.Set("a", "A");
cache.Set("b", "B");
AssertEqual(true, cache.TryGet("a", out _), "cache hit");
cache.Set("c", "C");
AssertEqual(false, cache.ContainsKey("b"), "least recently used evicted");
AssertSequence(new[] { "B" }, evicted, "evicted value");

Console.WriteLine("All core tests passed.");
