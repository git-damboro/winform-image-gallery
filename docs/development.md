# 开发文档

## 项目目标

本项目实现一个基于 .NET 6 或更高版本的 WinForms 图像浏览程序。核心目标是用虚拟化缩略图列表支持大量图片浏览，同时保持较低内存占用和稳定的异常隔离。

## 开发约定

- 代码实现阶段同步维护本文档。
- 可测试逻辑优先放入 `ImageGallery.Core`，避免和 WinForms UI 强耦合。
- UI 项目负责 WinForms 控件、图片解码、绘制和预览。
- 删除图片表示从当前浏览列表移除，不删除磁盘文件。
- `Crystal` 表示自绘水晶质感缩略图卡片风格。

## 当前环境

| 项 | 状态 |
|---|---|
| 工作目录 | `E:\Desktop\Mryao\WinForm` |
| Git 仓库 | 已初始化 |
| .NET SDK | 8.0.420 |
| 构建状态 | 核心测试通过，WinForms 构建通过 |

## 当前项目结构

| 路径 | 说明 |
|---|---|
| `src/ImageGallery.Core` | 不依赖 WinForms 的核心逻辑库 |
| `src/ImageGallery.App` | WinForms 桌面程序 |
| `tests/ImageGallery.Core.Tests` | 无第三方依赖的控制台测试项目 |
| `docs/superpowers/specs` | 设计文档 |
| `docs/superpowers/plans` | 实现计划 |

## 核心实现说明

### 虚拟化布局

`GalleryLayoutCalculator` 根据缩略图尺寸、文本区高度、内边距、间距、视口尺寸和滚动位置计算：

- 每行列数；
- 总行数；
- 总内容宽高；
- 当前视口可见或部分可见的首尾索引。

UI 绘制时只遍历 `VisibleRange`，因此 1 万张图片不会对应 1 万个 WinForms 控件。

### 选择规则

`SelectionManager` 管理选择状态：

- 普通单击只选中当前项；
- `Ctrl + 单击` 切换当前项；
- `Shift + 单击` 从锚点到当前项做范围选择；
- 删除时返回降序索引，避免从列表删除时索引错位。

### 缩略图缓存

`ThumbnailService` 使用 `LruCache<string, Image>` 缓存缩略图：

- 默认容量为 600 张缩略图；
- 缓存 key 包含文件路径、最后修改时间和缩略图尺寸；
- 缩略图大小变化时清空缓存；
- 被淘汰的 `Image` 会立即 `Dispose`。
- 缩略图后台解码使用 `SemaphoreSlim` 限制并发，最多同时 2 到 4 个解码任务。
- 待处理缩略图队列上限为 256，避免一次滚动或批量导入时创建过多后台任务。
- 失败项会记录在失败集合中，避免同一张损坏图片在每次重绘时反复解码。

### 图片导入

`ImageFileService` 添加图片时只读取轻量元数据：

- 文件名；
- 文件大小；
- 扩展名；
- 图像宽高；
- 单张图片失败时记录错误信息，不中断批量导入。

支持的扩展名策略位于 `FileFormatPolicy`。实际解码能力取决于运行环境的 GDI+ 支持。

格式策略分为两层：

- 原生可解码：`.jpg`、`.jpeg`、`.png`、`.bmp`、`.gif`、`.tif`、`.tiff`、`.ico`。
- 可识别现代格式：`.webp`、`.heic`、`.heif`、`.avif`。

可识别现代格式会出现在文件对话框中，但当前不承诺由 `System.Drawing` 稳定解码。若系统解码失败，会在对应缩略图上显示错误占位，程序不崩溃。若需要稳定支持 WebP、HEIC、AVIF，应引入 `ImageSharp` 或 `Magick.NET` 作为后续增强。

### WinForms UI

主窗体由 `MainForm` 组合：

- 添加图片按钮；
- 删除选中按钮；
- 缩略图大小滑动条；
- 显示风格下拉框；
- 图片数量标签；
- `ImageGalleryControl` 主体浏览区域。

`ImageGalleryControl` 自带水平和垂直滚动条，内部 canvas 双缓冲绘制卡片。`GalleryRenderer` 支持 `Default`、`Compact`、`Crystal` 三种显示风格，其中 `Crystal` 使用渐变、高光、阴影和边框自绘。

`PreviewForm` 用于鼠标悬停或双击后的大图预览，预览失败时显示错误文本。

预览策略：

- 鼠标悬停使用无边框临时预览，鼠标移开自动关闭。
- 双击缩略图打开可手动关闭的大图窗口。
- 大图窗口只保留按窗口尺寸缩放后的位图，避免长期持有超大原图。

## 性能验收辅助

主窗体工具栏提供“模拟1万张”按钮，用于快速生成 10,000 条虚拟图片记录，验证：

- 列表不会创建 10,000 个 WinForms 子控件；
- 滚动条和可见范围计算正常；
- 自绘虚拟化在大量数据下仍可交互；
- 损坏或无法解码图片会显示错误占位，不影响整体浏览。

## 开发日志

| 时间 | 内容 | 验证 |
|---|---|---|
| 2026-05-10 | 初始化 Git，写入设计文档和实现计划 | `git commit` 已完成设计文档提交 |
| 2026-05-10 | 创建核心测试项目并先写核心行为测试 | 初次验证因未安装 .NET SDK 失败 |
| 2026-05-10 | 实现核心库、WinForms 主界面、虚拟化控件、导入服务、缩略图服务、渲染器和预览窗体 | 初次验证因未安装 .NET SDK 失败 |
| 2026-05-10 | 安装 .NET 8 SDK 后，将项目目标框架从 .NET 6 升级到 .NET 8，并修正布局测试期望 | 核心测试通过，WinForms 构建通过 |
| 2026-05-10 | 增强性能与健壮性：缩略图并发限流、失败缓存、超大图预览降采样、1万张模拟入口、格式策略分层 | 核心测试通过，WinForms 构建通过 |

## 安装 SDK 后的验证命令

```powershell
dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj
dotnet build src\ImageGallery.App\ImageGallery.App.csproj
dotnet run --project src\ImageGallery.App\ImageGallery.App.csproj
```

## 最新自动验证结果

| 命令 | 结果 |
|---|---|
| `dotnet --version` | `8.0.420` |
| `dotnet --list-sdks` | `8.0.420 [C:\Program Files\dotnet\sdk]` |
| `dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj` | 通过，输出 `All core tests passed.` |
| `dotnet build src\ImageGallery.App\ImageGallery.App.csproj` | 通过，0 警告，0 错误 |

## 手工验证清单

| 功能 | 验证方式 |
|---|---|
| 添加单张图片 | 点击“添加图片”，选择一张图片，列表出现缩略图和元数据 |
| 添加多张图片 | 在文件对话框中多选，确认数量标签增加 |
| 单选删除 | 单击一张缩略图后点击“删除选中” |
| 多选删除 | 使用 `Ctrl` 或 `Shift` 选择多张后删除 |
| 滚动 | 添加大量图片后拖动垂直滚动条 |
| 缩略图尺寸 | 拖动滑动条，卡片重新布局 |
| Crystal 风格 | 在下拉框选择 `Crystal`，观察水晶质感卡片 |
| 大图预览 | 鼠标悬停或双击缩略图，出现预览窗体，移开后关闭 |
| 异常图片 | 添加损坏或不支持文件，程序不崩溃并显示错误占位 |
| 1万张性能 | 点击“模拟1万张”，确认数量显示为 10,000，滚动和缩放可用 |
