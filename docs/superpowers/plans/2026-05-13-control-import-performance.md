# ImageGalleryControl Import Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move high-volume import performance into `ImageGalleryControl` public APIs and implement true viewport-first loading before background backlog processing.

**Architecture:** Keep `ImageGalleryControl` as the single integration surface for host applications. The control will expose an async import API that accepts `GalleryImageInput` sequences, computes a viewport-first batch from current layout capacity, appends only the first visible batch immediately, and continues importing the backlog in merged UI batches on the UI thread. `MainForm` will become a thin demo host that calls the control API instead of owning the batching strategy.

**Tech Stack:** C# / WinForms / .NET / existing `ImageFileService`, `ThumbnailService`, `GalleryLayoutCalculator`

---

## File Map

| File | Action | Responsibility |
| --- | --- | --- |
| `src/ImageGallery.App/Controls/ImageGalleryControl.cs` | Modify | Add async public import API, viewport-first scheduling, UI-batch append path, import cancellation/lifecycle |
| `src/ImageGallery.App/Services/ImageFileService.cs` | Modify | Support chunk-oriented item creation without forcing per-item UI progress |
| `src/ImageGallery.Core/Models/GalleryTypes.cs` | Modify | Add small import option types only if needed for public API clarity |
| `src/ImageGallery.App/Forms/MainForm.cs` | Modify | Replace demo-local batching with control API calls |
| `tests/ImageGallery.App.Tests/Program.cs` | Modify | Add reflection/smoke tests for new control API and viewport-first batch policy |

## Design Decisions

| Topic | Decision |
| --- | --- |
| Public entry point | Add `LoadImagesAsync(...)` on `ImageGalleryControl` instead of requiring host code to use `MainForm` logic |
| Backward compatibility | Keep existing `LoadImages(...)` and `LoadItems(...)` for synchronous/simple scenarios |
| First-screen policy | Compute initial batch from current viewport rows x columns, with a small floor to avoid underfilling before first paint |
| Background continuation | Continue import in chunked batches and merge to UI every chunk, not every item |
| Duplicate handling | Reuse path de-duplication inside control, not in the demo form |
| Cancellation | Replace/clear/dispose should cancel in-flight import |

## Task 1: Define the Control-Level Import Surface

**Files:**
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `src/ImageGallery.Core/Models/GalleryTypes.cs` if an options type is introduced
- Test: `tests/ImageGallery.App.Tests/Program.cs`

- [ ] **Step 1: Write the failing test for the new public API**

Add a reflection-based test in `tests/ImageGallery.App.Tests/Program.cs` that requires a public async import method on the control:

```csharp
var asyncLoadMethod = typeof(ImageGalleryControl).GetMethod(
    "LoadImagesAsync",
    BindingFlags.Instance | BindingFlags.Public);

AssertTrue(asyncLoadMethod is not null, "control exposes LoadImagesAsync");
AssertTrue(typeof(Task).IsAssignableFrom(asyncLoadMethod!.ReturnType), "LoadImagesAsync returns Task");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: FAIL with `"control exposes LoadImagesAsync"` or equivalent missing-method error.

- [ ] **Step 3: Add the minimal public API to the control**

Add a public async method to `src/ImageGallery.App/Controls/ImageGalleryControl.cs` with this shape:

```csharp
public Task LoadImagesAsync(
    IEnumerable<GalleryImageInput> inputs,
    LoadMode mode,
    CancellationToken cancellationToken = default)
{
    throw new NotImplementedException();
}
```

If the method needs import options later, add an overload instead of breaking this simple host-facing signature.

- [ ] **Step 4: Run test to verify the API now exists**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: the missing-method assertion passes, later tests may still fail.

- [ ] **Step 5: Commit**

```bash
git add tests/ImageGallery.App.Tests/Program.cs src/ImageGallery.App/Controls/ImageGalleryControl.cs src/ImageGallery.Core/Models/GalleryTypes.cs
git commit -m "feat(control): add async image loading api"
```

## Task 2: Move Batch Import Ownership into the Control

**Files:**
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Modify: `src/ImageGallery.App/Services/ImageFileService.cs`
- Test: `tests/ImageGallery.App.Tests/Program.cs`

- [ ] **Step 1: Write the failing test for chunk coalescing policy**

Add a reflection test that requires a private helper responsible for chunk sizing:

```csharp
var batchMethod = typeof(ImageGalleryControl).GetMethod(
    "ComputeUiBatchSize",
    BindingFlags.Static | BindingFlags.NonPublic);

