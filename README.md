# WinForms Image Gallery

一个基于 `.NET 8.0` 的 WinForms 图片缩略图浏览器，面向本地大量图片快速浏览、筛选、预览和删除场景。

## 功能

- 支持批量导入图片
- 支持缩略图网格浏览和滚动
- 支持鼠标滚轮、拖动滚动条、键盘 `Ctrl + A` 全选当前可见项
- 支持缩略图尺寸调节
- 支持缩略图信息显示开关
- 支持图片类型多选筛选，仅影响画布显示
- 支持悬停预览和双击大图查看
- 支持删除选中图片
- 支持启动后恢复上次会话
- 支持进度提示，导入和恢复时显示当前任务与进度条
- 支持多种缩略图显示风格，包含 `crystal`

## 缩略图风格

当前内置风格如下：

| 风格 | 说明 |
|---|---|
| `default` | 默认基础卡片效果 |
| `rounded` | 圆角更明显 |
| `shadow` | 阴影层次更强 |
| `border` | 描边更突出 |
| `polaroid` | 拍立得样式，底部留白 |
| `glass` | 磨砂玻璃效果 |
| `crystal` | 水晶/玻璃高光效果，当前重点风格 |
| `neon` | 霓虹发光效果 |
| `minimal` | 极简扁平效果 |

`crystal` 风格采用冷色渐变、半透明高光、轻微阴影和发光，鼠标悬停时会增强边框亮度与质感。

## 运行环境

- Windows
- `.NET 8.0` 或更高版本

## 项目结构

| 路径 | 说明 |
|---|---|
| `src/ImageGallery.App` | WinForms 主程序 |
| `src/ImageGallery.Core` | 核心模型和基础服务 |
| `tests/ImageGallery.Core.Tests` | 核心逻辑自检控制台测试 |
| `docs/superpowers/specs` | 需求与设计说明 |
| `docs/superpowers/plans` | 实现计划 |

## 启动方式

```powershell
dotnet run --project src\ImageGallery.App\ImageGallery.App.csproj
```

## 核心测试

```powershell
dotnet run --project tests\ImageGallery.Core.Tests\ImageGallery.Core.Tests.csproj
```

## 使用说明

1. 点击“添加图片”导入文件。
2. 使用顶部工具栏调整缩略图尺寸。
3. 使用“风格”下拉框切换缩略图展示效果。
4. 使用“类型”下拉框多选筛选要显示的图片类型。
5. 使用“信息”下拉框控制缩略图下方显示哪些元数据。
6. 鼠标悬停在缩略图上可预览，双击可打开大图。
7. 使用“全选”按钮或 `Ctrl + A` 选中当前可见图片。

## 会话保存

程序会把最近一次导入的图片列表和当前缩略图风格保存到本地用户目录。下次启动时会自动恢复。

## 如何扩展新风格

新增缩略图风格时，通常只需要做三件事：

1. 在 `GalleryDisplayStyle` 中增加一个枚举值
2. 在 `ThumbnailStyleCatalog` 中增加一条风格配置
3. 在 `GalleryRenderer` 中补一段对应的绘制规格

这样可以保持风格入口、会话保存和渲染逻辑一致，不需要在多个地方写大量 `if/else`。

## 说明

项目当前优先保证：

- 大量图片下的浏览稳定性
- 缩略图绘制效率
- 操作响应速度
- 样式切换即时生效

