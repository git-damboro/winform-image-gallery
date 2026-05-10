# WinForm Image Gallery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 6+ WinForms image browser that virtualizes thumbnail display, supports add/delete/multi-select/preview/style switching, and keeps development documentation current.

**Architecture:** The application is split into a small testable core library and a WinForms UI project. Core owns file format policy, size formatting, virtual layout math, selection state, and LRU cache behavior; the WinForms app owns image decoding, thumbnail rendering, dialogs, preview windows, and controls.

**Tech Stack:** C# 10, .NET 6 Windows Forms, SDK-style projects, no NuGet packages for the first implementation, custom console test runner to avoid package restore dependency.

---

## File Map

| Path | Responsibility |
|---|---|
| `src/ImageGallery.Core/ImageGallery.Core.csproj` | Testable core library project. |
| `src/ImageGallery.Core/Models/ImageItem.cs` | Immutable-ish image metadata model used by UI and services. |
| `src/ImageGallery.Core/Models/GalleryTypes.cs` | Shared enums and geometry records. |
| `src/ImageGallery.Core/Services/FileFormatPolicy.cs` | Supported image extension checks and file dialog filter text. |
| `src/ImageGallery.Core/Services/HumanReadableSizeFormatter.cs` | Byte size formatting for thumbnails. |
| `src/ImageGallery.Core/Services/GalleryLayoutCalculator.cs` | Virtualized row/column layout and visible item range calculation. |
| `src/ImageGallery.Core/Services/SelectionManager.cs` | Single, Ctrl, Shift, and delete selection logic. |
| `src/ImageGallery.Core/Services/LruCache.cs` | Bounded thumbnail cache with disposal callback. |
| `src/ImageGallery.App/ImageGallery.App.csproj` | WinForms executable project. |
| `src/ImageGallery.App/Program.cs` | Application startup. |
| `src/ImageGallery.App/Forms/MainForm.cs` | Main toolbar and gallery composition. |
| `src/ImageGallery.App/Forms/PreviewForm.cs` | Borderless large-image preview window. |
| `src/ImageGallery.App/Controls/ImageGalleryControl.cs` | Virtualized image gallery user control with scrollbars and canvas. |
| `src/ImageGallery.App/Rendering/GalleryRenderer.cs` | Default, Compact, and Crystal card drawing. |
| `src/ImageGallery.App/Services/ImageFileService.cs` | Adds files and reads metadata safely. |
| `src/ImageGallery.App/Services/ThumbnailService.cs` | Async thumbnail generation and image cache. |
| `tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj` | Console test project referencing the core library. |
| `tests/ImageGallery.Core.Tests/Program.cs` | Test runner with explicit assertions. |
| `docs/development.md` | Living development document updated during implementation. |

## Task 1: Project Skeleton and Core Tests

**Files:**
- Create: `src/ImageGallery.Core/ImageGallery.Core.csproj`
- Create: `tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj`
- Create: `tests/ImageGallery.Core.Tests/Program.cs`
- Modify: `docs/development.md`

- [ ] **Step 1: Write the failing tests**

Create a console test runner that references types not implemented yet:

```csharp
using ImageGallery.Core.Services;

static void AssertEqual<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
}

AssertEqual("512 B", HumanReadableSizeFormatter.Format(512), "bytes");
AssertEqual("1.5 KB", HumanReadableSizeFormatter.Format(1536), "kilobytes");
AssertEqual(true, FileFormatPolicy.IsSupported("photo.JPG"), "jpg extension");
AssertEqual(false, FileFormatPolicy.IsSupported("notes.txt"), "txt extension");

Console.WriteLine("All core tests passed.");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj`

Expected when SDK is available: compilation fails because `HumanReadableSizeFormatter` and `FileFormatPolicy` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create SDK projects and implement `HumanReadableSizeFormatter` and `FileFormatPolicy` in `ImageGallery.Core`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj`

Expected when SDK is available: process exits with code 0 and prints `All core tests passed.`

- [ ] **Step 5: Update development document**

Add a section describing the repository layout, SDK requirement, and current verification status.

## Task 2: Layout, Selection, and Cache Core

**Files:**
- Create: `src/ImageGallery.Core/Models/GalleryTypes.cs`
- Create: `src/ImageGallery.Core/Models/ImageItem.cs`
- Create: `src/ImageGallery.Core/Services/GalleryLayoutCalculator.cs`
- Create: `src/ImageGallery.Core/Services/SelectionManager.cs`
- Create: `src/ImageGallery.Core/Services/LruCache.cs`
- Modify: `tests/ImageGallery.Core.Tests/Program.cs`
- Modify: `docs/development.md`

- [ ] **Step 1: Write the failing tests**

Add tests for visible range calculation, Ctrl/Shift selection, selected deletion order, and LRU eviction.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj`