AssertTrue(batchMethod is not null, "control has batch size helper");
var largeBatch = (int?)batchMethod!.Invoke(null, new object?[] { 10_000 });
AssertTrue(largeBatch == 209, "10k items coalesce to about dozens of control-level refreshes");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: FAIL with `"control has batch size helper"`.

- [ ] **Step 3: Implement chunked import inside the control**

In `src/ImageGallery.App/Controls/ImageGalleryControl.cs`, introduce private control-owned import helpers:

```csharp
private CancellationTokenSource? _importCts;
private readonly ImageFileService _imageFileService = new();

private static int ComputeUiBatchSize(int total)
{
    const int targetRefreshes = 48;
    if (total <= 0)
    {
        return 1;
    }

    return Math.Max(1, (int)Math.Ceiling(total / (double)targetRefreshes));
}
```

And make `LoadImagesAsync(...)` own this pattern:

```csharp
CancelActiveImport();

if (mode == LoadMode.Replace)
{
    _thumbnailService.CancelPrefetch();
    _items.Clear();
    _selectionManager.Clear();
    _hoverIndex = -1;
    RebuildVisibleItems(clearSelection: true);
}

var inputArray = inputs as GalleryImageInput[] ?? inputs.ToArray();
var batchSize = ComputeUiBatchSize(inputArray.Length);

for (var offset = 0; offset < inputArray.Length; offset += batchSize)
{
    cancellationToken.ThrowIfCancellationRequested();
    var count = Math.Min(batchSize, inputArray.Length - offset);
    var batch = new ArraySegment<GalleryImageInput>(inputArray, offset, count);
    var items = await _imageFileService.CreateItemsAsync(batch, progress: null, cancellationToken).ConfigureAwait(false);
    await AppendItemsOnUiThreadAsync(items, offset == 0 && mode == LoadMode.Replace ? LoadMode.Append : LoadMode.Append).ConfigureAwait(false);
}
```

Keep all `_items` mutation on the UI thread. Do not mutate `_items` from the background import thread.

- [ ] **Step 4: Run tests**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: new control-level batch helper test passes; any remaining failures are now related to viewport-first behavior.

- [ ] **Step 5: Commit**

```bash
git add src/ImageGallery.App/Controls/ImageGalleryControl.cs src/ImageGallery.App/Services/ImageFileService.cs tests/ImageGallery.App.Tests/Program.cs
git commit -m "feat(control): own batch import inside control"
```

## Task 3: Implement Viewport-First Initial Load

**Files:**
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Test: `tests/ImageGallery.App.Tests/Program.cs`

- [ ] **Step 1: Write the failing test for first-screen sizing**

Add a reflection test for a helper that determines initial visible-batch size:

```csharp
var firstBatchMethod = typeof(ImageGalleryControl).GetMethod(
    "ComputeInitialViewportBatchSize",
    BindingFlags.Instance | BindingFlags.NonPublic);

AssertTrue(firstBatchMethod is not null, "control has viewport-first batch helper");
```

Then instantiate the control, set a deterministic size, and assert the returned size is bounded and positive:

