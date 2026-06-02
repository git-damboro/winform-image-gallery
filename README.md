# WinForms Image Gallery

一个面向 WinForms 的图像浏览控件与示例程序，支持大批量图片缩略图浏览、内容信息展示、删除事件回调、双击选中事件、缩略图风格切换，以及面向大数据量场景的异步分批导入。

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `src/ImageGallery.Core` | 核心模型、布局计算、过滤、会话保存、信息格式化 |
| `src/ImageGallery.App` | WinForms 控件、渲染、缩略图服务、示例宿主窗体 |
| `tests/ImageGallery.Core.Tests` | Core 层自检 |
| `tests/ImageGallery.App.Tests` | App / 控件层自检 |

## 当前能力

| 功能 | 状态 |
| --- | --- |
| 网格缩略图浏览 | 已实现 |
| 横向 / 纵向滚动条 | 已实现 |
| 缩略图倍率 0.1x 到 50x | 已实现 |
| 缩略图下显示直径 / 面积 / 规格 | 已实现 |
| 追加导入 / 替换导入 | 已实现 |
| 删除单张 / 多张 / 全部 | 已实现 |
| 用户删除后的事件回调 | 已实现 |
| 上级程序主动删除且不回调 | 已实现 |
| 双击图片选中事件 | 已实现 |
| 多种显示风格，包含 `Crystal` | 已实现 |
| 首屏优先加载 | 已实现 |
| 异步批量导入 | 已实现 |
| 低频率底部进度刷新 | 已实现 |

## 本轮按新需求完成的改动

| 类别 | 改动 |
| --- | --- |
| 控件化接口 | 把大批量导入能力下沉到 `ImageGalleryControl`，不再只依赖 `MainForm` |
| 新公开 API | 新增 `LoadImagesAsync(...)`，上级程序可直接异步导入 |
| 加载策略 | 先按当前视口容量加载首批图片，再后台分批处理剩余图片 |
| 进度策略 | 底部进度按“批次”更新，不再每张图片刷新一次，兼顾可见性与性能 |
| 取消机制 | 替换、清空、销毁时会取消进行中的导入 |
| 事件语义 | 保留删除事件与选中事件；双击时通过 `ImageSelected` 上报完整文件路径 |
| 信息展示 | 支持 `FileName / FileSize / ImageType / Dimensions / Diameter / Area / SizeSpec` |
| 宿主接线 | 示例程序 `MainForm` 已切换到控件级异步导入主路径 |

## 运行环境

| 项目 | 当前配置 |
| --- | --- |
| 操作系统 | Windows 10 / 11 |
| SDK | .NET SDK 8.0+ |
| 应用目标框架 | `net8.0-windows` |
| Core 目标框架 | `net8.0` |

> 说明：当前代码仓库实际目标框架是 `.NET 8`。如果后续要严格满足 “.NET 6.0 及以上” 的交付要求，需要再补多目标或下调目标框架。

## 快速运行

### 运行示例程序

```powershell
dotnet run --project src\ImageGallery.App\ImageGallery.App.csproj
```

### 运行测试

```powershell
dotnet run --project tests\ImageGallery.App.Tests\ImageGallery.App.Tests.csproj
dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj
```

### 构建

```powershell
dotnet build src\ImageGallery.App\ImageGallery.App.csproj
```

## 控件公开用法

### 1. 基本模型

```csharp
using ImageGallery.App.Controls;
using ImageGallery.Core.Models;
```

上级程序传入的数据模型：

| 类型 | 说明 |
| --- | --- |
| `GalleryImageInput` | 单张图片输入，包含文件路径和内容信息 |
| `ImageContentInfo` | 内容信息，包含最大物体直径、面积、规格 |
| `LoadMode.Replace` | 替换当前全部图片 |
| `LoadMode.Append` | 在现有图片基础上追加 |

### 2. 创建控件

```csharp
var gallery = new ImageGalleryControl
{
    Dock = DockStyle.Fill,
    DisplayStyle = GalleryDisplayStyle.Crystal,
    ThumbnailScale = 1.0f,
    ThumbnailInfoFields =
        ThumbnailInfoFields.Diameter
        | ThumbnailInfoFields.Area
        | ThumbnailInfoFields.SizeSpec
};

Controls.Add(gallery);
```

### 3. 组织输入数据

```csharp
var inputs = new[]
{
    new GalleryImageInput(
        @"D:\Images\a.jpg",
        new ImageContentInfo(
            MaxObjectDiameter: 12.5,
            MaxObjectArea: 88.2,
            SizeSpec: "S1-01")),
    new GalleryImageInput(
        @"D:\Images\b.png",
        new ImageContentInfo(
            MaxObjectDiameter: 18.0,
            MaxObjectArea: 126.4,
            SizeSpec: "S2-08"))
};
```

### 4. 异步导入，推荐

#### 替换方式

```csharp
await gallery.LoadImagesAsync(inputs, LoadMode.Replace);
```

#### 追加方式

```csharp
await gallery.LoadImagesAsync(inputs, LoadMode.Append);
```

#### 带低频进度回调

```csharp
await gallery.LoadImagesAsync(
    inputs,
    LoadMode.Append,
    (completed, total, currentFileName) =>
    {
        statusLabel.Text = $"正在导入 {completed}/{total}";
        detailLabel.Text = currentFileName;
    });
```