Expected when SDK is available: compilation fails because the new core types do not exist.

- [ ] **Step 3: Write minimal implementation**

Implement deterministic layout math and selection/cache types without WinForms dependencies.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj`

Expected when SDK is available: all core tests pass.

- [ ] **Step 5: Update development document**

Document the virtualization formula, selection rules, and cache disposal behavior.

## Task 3: WinForms App and Main UI

**Files:**
- Create: `src/ImageGallery.App/ImageGallery.App.csproj`
- Create: `src/ImageGallery.App/Program.cs`
- Create: `src/ImageGallery.App/Forms/MainForm.cs`
- Create: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `docs/development.md`

- [ ] **Step 1: Create the app project**

Use an SDK-style WinForms project targeting `net6.0-windows` with `<UseWindowsForms>true</UseWindowsForms>`.

- [ ] **Step 2: Implement `Program` and `MainForm`**

Create a top toolbar with add, delete, thumbnail-size trackbar, style selector, and count label.

- [ ] **Step 3: Implement `ImageGalleryControl` shell**

Add vertical and horizontal scrollbars, item source binding, selection hooks, and layout invalidation.

- [ ] **Step 4: Verify compile**

Run: `dotnet build src/ImageGallery.App/ImageGallery.App.csproj`

Expected when SDK is available: build succeeds.

- [ ] **Step 5: Update development document**

Document UI composition and manual verification steps.

## Task 4: File Add/Delete and Thumbnail Rendering

**Files:**
- Create: `src/ImageGallery.App/Services/ImageFileService.cs`
- Create: `src/ImageGallery.App/Services/ThumbnailService.cs`
- Create: `src/ImageGallery.App/Rendering/GalleryRenderer.cs`
- Modify: `src/ImageGallery.App/Forms/MainForm.cs`
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `docs/development.md`

- [ ] **Step 1: Implement safe file import**

Use `OpenFileDialog.Multiselect = true` and `FileFormatPolicy.FileDialogFilter`; read file size and image dimensions in isolated `try` blocks.

- [ ] **Step 2: Implement thumbnail service**

Generate thumbnails asynchronously, cap cache size, dispose evicted images, and return error placeholders on failure.

- [ ] **Step 3: Implement renderer**

Draw thumbnail image, metadata text, selection state, and `Default`/`Compact`/`Crystal` styles.

- [ ] **Step 4: Wire delete behavior**

Delete selected items from the in-memory list only, then update scrollbars and count label.

- [ ] **Step 5: Verify compile**

Run: `dotnet build src/ImageGallery.App/ImageGallery.App.csproj`

Expected when SDK is available: build succeeds.

- [ ] **Step 6: Update development document**

Document import behavior, supported formats, cache limit, and known decoder limitations.

## Task 5: Preview, Polish, and Final Verification

**Files:**
- Create: `src/ImageGallery.App/Forms/PreviewForm.cs`
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `src/ImageGallery.App/Forms/MainForm.cs`
- Modify: `docs/development.md`

- [ ] **Step 1: Implement preview form**

Create a borderless preview form with a `PictureBox`, fit-to-window rendering, and safe load failure display.

- [ ] **Step 2: Wire hover and double-click preview**

Open preview on hover or double-click and close it when the mouse leaves the active thumbnail region.

- [ ] **Step 3: Verify full solution**

Run: `dotnet run --project tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj`

Expected when SDK is available: all core tests pass.

Run: `dotnet build src/ImageGallery.App/ImageGallery.App.csproj`

Expected when SDK is available: build succeeds.

- [ ] **Step 4: Update development document**

Record final implemented feature list, verification commands, SDK blocker if still present, and manual test checklist.

## Self-Review

| Spec Requirement | Covered By |
|---|---|
| Multi-image thumbnail grid | Tasks 2, 3, 4 |
| Metadata under thumbnail | Tasks 1, 4 |
| Add/delete, scrollbars | Tasks 3, 4 |
| Trackbar thumbnail sizing | Task 3 |
| Hover/double-click preview | Task 5 |
| Single and multi-select delete | Tasks 2, 4 |
| Multi-image add | Task 4 |
| Crystal style | Task 4 |
| 10,000-image performance | Tasks 2, 3, 4 |
| Large image robustness | Tasks 4, 5 |
| Mainstream format policy | Tasks 1, 4 |
| Low memory and exception handling | Tasks 2, 4, 5 |
| Development document maintenance | Every task |