```csharp
using var control = new ImageGalleryControl
{
    Width = 1200,
    Height = 800
};

var firstBatch = (int?)firstBatchMethod!.Invoke(control, Array.Empty<object>());
AssertTrue(firstBatch.HasValue && firstBatch.Value > 0, "viewport-first batch is positive");
AssertTrue(firstBatch.Value < 500, "viewport-first batch stays small");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: FAIL with `"control has viewport-first batch helper"`.

- [ ] **Step 3: Implement viewport-first batch calculation**

In `src/ImageGallery.App/Controls/ImageGalleryControl.cs`, add a helper that uses current layout options and current control size:

```csharp
private int ComputeInitialViewportBatchSize()
{
    var options = CreateLayoutOptions();
    var viewportWidth = Math.Max(1, _canvas.Width > 0 ? _canvas.Width : Width);
    var viewportHeight = Math.Max(1, _canvas.Height > 0 ? _canvas.Height : Height);

    var columns = Math.Max(1, (viewportWidth + options.Gap) / options.StepX);
    var rows = Math.Max(1, (viewportHeight + options.Gap) / options.StepY);

    return Math.Max(columns * rows, columns * 2);
}
```

Use this helper in `LoadImagesAsync(...)`:

```csharp
var firstBatchSize = Math.Min(inputArray.Length, ComputeInitialViewportBatchSize());
var firstBatchInputs = inputArray.AsSpan(0, firstBatchSize).ToArray();
var firstBatchItems = await _imageFileService.CreateItemsAsync(firstBatchInputs, progress: null, cancellationToken).ConfigureAwait(false);
await AppendItemsOnUiThreadAsync(firstBatchItems, mode).ConfigureAwait(false);

var remaining = inputArray.Skip(firstBatchSize).ToArray();
// continue chunked background import for the remainder
```

This is the key requirement: first visible batch must be loaded separately before the regular chunk loop begins.

- [ ] **Step 4: Run tests**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: viewport-first helper test passes.

- [ ] **Step 5: Commit**

```bash
git add src/ImageGallery.App/Controls/ImageGalleryControl.cs tests/ImageGallery.App.Tests/Program.cs
git commit -m "feat(control): prioritize viewport-first import"
```

## Task 4: Add Safe UI-Thread Merge and Import Cancellation

**Files:**
- Modify: `src/ImageGallery.App/Controls/ImageGalleryControl.cs`
- Test: `tests/ImageGallery.App.Tests/Program.cs`

- [ ] **Step 1: Write the failing test for cancellation hook presence**

Add a reflection test:

```csharp
var cancelMethod = typeof(ImageGalleryControl).GetMethod(
    "CancelActiveImport",
    BindingFlags.Instance | BindingFlags.NonPublic);

AssertTrue(cancelMethod is not null, "control can cancel active import");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: FAIL with `"control can cancel active import"`.

- [ ] **Step 3: Implement cancellation and UI-thread append path**

In `src/ImageGallery.App/Controls/ImageGalleryControl.cs`, add:

```csharp
private void CancelActiveImport()
{
    var cts = Interlocked.Exchange(ref _importCts, null);
    if (cts is null)
    {
        return;
    }

    try
    {
        cts.Cancel();
        cts.Dispose();
    }
    catch (ObjectDisposedException)
    {
    }
}
```

Also add:

```csharp
private Task AppendItemsOnUiThreadAsync(IReadOnlyList<ImageItem> items, LoadMode mode)
{
    if (!_canvas.IsHandleCreated || !InvokeRequired)
    {
        LoadItems(items, mode);
        return Task.CompletedTask;
    }

    var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    BeginInvoke(new Action(() =>
    {
        try
        {
            LoadItems(items, mode);
            tcs.SetResult();
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    }));
    return tcs.Task;
}
```

Call `CancelActiveImport()` from `ClearImages()`, `Dispose(...)`, and at the start of `LoadImagesAsync(...)`.

- [ ] **Step 4: Run tests**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: cancellation reflection test passes.

- [ ] **Step 5: Commit**

```bash
git add src/ImageGallery.App/Controls/ImageGalleryControl.cs tests/ImageGallery.App.Tests/Program.cs
git commit -m "fix(control): cancel in-flight import safely"
```

