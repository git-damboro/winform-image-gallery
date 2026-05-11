# Thumbnail Selection Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a top-level select-all control, fix `Ctrl+A` to select the currently visible images, and add a display-only filter for image types such as PNG without reloading data.

**Architecture:** Keep the source image list unchanged in `MainForm` and add a separate display filter state that only affects the gallery control's visible projection. Extend `ImageGalleryControl` so selection and hit-testing operate on the visible subset, while the underlying data list remains the full imported session. The new top toolbar controls only mutate filter/selection state; they do not touch persistence or image loading.

**Tech Stack:** WinForms, .NET 8, existing core service classes, existing gallery rendering pipeline.

---

### Task 1: Add core coverage for visible filtering and select-all semantics

**Files:**
- Modify: `tests/ImageGallery.Core.Tests/Program.cs`
- Modify: `src/ImageGallery.Core/Models/GalleryTypes.cs`
- Modify: `src/ImageGallery.Core/Services/SelectionManager.cs`
- Create: `src/ImageGallery.Core/Services/ImageFilterPolicy.cs`

- [ ] **Step 1: Write the failing test**

```csharp
var visible = ImageFilterPolicy.FilterByExtensions(items, new[] { ".png" });
AssertEqual(2, visible.Count, "png filter count");

var selection = new SelectionManager();
selection.SelectAll(visible.Count);
AssertSequence(new[] { 1, 0 }, selection.GetSelectedIndexesDescending(), "select all visible items");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj`
Expected: fail because `ImageFilterPolicy` and `SelectAll` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public static class ImageFilterPolicy
{
    public static IReadOnlyList<ImageItem> FilterByExtensions(IEnumerable<ImageItem> items, IReadOnlyCollection<string> extensions)
    {
        // normalize extensions and return matching items
    }
}
```

```csharp
public void SelectAll(int itemCount)
{
    _selectedIndexes.Clear();
    for (var i = 0; i < itemCount; i++) _selectedIndexes.Add(i);
    _anchorIndex = itemCount > 0 ? 0 : null;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj`
Expected: `All core tests passed.`

---

### Task 2: Add visible-filter state to the gallery control

**Files:**
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `src/ImageGallery.App/Rendering/GalleryRenderer.cs`

- [ ] **Step 1: Write the failing test**

Add a small test for visible item mapping if needed in the core test harness, or verify through control-level build if no unit seam exists.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj`
Expected: pass for existing core tests, but control code still lacks the new filter API.

- [ ] **Step 3: Write minimal implementation**

```csharp
public IReadOnlyCollection<string> VisibleExtensions
{
    get => _visibleExtensions;
    set
    {
        _visibleExtensions = NormalizeExtensions(value);
        RecalculateLayout();
    }
}
```

```csharp
private IReadOnlyList<ImageItem> GetVisibleItems()
{
    return ImageFilterPolicy.FilterByExtensions(_items, _visibleExtensions);
}
```

Update painting, hit testing, and `Ctrl+A` handling to work off the visible subset only.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build src\ImageGallery.App\ImageGallery.App.csproj -p:OutDir=E:\Desktop\Mryao\WinForm\.buildverify\`
Expected: build succeeds.

---

### Task 3: Add top toolbar select-all and type filter controls

**Files:**
- Modify: `src/ImageGallery.App/Forms/MainForm.cs`

- [ ] **Step 1: Write the failing test**

No new automated UI test harness exists here; verify by build plus manual behavior after implementation.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet build src\ImageGallery.App\ImageGallery.App.csproj -p:OutDir=E:\Desktop\Mryao\WinForm\.buildverify\`
Expected: build currently fails because the new controls and handlers are missing.

- [ ] **Step 3: Write minimal implementation**

```csharp
_selectAllButton.Click += (_, _) => _galleryControl.SelectAllVisible();
_typeFilterDropDown.SelectedIndexChanged += (_, _) => _galleryControl.VisibleExtensions = GetSelectedExtensions();
```

Add a `Select All` button near the existing toolbar controls, and add a dropdown for extensions like `All`, `PNG`, `JPG`, `JPEG`, `BMP`, `GIF`, `TIFF`, `ICO`, `WEBP`, `HEIC`, `HEIF`, `AVIF`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build src\ImageGallery.App\ImageGallery.App.csproj -p:OutDir=E:\Desktop\Mryao\WinForm\.buildverify\`
Expected: `0` warnings, `0` errors.

---

### Task 4: Verify selection and filtering behavior end to end

**Files:**
- Modify: `tests/ImageGallery.Core.Tests/Program.cs`
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `src/ImageGallery.App/Forms/MainForm.cs`

- [ ] **Step 1: Write the failing test**

Add a regression assertion that `Ctrl+A` applies only to visible items after a filter is set.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj`
Expected: fail until the visible-item selection behavior is wired through.

- [ ] **Step 3: Write minimal implementation**

Keep selection indices relative to the visible list for the gallery control, while the main form continues to own the full imported session.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj`
Expected: `All core tests passed.`

---

### Task 5: Build verification and cleanup

**Files:**
- Modify: none

- [ ] **Step 1: Run the application build**

Run: `dotnet build src\ImageGallery.App\ImageGallery.App.csproj -p:OutDir=E:\Desktop\Mryao\WinForm\.buildverify\`

- [ ] **Step 2: Confirm output**

Expected: build succeeds with `0` errors.

- [ ] **Step 3: Remove temporary build output**

Delete the temporary `.buildverify` directory if present.
