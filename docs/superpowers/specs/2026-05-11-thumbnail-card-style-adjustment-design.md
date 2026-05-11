# Thumbnail Card Style Adjustment Design

**Goal:** 把缩略图风格效果恢复到卡片层绘制，并拉开 `glass`、`crystal`、`shadow` 的视觉差异，同时保持当前图片导入、预览、删除、筛选、会话保存逻辑不变。

**Architecture:** 继续使用现有的 `GalleryRenderer` + `ThumbnailStyleCatalog` + `GalleryDisplayStyle` 结构，不新增业务流程，也不改变图片数据流。风格效果只作用于卡片背景、卡片内高光、边框、阴影和发光层，不再在图片本体上叠加风格层。`glass` 走柔和半透明路线，`crystal` 走冷色高光和更强的边缘光路线，`shadow` 则把阴影和层次感拉强，避免三个风格看起来接近。

**Tech Stack:** C# / WinForms / `System.Drawing` / existing gallery renderer / existing session store / existing core tests.

---

### 1. Card-level style composition

`GalleryRenderer.DrawCard` 负责整张卡片的视觉组成：

1. 先画卡片阴影
2. 再画卡片底色和边框
3. 再在卡片内部画缩略图
4. 最后叠加风格层，例如高光、玻璃层、发光边框

这样可以保证所有风格都属于“卡片样式”，而不是“图片滤镜”，也能保持缩略图内容始终清晰可见。

### 2. Style differentiation rules

**`glass`**
- 更柔和
- 半透明感更强
- 边缘高光更轻
- 阴影更淡
- 视觉重点是“透”和“软”

**`crystal`**
- 冷色调更明显
- 边缘高光更硬、更亮
- 发光更明显
- 轮廓更清晰
- 视觉重点是“亮”和“硬”

**`shadow`**
- 阴影更深
- 阴影更扩散
- 卡片主体保持克制
- 视觉重点是“层次”和“悬浮感”

**其他风格**
- `default` 保持当前基础效果
- `rounded` 只增强圆角和柔和感
- `border` 只增强描边
- `polaroid` 保留拍立得底部留白
- `neon` 维持更强的霓虹边缘感
- `minimal` 维持扁平、低装饰感

### 3. Data flow

风格选择流程保持不变：

1. 用户在工具栏下拉框选择风格
2. `MainForm` 把选中的 `GalleryDisplayStyle` 传给 `ImageGalleryControl`
3. 控件触发布局重算和重绘
4. `GalleryRenderer` 根据当前风格选择对应的卡片层视觉参数
5. 当前会话继续保存风格值，重启后恢复

这次调整不会改动图片导入、缓存、预览、删除、类型筛选、信息筛选和会话文件格式。

### 4. Error handling and compatibility

如果某个风格值在旧会话里不存在，按现有逻辑回退到默认风格，不阻塞启动。  
如果某个图像本身加载失败，仍然沿用当前的失败占位绘制，不让卡片风格层影响异常处理路径。  
如果图片很大或缩略图还未准备好，卡片风格依然先绘制，内容层继续按现有异步加载逻辑补上。

### 5. Testing

验证重点放在两个层面：

1. 核心测试继续确认风格目录存在、会话风格能保存和恢复
2. WinForms 编译通过后，手动确认三件事：
   - `glass` 和 `crystal` 能明显区分
   - `shadow` 阴影更明显
   - 所有风格都仍然只影响卡片层，不影响图片加载流程

### 6. Files expected to change

- `src/ImageGallery.App/Rendering/GalleryRenderer.cs`
- `src/ImageGallery.Core/Models/ThumbnailStyleCatalog.cs`
- `src/ImageGallery.Core/Models/ThumbnailStyleDefinition.cs`
- `src/ImageGallery.Core/Models/GalleryTypes.cs`
- `src/ImageGallery.App/Forms/MainForm.cs`