## Task 5: Migrate MainForm to the Control API

**Files:**
- Modify: `src/ImageGallery.App/Forms/MainForm.cs`
- Test: `tests/ImageGallery.App.Tests/Program.cs`

- [ ] **Step 1: Write the failing integration expectation**

Add a reflection test to assert `MainForm` no longer owns `ComputeUiBatchSize`:

```csharp
var oldBatchMethod = typeof(MainForm).GetMethod(
    "ComputeUiBatchSize",
    BindingFlags.Static | BindingFlags.NonPublic);

AssertTrue(oldBatchMethod is null, "MainForm no longer owns import batching");
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj`

Expected: FAIL because `MainForm` still contains the batching helper.

- [ ] **Step 3: Replace MainForm-local batching with control API usage**

Remove `ImportIntoGalleryAsync(...)`, `ComputeUiBatchSize(...)`, and the form-owned import batching logic from `src/ImageGallery.App/Forms/MainForm.cs`. Replace call sites with:

```csharp
await _galleryControl.LoadImagesAsync(
    CreateGalleryInputs(savedState.ImagePaths),
    LoadMode.Replace);
```

and

```csharp
await _galleryControl.LoadImagesAsync(
    CreateGalleryInputs(dialog.FileNames),
    LoadMode.Append);
```

Keep the form responsible only for status-bar text, session persistence, and demo metadata generation.

- [ ] **Step 4: Run verification**

Run:

```bash
dotnet run --project tests/ImageGallery.App.Tests/ImageGallery.App.Tests.csproj
dotnet run --project tests/ImageGallery.Core.Tests/ImageGallery.Core.Tests.csproj
dotnet build src/ImageGallery.App/ImageGallery.App.csproj
```

Expected:
- `All app tests passed.`
- `All core tests passed.`
- build succeeds with `0` errors

- [ ] **Step 5: Commit**

```bash
git add src/ImageGallery.App/Forms/MainForm.cs tests/ImageGallery.App.Tests/Program.cs
git commit -m "refactor(app): use control import api in demo host"
```

## Acceptance Checklist

| Requirement | Acceptance check |
| --- | --- |
| 控件自身提供大批量导入能力 | Host can call `ImageGalleryControl.LoadImagesAsync(...)` directly |
| 替换/追加模式仍可用 | `Replace` clears first, `Append` preserves existing items |
| 首屏优先加载 | First batch size comes from viewport capacity helper, not total-count coalescing helper |
| 背景继续处理其余项 | Remaining inputs continue in chunked async batches |
| UI 刷新不按单张触发 | Large import uses merged append batches |
| 替换/清空/释放时可中断 | In-flight import cancels without corrupting `_items` |

## Manual Test Flow

| Step | Action | Expected result |
| --- | --- | --- |
| 1 | 启动程序，恢复包含数千张图片的会话 | 首屏先出现一批可见缩略图，不是长时间空白 |
| 2 | 一次性添加 5000~10000 张图片 | 列表分批增长，界面可响应，进度文本按批次跳动 |
| 3 | 导入过程中点击“清除全部” | 导入停止，列表清空，不出现后续回灌 |
| 4 | 导入过程中关闭窗体 | 无异常，无后台继续访问已释放控件 |
| 5 | 切换缩略图倍率后再次导入 | 首屏优先批次会随当前卡片尺寸变化 |

## Self-Review

| Check | Result |
| --- | --- |
| Spec coverage | 本计划只覆盖你要求先改的前两项，未扩展到格式支持、目标框架、事件语义拆分 |
| Placeholder scan | 已去掉 `TODO/TBD`，每个任务都给了目标代码形状、命令和验收点 |
| Type consistency | 统一使用 `LoadImagesAsync(...)`、`ComputeUiBatchSize(...)`、`ComputeInitialViewportBatchSize()`、`CancelActiveImport()` |

Plan complete and saved to `docs/superpowers/plans/2026-05-13-control-import-performance.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
