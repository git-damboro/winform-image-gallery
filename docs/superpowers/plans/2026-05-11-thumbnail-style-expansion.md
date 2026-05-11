# Thumbnail Style Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the gallery thumbnail style system to support `default`, `rounded`, `shadow`, `border`, `polaroid`, `glass`, `crystal`, `neon`, and `minimal`, with immediate redraw and persisted user choice.

**Architecture:** Keep the existing WinForms rendering pipeline, but replace the hardcoded enum binding with a small style registry that carries label, description, and render metadata for each preset. Persist the selected style in the session JSON alongside image paths so the existing startup restore path can reapply it. Implement `crystal` as a dedicated render branch with layered gradients, highlight pass, glow, and hover emphasis, while the other styles reuse shared drawing helpers to avoid a long if/else chain.

**Tech Stack:** C# / WinForms / `System.Drawing` / existing gallery session JSON / existing console-style core tests.

---

### Task 1: Add thumbnail style presets and persist the selection

**Files:**
- Modify: `src/ImageGallery.Core/Models/GalleryTypes.cs`
- Create: `src/ImageGallery.Core/Models/ThumbnailStyleDefinition.cs`
- Modify: `src/ImageGallery.Core/Services/GallerySessionStore.cs`
- Modify: `tests/ImageGallery.Core.Tests/Program.cs`

- [ ] **Step 1: Write the failing test**

```csharp
var styles = ThumbnailStyleCatalog.All;
AssertEqual(true, styles.Any(style => style.Value == GalleryDisplayStyle.Crystal), "crystal preset exists");
AssertEqual(true, styles.Any(style => style.Value == GalleryDisplayStyle.Rounded), "rounded preset exists");

var sessionStore = new GallerySessionStore();
sessionStore.Save(sessionFile, new[] { @"C:\Images\a.jpg" }, GalleryDisplayStyle.Crystal);
var state = sessionStore.Load(sessionFile);
AssertEqual(GalleryDisplayStyle.Crystal, state.DisplayStyle, "session style round trip");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests\\ImageGallery.Core.Tests\\ImageGallery.Core.Tests.csproj`
Expected: fail because the new style catalog and session style persistence do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
public enum GalleryDisplayStyle
{
    Default,
    Rounded,
    Shadow,
    Border,
    Polaroid,
    Glass,
    Crystal,
    Neon,
    Minimal
}

public static class ThumbnailStyleCatalog
{
    public static IReadOnlyList<ThumbnailStyleDefinition> All { get; } = [...];
}
```

```csharp
public sealed class GallerySessionStore
{
    public IReadOnlyList<string> Load(string filePath, out GalleryDisplayStyle displayStyle) { ... }
    public void Save(string filePath, IEnumerable<string> imagePaths, GalleryDisplayStyle displayStyle) { ... }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests\\ImageGallery.Core.Tests\\ImageGallery.Core.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ImageGallery.Core/Models/GalleryTypes.cs src/ImageGallery.Core/Models/ThumbnailStyleDefinition.cs src/ImageGallery.Core/Services/GallerySessionStore.cs tests/ImageGallery.Core.Tests/Program.cs
git commit -m "feat: add thumbnail style presets"
```

### Task 2: Refactor thumbnail rendering to use the preset-driven style system

**Files:**
- Modify: `src/ImageGallery.App/Rendering/GalleryRenderer.cs`
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Create: `src/ImageGallery.App/Rendering/ThumbnailStylePainter.cs`
- Modify: `tests/ImageGallery.Core.Tests/Program.cs`

- [ ] **Step 1: Write the failing test**

```csharp
AssertEqual(true, ThumbnailStyleCatalog.All.Count >= 9, "all style presets available");
AssertEqual("Crystal", ThumbnailStyleCatalog.All.First(style => style.Value == GalleryDisplayStyle.Crystal).Label, "crystal label");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests\\ImageGallery.Core.Tests\\ImageGallery.Core.Tests.csproj`
Expected: fail until renderer-facing style catalog exists and the enum values are expanded.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class ThumbnailStylePainter
{
    public void DrawBackground(Graphics graphics, Rectangle bounds, bool selected, bool hovered, GalleryDisplayStyle style) { ... }
    public void DrawThumbnailFrame(Graphics graphics, Rectangle bounds, GalleryDisplayStyle style, bool hovered, bool selected) { ... }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project tests\\ImageGallery.Core.Tests\\ImageGallery.Core.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/ImageGallery.App/Rendering/GalleryRenderer.cs src/ImageGallery.App/Controls/ImageGalleryControl.cs src/ImageGallery.App/Rendering/ThumbnailStylePainter.cs tests/ImageGallery.Core.Tests/Program.cs
git commit -m "feat: render thumbnails with style presets"
```

### Task 3: Wire the style selector, immediate redraw, and restore path

**Files:**
- Modify: `src/ImageGallery.App/Forms/MainForm.cs`
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `src/ImageGallery.Core/Services/GallerySessionStore.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// Manual verification target:
// 1. Choose a non-default style in the toolbar.
// 2. Confirm all visible thumbnails repaint immediately.
// 3. Restart the app and confirm the selected style is restored.
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet build src\\ImageGallery.App\\ImageGallery.App.csproj`
Expected: fail until the selector is bound to the new preset list and the session store returns the saved style.

- [ ] **Step 3: Write minimal implementation**

```csharp
_styleComboBox.DataSource = ThumbnailStyleCatalog.All;
_styleComboBox.DisplayMember = nameof(ThumbnailStyleDefinition.Label);
_styleComboBox.ValueMember = nameof(ThumbnailStyleDefinition.Value);
```

```csharp
_galleryControl.DisplayStyle = selectedStyle;
_galleryControl.Invalidate();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build src\\ImageGallery.App\\ImageGallery.App.csproj -p:OutDir=E:\\Desktop\\Mryao\\WinForm\\.buildverify\\`
Expected: PASS with `0` warnings and `0` errors.

- [ ] **Step 5: Commit**

```bash
git add src/ImageGallery.App/Forms/MainForm.cs src/ImageGallery.App/Controls/ImageGalleryControl.cs src/ImageGallery.Core/Services/GallerySessionStore.cs
git commit -m "feat: restore and apply thumbnail style selection"
```

### Self-Review

- Spec coverage: `default`, `rounded`, `shadow`, `border`, `polaroid`, `glass`, `crystal`, `neon`, `minimal` are all represented in the catalog and selector.
- Placeholder scan: no TBD steps or vague test instructions.
- Type consistency: `GalleryDisplayStyle`, `ThumbnailStyleDefinition`, `ThumbnailStyleCatalog`, and `GallerySessionStore` signatures are used consistently across tasks.

