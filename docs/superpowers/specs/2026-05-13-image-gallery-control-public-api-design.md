# 图像浏览控件二阶段需求设计 Spec

## 1. 背景

当前已有 `ImageGalleryControl`（含虚拟化布局、九种风格、双击/悬停大图、类型筛选、信息字段控制等）。第二阶段把它真正包装成一个**可被外部调用的复用控件**，调用方传入文件路径 + 每张图的"内容信息"（最大物体直径、面积、大小规格），控件负责显示、滚动、缩放、选择、删除以及事件回传。

## 2. 控件公共 API

### 2.1 数据输入

```csharp
public sealed record GalleryImageInput(string FilePath, ImageContentInfo ContentInfo);

public readonly record struct ImageContentInfo(
    double? MaxObjectDiameter,
    double? MaxObjectArea,
    string? SizeSpec)
{
    public static ImageContentInfo Empty => new(null, null, null);
}
```

### 2.2 加载模式

```csharp
public enum LoadMode { Replace, Append }
```

- `Replace`：先清空再显示，**不**触发 `ImageDeleted`（清空属于"为加载让路"，非用户删除）。
- `Append`：追加显示，已存在的路径（`OrdinalIgnoreCase`）跳过去重。

### 2.3 控件 API

```csharp
public sealed class ImageGalleryControl : UserControl
{
    public void LoadImages(IEnumerable<GalleryImageInput> images, LoadMode mode);

    public void RemoveImage(string filePath);                 // API 删除，不发事件
    public void RemoveImages(IEnumerable<string> filePaths);  // API 删除，不发事件
    public void ClearImages();                                // API 删除，不发事件

    public float ThumbnailScale { get; set; }                 // 0.1f ~ 50f
    public int ThumbnailPixelSize { get; }                    // 派生只读

    public GalleryDisplayStyle DisplayStyle { get; set; }
    public ThumbnailInfoFields ThumbnailInfoFields { get; set; }

    public event EventHandler<ImageDeletedEventArgs>? ImageDeleted;   // 仅 UI 删除触发
    public event EventHandler<ImageSelectedEventArgs>? ImageSelected; // 双击触发
}
```

### 2.4 删除事件

```csharp
public enum ImageDeleteAction { Single, Multiple, ClearAll }

public sealed class ImageDeletedEventArgs : EventArgs
{
    public ImageDeletedEventArgs(ImageDeleteAction action, IReadOnlyList<string> filePaths);
    public ImageDeleteAction Action { get; }
    public IReadOnlyList<string> FilePaths { get; }   // ClearAll 时为空数组
}
```

触发规则：
- 用户在控件 UI 上选中后按"删除选中"按钮 / Delete 键：
  - 1 张被删 → `Action=Single, FilePaths=[path]`
  - 多张被删 → `Action=Multiple, FilePaths=[p1, p2, ...]`
- 用户在控件 UI 上点"清除全部"按钮：
  - `Action=ClearAll, FilePaths=[]`
- 上层调用 `RemoveImage / RemoveImages / ClearImages` API 时一律**不**触发事件。

### 2.5 双击事件

```csharp
public sealed class ImageSelectedEventArgs : EventArgs
{
    public ImageSelectedEventArgs(string filePath);
    public string FilePath { get; }   // 完整路径
}
```

控件本身不再"双击=打开大图"。是否打开大图由上层程序订阅 `ImageSelected` 后自行决定（演示程序 `MainForm` 仍打开 `PreviewForm`）。

## 3. 缩略图信息行

`ThumbnailInfoFields` 在原 `FileName / FileSize / ImageType / Dimensions` 基础上扩展：

```csharp
[Flags]
public enum ThumbnailInfoFields
{
    None       = 0,
    FileName   = 1,
    FileSize   = 2,
    ImageType  = 4,
    Dimensions = 8,
    Diameter   = 16,   // 新增：最大物体直径
    Area       = 32,   // 新增：最大物体面积
    SizeSpec   = 64,   // 新增：大小规格
    All = FileName | FileSize | ImageType | Dimensions | Diameter | Area | SizeSpec
}
```

