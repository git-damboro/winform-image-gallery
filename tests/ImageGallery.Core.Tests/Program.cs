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

var sessionDirectory = Path.Combine(Path.GetTempPath(), "ImageGallery.Core.Tests", Guid.NewGuid().ToString("N"));
var sessionFile = Path.Combine(sessionDirectory, "session.json");
var sessionStore = new GallerySessionStore();
sessionStore.Save(sessionFile, new[] { @"C:\Images\a.jpg", "", @"C:\Images\a.jpg", @"C:\Images\b.png" });
AssertSequence(new[] { @"C:\Images\a.jpg", @"C:\Images\b.png" }, sessionStore.Load(sessionFile), "session paths round trip");

File.WriteAllText(sessionFile, "{not-json");
AssertSequence(Array.Empty<string>(), sessionStore.Load(sessionFile), "bad session file ignored");

var styleValues = ThumbnailStyleCatalog.All.Select(style => style.Value).ToArray();
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Default), "default style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Rounded), "rounded style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Shadow), "shadow style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Border), "border style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Polaroid), "polaroid style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Glass), "glass style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Crystal), "crystal style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Neon), "neon style exists");
AssertEqual(true, styleValues.Contains(GalleryDisplayStyle.Minimal), "minimal style exists");
AssertEqual(9, styleValues.Distinct().Count(), "style count");

AssertEqual(GalleryDisplayStyle.Minimal, ThumbnailStyleCatalog.ResolveVisibleStyle(GalleryDisplayStyle.Compact), "compact resolves to minimal");

var compactStyle = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Compact);
var minimalStyle = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Minimal);
AssertEqual(GalleryDisplayStyle.Compact, compactStyle.Value, "compact fallback value");
AssertEqual(compactStyle.CardVisualProfile, minimalStyle.CardVisualProfile, "compact fallback profile matches minimal");

var glassProfile = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Glass).CardVisualProfile;
var crystalProfile = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Crystal).CardVisualProfile;
var shadowProfile = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Shadow).CardVisualProfile;
var selectionProfile = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Crystal).SelectionVisualProfile;

AssertEqual(
    true,
    crystalProfile.BorderGlowAlpha > glassProfile.BorderGlowAlpha,
    "crystal glow stronger than glass");
AssertEqual(
    true,
    crystalProfile.BorderAlpha > glassProfile.BorderAlpha,
    "crystal border opacity stronger than glass");
AssertEqual(
    true,
    shadowProfile.ShadowAlpha > glassProfile.ShadowAlpha,
    "shadow stronger than glass");
AssertEqual(
    true,
    shadowProfile.ShadowBlurPx > crystalProfile.ShadowBlurPx,
    "shadow blur stronger than crystal");
AssertEqual(0, selectionProfile.BorderRadius, "selected card uses square border");
AssertEqual(2, selectionProfile.BorderThickness, "selected card border is thicker");
AssertEqual(unchecked((int)0x4A184A96), selectionProfile.OverlayArgb, "selected card overlay is deeper blue");
AssertEqual(unchecked((int)0xFF2A71DC), selectionProfile.BorderArgb, "selected card border is bright blue");

sessionStore.Save(sessionFile, new[] { @"C:\Images\a.jpg" }, GalleryDisplayStyle.Crystal);
var savedState = sessionStore.LoadState(sessionFile);
AssertEqual(GalleryDisplayStyle.Crystal, savedState.DisplayStyle, "saved style round trip");

var imageItem = new ImageItem(@"C:\Images\sample-photo.jpg", "sample-photo.jpg", 1536, ".jpg", 640, 480);
AssertSequence(
    Array.Empty<string>(),
    ThumbnailInfoFormatter.GetLines(imageItem, ThumbnailInfoFields.None),
    "image only has no info lines");
AssertSequence(
    new[] { "sample-photo.jpg", "1.5 KB", "JPG", "640 x 480" },
    ThumbnailInfoFormatter.GetLines(imageItem, ThumbnailInfoFields.All),
    "all thumbnail info lines");
AssertSequence(
    new[] { "sample-photo.jpg", "JPG" },
    ThumbnailInfoFormatter.GetLines(imageItem, ThumbnailInfoFields.FileName | ThumbnailInfoFields.ImageType),
    "selected thumbnail info lines");

AssertEqual("就绪", TaskProgressFormatter.FormatIdle(), "idle task text");
AssertEqual("正在添加图片 3/10", TaskProgressFormatter.Format("正在添加图片", 3, 10), "active task text");
AssertEqual("正在恢复图片列表", TaskProgressFormatter.Format("正在恢复图片列表", 0, 0), "unknown total task text");

var progressPolicy = new TaskProgressUpdatePolicy(total: 10_000, minimumStep: 100);
AssertEqual(true, progressPolicy.ShouldReport(1), "first progress update is reported");
AssertEqual(false, progressPolicy.ShouldReport(50), "small progress updates are skipped");
AssertEqual(true, progressPolicy.ShouldReport(101), "progress update after minimum step is reported");
AssertEqual(true, progressPolicy.ShouldReport(10_000), "final progress update is reported");

var filterItems = new[]
{
    new ImageItem(@"C:\Images\a.png", "a.png", 100, ".png"),
    new ImageItem(@"C:\Images\b.jpg", "b.jpg", 100, ".jpg"),
    new ImageItem(@"C:\Images\c.PNG", "c.PNG", 100, ".PNG")
};

var filtered = ImageFilterPolicy.FilterByExtensions(filterItems, new[] { ".png" });
AssertEqual(2, filtered.Count, "png filter count");

var multiFiltered = ImageFilterPolicy.FilterByExtensions(filterItems, new[] { ".png", ".jpg" });
AssertEqual(3, multiFiltered.Count, "multi extension filter count");

var filterSelection = new SelectionManager();
filterSelection.SelectAll(filtered.Count);
AssertSequence(new[] { 1, 0 }, filterSelection.GetSelectedIndexesDescending(), "select all filtered items");

AssertEqual(1, PreviewNavigationPolicy.Move(0, 3, 1), "next index");
AssertEqual(2, PreviewNavigationPolicy.Move(0, 3, -1), "previous wraps");
AssertEqual(-1, PreviewNavigationPolicy.Move(0, 0, 1), "empty list");

Console.WriteLine("All core tests passed.");
