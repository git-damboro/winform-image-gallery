using System.Reflection;
using ImageGallery.App.Controls;
using ImageGallery.App.Forms;
using ImageGallery.App.Services;
using ImageGallery.Core.Models;

static void AssertTrue(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{name}: expected true");
    }
}

var ensureMethod = typeof(MainForm).GetMethod(
    "EnsurePreviewForm",
    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

if (ensureMethod is null)
{
    throw new InvalidOperationException("EnsurePreviewForm method was not found.");
}

var created = (PreviewForm?)ensureMethod.Invoke(null, new object?[] { null });
AssertTrue(created is not null, "creates preview form when missing");
AssertTrue(!created!.IsDisposed, "created preview form is alive");

var reused = (PreviewForm?)ensureMethod.Invoke(null, new object?[] { created });
AssertTrue(ReferenceEquals(created, reused), "reuses existing live preview form");

created.Dispose();

var replaced = (PreviewForm?)ensureMethod.Invoke(null, new object?[] { created });
AssertTrue(replaced is not null, "recreates preview form when disposed");
AssertTrue(!ReferenceEquals(created, replaced), "disposed preview form is replaced");
AssertTrue(!replaced!.IsDisposed, "replacement preview form is alive");

replaced.Dispose();

var createInputsMethod = typeof(MainForm).GetMethod(
    "CreateGalleryInputs",
    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

if (createInputsMethod is null)
{
    throw new InvalidOperationException("CreateGalleryInputs method was not found.");
}

var inputs = (IEnumerable<GalleryImageInput>?)createInputsMethod.Invoke(
    null,
    new object?[] { new[] { @"C:\Images\a.jpg", @"C:\Images\b.png" } });

var inputArray = inputs?.ToArray() ?? Array.Empty<GalleryImageInput>();
AssertTrue(inputArray.Length == 2, "creates one gallery input per path");
AssertTrue(inputArray.All(input => input.ContentInfo.MaxObjectDiameter.HasValue), "simulates diameter values");
AssertTrue(inputArray.All(input => input.ContentInfo.MaxObjectArea.HasValue), "simulates area values");
AssertTrue(inputArray.All(input => !string.IsNullOrWhiteSpace(input.ContentInfo.SizeSpec)), "simulates size spec values");

var hoverSelection = new ImageSelectedEventArgs(@"C:\Images\a.jpg", ImageSelectionMode.HoverPreview);
AssertTrue(hoverSelection.Mode == ImageSelectionMode.HoverPreview, "hover selection mode is preserved");

var pinnedSelection = new ImageSelectedEventArgs(@"C:\Images\a.jpg", ImageSelectionMode.PinnedOpen);
AssertTrue(pinnedSelection.Mode == ImageSelectionMode.PinnedOpen, "pinned selection mode is preserved");

var formatScaleTextMethod = typeof(MainForm).GetMethod(
    "FormatScaleText",
    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

if (formatScaleTextMethod is null)
{
    throw new InvalidOperationException("FormatScaleText method was not found.");
}

var scaleText = (string?)formatScaleTextMethod.Invoke(null, new object?[] { 1.25f });
AssertTrue(scaleText == "1.25x", "formats scale text with multiplier suffix");

var asyncLoadMethods = typeof(ImageGalleryControl)
    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
    .Where(method => method.Name == "LoadImagesAsync")
    .ToArray();

if (asyncLoadMethods.Length == 0)
{
    throw new InvalidOperationException("LoadImagesAsync method was not found.");
}

AssertTrue(asyncLoadMethods.All(method => typeof(Task).IsAssignableFrom(method.ReturnType)), "control exposes async import task");

var computeUiBatchSizeMethod = typeof(ImageGalleryControl).GetMethod(
    "ComputeUiBatchSize",
    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

if (computeUiBatchSizeMethod is null)
{
    throw new InvalidOperationException("ImageGalleryControl.ComputeUiBatchSize method was not found.");
}

var largeBatchSize = (int?)computeUiBatchSizeMethod.Invoke(null, new object?[] { 10_000 });
AssertTrue(largeBatchSize == 209, "10k items coalesce to about dozens of UI refreshes");

using var control = new ImageGalleryControl();
control.Width = 1200;
control.Height = 800;

var initialViewportBatchMethod = typeof(ImageGalleryControl).GetMethod(
    "ComputeInitialViewportBatchSize",
    BindingFlags.Instance | BindingFlags.NonPublic);

if (initialViewportBatchMethod is null)
{
    throw new InvalidOperationException("ComputeInitialViewportBatchSize method was not found.");
}

var initialViewportBatchSize = (int?)initialViewportBatchMethod.Invoke(control, Array.Empty<object>());
AssertTrue(initialViewportBatchSize.HasValue && initialViewportBatchSize.Value > 0, "viewport-first batch is positive");
AssertTrue(initialViewportBatchSize.HasValue && initialViewportBatchSize.GetValueOrDefault() < 500, "viewport-first batch stays small");

EventHandler? previewCloseHandler = (_, _) => { };
control.PreviewCloseRequested += previewCloseHandler;
control.PreviewCloseRequested -= previewCloseHandler;

var service = new ImageFileService();
var progressEvents = new List<ImageImportProgress>();
var progress = new CollectingProgress(progressEvents);
var serviceItems = await service.CreateItemsAsync(
    new[]
    {
        new GalleryImageInput(
            @"C:\Images\missing-a.jpg",
            new ImageContentInfo(12.5, 88.2, "S1-01")),
        new GalleryImageInput(
            @"C:\Images\missing-b.jpg",
            new ImageContentInfo(15.0, 120.0, "S2-02"))
    },
    progress).ConfigureAwait(false);

AssertTrue(serviceItems.Count == 2, "service creates one item per gallery input");
AssertTrue(serviceItems[0].ContentInfo.MaxObjectDiameter == 12.5, "service preserves diameter");
AssertTrue(serviceItems[0].ContentInfo.MaxObjectArea == 88.2, "service preserves area");
AssertTrue(serviceItems[0].ContentInfo.SizeSpec == "S1-01", "service preserves size spec");
AssertTrue(progressEvents.Count == 2, "service reports progress for each imported item");
AssertTrue(progressEvents.All(value => value.Item is not null), "service progress carries imported item");

Console.WriteLine("All app tests passed.");

sealed class CollectingProgress(List<ImageImportProgress> target) : IProgress<ImageImportProgress>
{
    public void Report(ImageImportProgress value)
    {
        target.Add(value);
    }
}