格式化（`ThumbnailInfoFormatter.GetLines`）：

| 字段 | 输出示例 | 空值处理 |
|---|---|---|
| Diameter | `直径 12.50` | 不输出该行 |
| Area | `面积 38.46` | 不输出该行 |
| SizeSpec | `规格 M8×20` | 不输出该行 |

数值统一保留 2 位小数，不带单位（单位由上层在 `SizeSpec` 里自行表达）。

## 4. 缩略图大小

- 取消"64~256 像素"的硬限制，改为缩放因子 0.1× ~ 50×。
- 基线像素 `BasePixelSize = 128`，实际像素 = `Clamp(round(128 * scale), 16, 4096)`。
- TrackBar 用对数刻度：trackbar `0~1000` 映射到 scale `0.1~50`，让小值区间也能精细调节。
- 缩放因子超过 8× 后自动启用更大 LRU 项时降低缓存条数（避免内存爆掉）。

## 5. 性能策略（"ACDSee 3.0 体验"）

- **可见区先行**：`OnPaint` 阶段对当前可见 item 立即入队解码（保留现有 `GetOrQueue`）。
- **后台递增**：每次布局变化或滚动停止后，控件调用 `ThumbnailService.SchedulePrefetch(backlog)` 把"非可见 item 按距离排序"传给后台 worker。worker 使用与同步路径相同的 `_decoderGate` 并发上限，但优先级更低（`Task.Yield` + 取消令牌随时打断）。
- **超大图**：解码前判定 `width * height > 5e7`，走 `Image.GetThumbnailImage(targetW, targetH, null, IntPtr.Zero)` 路径，避免一次性把数百 MB 像素加载到内存。
- **预览**：`PreviewForm.CreatePreviewBitmap` 同样按 100 Mpx 阈值切换到 `GetThumbnailImage`，并加 `OutOfMemoryException` 兜底显示错误条。
- **不锁文件**：所有 `FileStream` 用 `FileShare.Read | FileShare.Write | FileShare.Delete`，确保第三方代码可以同时读取/移动/删除原图。

## 6. 健壮性

- 解码 `OutOfMemoryException`（GDI+ "格式不支持"信号）单独 catch → 返回 placeholder。
- `ThumbnailService` 在 `Dispose` 之后所有后台任务用 `_disposed` 标志短路，`SchedulePrefetch` 内部使用 `CancellationTokenSource`，每次调度先取消旧任务。
- 控件 `LoadImages(Replace)` 前主动 `_thumbnailService.CancelPrefetch()` 让旧预取尽快退出。

## 7. 演示程序（`MainForm`）需要做的改动

仅作为对外 API 的展示：
- TrackBar 改为 `0~1000` 对数刻度 → `ThumbnailScale`。
- 工具栏新增"清除全部"按钮（走 UI 路径，会发 `ImageDeleted(ClearAll)` 事件）。
- 文件对话框选图时构造 `GalleryImageInput`，`ContentInfo` 使用 `ImageContentInfo.Empty`（演示用，真实调用方会有数据）。
- 信息下拉新增"直径/面积/规格"3 项。
- 订阅 `ImageDeleted` 在状态栏显示提示；订阅 `ImageSelected` 决定是否打开 `PreviewForm`。

## 8. 兼容性 / 后向破坏

- 删除现有 `ImageGalleryControl.SetItems` / `ImageOpenRequested` / `PreviewRequested` / `PreviewCloseRequested` 公共成员（仅 `MainForm` 在用，会同步更新）。
- `ThumbnailSize`（int）属性改为派生只读，setter 标记 `[Obsolete]` 改为转发到 `ThumbnailScale`，避免破坏外部潜在引用。
- `ImageItem` 构造器新增 `ImageContentInfo? contentInfo = null` 可选参数，旧调用站点不受影响。