> 这里的进度不是每张图都回调一次，而是按控件内部批次回调，适合大批量场景。

### 5. 同步导入，兼容旧用法

```csharp
gallery.LoadImages(inputs, LoadMode.Replace);
```

> 同步接口仍然保留，但大批量场景建议优先使用 `LoadImagesAsync(...)`。

## 删除接口与事件

### 上级程序主动删除，不触发删除事件

```csharp
gallery.RemoveImage(@"D:\Images\a.jpg");

gallery.RemoveImages(new[]
{
    @"D:\Images\b.jpg",
    @"D:\Images\c.png"
});

gallery.ClearImages();
```

### 用户在控件中删除，触发删除事件

```csharp
gallery.ImageDeleted += (_, e) =>
{
    switch (e.Action)
    {
        case ImageDeleteAction.Single:
        case ImageDeleteAction.Multiple:
            foreach (var filePath in e.FilePaths)
            {
                Console.WriteLine(filePath);
            }
            break;

        case ImageDeleteAction.ClearAll:
            Console.WriteLine("全部清空");
            break;
    }
};
```

事件规则：

| 场景 | 是否触发 `ImageDeleted` |
| --- | --- |
| 用户通过控件 UI 删除单张/多张 | 触发 |
| 用户通过控件 UI 清空全部 | 触发 |
| 上级程序调用 `RemoveImage/RemoveImages/ClearImages` | 不触发 |

## 选中事件

### 双击图片后的选中事件

```csharp
gallery.ImageSelected += (_, e) =>
{
    if (e.Mode == ImageSelectionMode.PinnedOpen)
    {
        Console.WriteLine($"双击选中: {e.FilePath}");
    }
};
```

当前 `ImageSelected` 还承载悬浮预览通知：

| `Mode` | 含义 |
| --- | --- |
| `HoverPreview` | 悬浮预览 |
| `PinnedOpen` | 双击固定打开 / 选中 |

如果上级程序只关心“双击选中”，只处理 `PinnedOpen` 即可。

## 缩略图显示控制

### 设置显示字段

```csharp
gallery.ThumbnailInfoFields =
    ThumbnailInfoFields.FileName
    | ThumbnailInfoFields.Diameter
    | ThumbnailInfoFields.Area
    | ThumbnailInfoFields.SizeSpec;
```

### 设置缩略图倍率

```csharp
gallery.ThumbnailScale = 0.5f;   // 0.5x
gallery.ThumbnailScale = 1.0f;   // 1.0x
gallery.ThumbnailScale = 10.0f;  // 10x
```

范围：

| 属性 | 范围 |
| --- | --- |
| `ThumbnailScale` | `0.1f` 到 `50f` |

### 设置显示风格

```csharp
gallery.DisplayStyle = GalleryDisplayStyle.Crystal;
```

支持风格：

| 风格 |
| --- |
| `Default` |
| `Rounded` |
| `Shadow` |
| `Border` |
| `Polaroid` |
| `Glass` |
| `Crystal` |
| `Neon` |
| `Minimal` |

## 性能说明

### 当前实现策略

| 策略 | 说明 |
| --- | --- |
| 首屏优先 | 先按视口行列容量加载首批图片，优先让用户看到内容 |
| 后台分批导入 | 剩余图片按批次继续导入，避免一次性全量阻塞 UI |
| 低频进度更新 | 底部进度按批次更新，而不是每张图片都更新 |
| 缩略图缓存 | 使用缓存与后台预取，减少重复解码 |
| 超大图保护 | 对超大图走降级缩略图路径，降低内存压力 |
| 非锁文件读取 | 文件读取使用共享方式打开，避免锁死图片文件 |

### 已知说明

| 项 | 说明 |
| --- | --- |
| 现代格式 | `WEBP / HEIC / HEIF / AVIF` 已识别，但是否可解码仍取决于系统解码支持 |
| 当前目标框架 | 仓库当前是 `.NET 8`，不是 `.NET 6` 多目标 |
| 事件语义 | `ImageSelected` 当前同时覆盖悬浮预览与双击选中，通过 `Mode` 区分 |

## 示例宿主说明

`MainForm` 只是控件演示宿主，额外做了两件事：

| 内容 | 说明 |
| --- | --- |
| 模拟内容信息 | 为演示直径 / 面积 / 规格显示，示例程序会自动生成模拟数据 |
| 会话恢复 | 关闭后会保存已加载图片路径与风格，下次启动自动恢复 |

## 推荐回归验证

| 场景 | 检查点 |
| --- | --- |
| 一次追加几千张图片 | 首屏能尽快看到图片，后续持续分批补齐 |
| 导入 1 万张图片 | 底部进度会动，但不会高频抖动 |
| 清空 / 替换 | 导入中的旧任务不会继续回灌 |
| 双击图片 | 上级程序能收到 `PinnedOpen` 事件和完整文件路径 |
| 用户删除 | 上级程序能收到 `ImageDeleted` 及文件列表 |

## 常用命令

```powershell
dotnet run --project src\ImageGallery.App\ImageGallery.App.csproj
dotnet run --project tests\ImageGallery.App.Tests\ImageGallery.App.Tests.csproj
dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj
dotnet build src\ImageGallery.App\ImageGallery.App.csproj
```
