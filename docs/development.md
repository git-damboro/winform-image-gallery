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
| .NET SDK | 当前未检测到可用 SDK |
| 构建状态 | `dotnet run` 和 `dotnet build` 均因 SDK 缺失无法执行 |

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

### 图片导入

`ImageFileService` 添加图片时只读取轻量元数据：

- 文件名；
- 文件大小；
- 扩展名；
- 图像宽高；
- 单张图片失败时记录错误信息，不中断批量导入。

支持的扩展名策略位于 `FileFormatPolicy`，包括 `.jpg`、`.jpeg`、`.png`、`.bmp`、`.gif`、`.tif`、`.tiff`、`.ico`、`.webp`。实际解码能力仍取决于运行环境的 GDI+ 支持。

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

## 开发日志

| 时间 | 内容 | 验证 |
|---|---|---|
| 2026-05-10 | 初始化 Git，写入设计文档和实现计划 | `git commit` 已完成设计文档提交 |
| 2026-05-10 | 创建核心测试项目并先写核心行为测试 | `dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj` 因未安装 .NET SDK 失败 |
| 2026-05-10 | 实现核心库、WinForms 主界面、虚拟化控件、导入服务、缩略图服务、渲染器和预览窗体 | `dotnet build src\ImageGallery.App\ImageGallery.App.csproj` 因未安装 .NET SDK 失败 |

## 安装 SDK 后的验证命令

```powershell
dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj
dotnet build src\ImageGallery.App\ImageGallery.App.csproj
dotnet run --project src\ImageGallery.App\ImageGallery.App.csproj
```

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
