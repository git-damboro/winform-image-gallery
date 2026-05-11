# WinForms Image Gallery

WinForms Image Gallery 是一个基于 `.NET 8.0` 的 Windows 本地图片缩略图浏览系统，适合在本机管理、浏览和筛选大量图片。系统支持批量导入、缩略图风格切换、图片类型筛选、大图预览、选择删除、会话恢复和加载进度提示。

## 一、系统功能

| 功能 | 说明 |
|---|---|
| 批量导入图片 | 支持一次选择多张图片导入，适合本地大量图片浏览场景 |
| 缩略图网格浏览 | 以网格方式展示图片，支持鼠标滚轮和滚动条浏览 |
| 实时滚动显示 | 拖动滚动条时会实时刷新当前可见图片 |
| 大图预览 | 双击缩略图打开大图窗口，支持键盘左右键切换 |
| 翻页提示 | 大图查看时显示文件名和当前序号，例如 `1/21323` |
| 多选与全选 | 支持鼠标选择、`Ctrl + A` 和顶部“全选”按钮 |
| 删除选中图片 | 可以删除当前选中的图片项 |
| 图片类型筛选 | 支持多选筛选 PNG、JPG、BMP、GIF、TIFF、WEBP 等类型 |
| 缩略图信息控制 | 可选择是否显示图片名称、大小、类型和尺寸 |
| 缩略图尺寸调整 | 可通过顶部滑块调整缩略图大小 |
| 多种显示风格 | 支持 default、rounded、shadow、border、polaroid、glass、crystal、neon、minimal |
| 会话恢复 | 重新打开程序时自动恢复上次导入的图片列表和缩略图风格 |
| 进度提示 | 启动恢复和导入图片时显示当前任务、进度和当前文件名 |

## 二、运行环境

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows 10 / Windows 11 |
| 运行时 | .NET 8 Desktop Runtime，或更高版本 |
| 项目框架 | `net8.0-windows` |
| 开发工具 | Visual Studio 2022 或 .NET SDK 8.0+ |

> 注意：这是 WinForms 桌面程序，新电脑必须安装 Windows 桌面运行时。只安装普通 `.NET Runtime` 不一定能运行 WinForms 程序。

## 三、新电脑部署方式

### 方式一：直接运行发布包

适合只需要使用程序的新电脑。

1. 在新电脑安装 `.NET 8 Desktop Runtime`。
2. 解压发布压缩包。
3. 进入解压后的文件夹。
4. 双击运行 `ImageGallery.App.exe`。

如果运行时报缺少 .NET 运行环境，请安装：

```text
.NET 8 Desktop Runtime x64
```

### 方式二：从源码运行

适合需要继续开发或修改代码的新电脑。

1. 安装 `.NET SDK 8.0` 或更高版本。
2. 安装 Visual Studio 2022，并勾选“.NET 桌面开发”工作负载。
3. 克隆或复制本项目源码。
4. 在项目根目录执行：

```powershell
dotnet run --project src\ImageGallery.App\ImageGallery.App.csproj
```

## 四、如何重新打包发布包

在项目根目录执行：

```powershell
dotnet publish src\ImageGallery.App\ImageGallery.App.csproj -c Release -r win-x64 --self-contained false -o release\publish
```

然后压缩 `release\publish` 目录即可。

当前项目使用框架依赖发布方式，压缩包体积更小，但新电脑需要提前安装 `.NET 8 Desktop Runtime`。

## 五、项目结构

| 路径 | 说明 |
|---|---|
| `src/ImageGallery.App` | WinForms 桌面程序入口、窗体、控件和渲染逻辑 |
| `src/ImageGallery.Core` | 图片模型、布局计算、筛选、选择、会话保存等核心逻辑 |
| `tests/ImageGallery.Core.Tests` | 核心逻辑自检测试 |
| `docs/superpowers/specs` | 需求和设计说明 |
| `docs/superpowers/plans` | 实现计划 |

## 六、常用开发命令

运行程序：

```powershell
dotnet run --project src\ImageGallery.App\ImageGallery.App.csproj
```

运行测试：

```powershell
dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj
```

构建程序：

```powershell
dotnet build src\ImageGallery.App\ImageGallery.App.csproj
```

发布程序：

```powershell
dotnet publish src\ImageGallery.App\ImageGallery.App.csproj -c Release -r win-x64 --self-contained false -o release\publish
```

## 七、使用说明

1. 点击“添加图片”导入本地图片。
2. 使用顶部滑块调整缩略图大小。
3. 使用风格下拉框切换缩略图显示风格。
4. 使用“类型”下拉框筛选要显示的图片类型。
5. 使用“信息”下拉框控制缩略图下方显示哪些信息。
6. 鼠标滚轮或滚动条浏览图片。
7. 单击图片选择，`Ctrl + A` 或“全选”选择当前可见图片。
8. 双击缩略图打开大图查看。
9. 在大图窗口使用鼠标或键盘左右键切换图片。

## 八、缩略图风格说明

| 风格 | 效果 |
|---|---|
| `default` | 默认基础卡片效果 |
| `rounded` | 圆角卡片效果 |
| `shadow` | 更明显的卡片阴影 |
| `border` | 描边边框效果 |
| `polaroid` | 拍立得照片样式，底部留白 |
| `glass` | 柔和半透明玻璃质感 |
| `crystal` | 冷色渐变、高光、轻微发光的水晶卡片效果 |
| `neon` | 霓虹发光效果 |
| `minimal` | 极简扁平效果 |

## 九、数据保存说明

程序会在本地用户目录保存会话信息，包括：

| 数据 | 说明 |
|---|---|
| 图片路径列表 | 用于下次启动时恢复上次导入的图片 |
| 缩略图风格 | 用于恢复上次选择的显示风格 |

程序不会把图片复制到项目目录，保存的是图片原始路径。如果新电脑上图片路径变化，需要重新导入图片。

## 十、注意事项

- 如果需要浏览一万张以上图片，建议图片放在本地磁盘，避免网络盘带来的读取延迟。
- 如果图片被移动或删除，程序恢复时会保留条目但显示加载失败信息。
- HEIC、HEIF、AVIF 等格式可以识别，但是否能直接显示取决于 Windows 系统解码支持。
- 如果需要让没有 .NET 环境的新电脑直接运行，可以改为自包含发布：`--self-contained true`，但压缩包会明显变大。
