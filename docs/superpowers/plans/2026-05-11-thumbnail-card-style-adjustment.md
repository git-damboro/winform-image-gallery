# Thumbnail Card Style Adjustment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把缩略图样式效果恢复到卡片层，并明确拉开 `glass`、`crystal`、`shadow` 的视觉差异，同时保持图片导入、预览、删除、筛选、会话保存逻辑不变。

**Architecture:** 继续沿用现有的 `GalleryRenderer` + `ThumbnailStyleCatalog` + `GalleryDisplayStyle` 结构，但把风格参数收敛成“卡片视觉配置”，不再把样式叠到图片本体上。渲染流程保持单向：先画卡片阴影和底色，再画缩略图，再叠加卡片高光/描边/发光层。`glass` 走柔和半透明路线，`crystal` 走冷色高光和更强边缘光路线，`shadow` 则把阴影强度和扩散感拉高。

**Tech Stack:** C# / WinForms / `System.Drawing` / 现有核心测试控制台 / 现有会话保存机制。

---

### Task 1: 把风格参数整理成可测试的卡片视觉配置

**Files:**
- Create: `src/ImageGallery.Core/Models/ThumbnailStyleVisualProfile.cs`
- Modify: `src/ImageGallery.Core/Models/ThumbnailStyleDefinition.cs`
- Modify: `src/ImageGallery.Core/Models/ThumbnailStyleCatalog.cs`
- Modify: `tests/ImageGallery.Core.Tests/Program.cs`

- [ ] **Step 1: 写失败测试**

```csharp
var glass = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Glass);
var crystal = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Crystal);
var shadow = ThumbnailStyleCatalog.Get(GalleryDisplayStyle.Shadow);

AssertEqual(true, crystal.VisualProfile.GlowStrength > glass.VisualProfile.GlowStrength, "crystal glow stronger than glass");
AssertEqual(true, crystal.VisualProfile.BorderContrast > glass.VisualProfile.BorderContrast, "crystal border stronger than glass");
AssertEqual(true, shadow.VisualProfile.ShadowStrength > glass.VisualProfile.ShadowStrength, "shadow stronger than glass");
AssertEqual(true, shadow.VisualProfile.ShadowBlur > crystal.VisualProfile.ShadowBlur, "shadow blur stronger than crystal");
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet run --project tests\\ImageGallery.Core.Tests\\ImageGallery.Core.Tests.csproj`
Expected: 失败，原因是 `ThumbnailStyleVisualProfile` 还不存在，`ThumbnailStyleDefinition` 也还没有视觉配置字段。

- [ ] **Step 3: 写最小实现**

```csharp
public sealed record ThumbnailStyleVisualProfile(
    int ShadowStrength,
    int ShadowBlur,
    int BorderContrast,
    int GlowStrength,
    int HighlightStrength,
    bool UseGlassOverlay,
    bool UseBottomBand);

public sealed record ThumbnailStyleDefinition(
    GalleryDisplayStyle Value,
    string Label,
    string Description,
    int Padding,
    int Gap,
    int TextTopSpacing,
    int TextLineHeight,
    int CornerRadius,
    ThumbnailStyleVisualProfile VisualProfile);
```

```csharp
new ThumbnailStyleDefinition(
    GalleryDisplayStyle.Crystal,
    "Crystal",
    "冷色水晶高光卡片，适合强调质感",
    Padding: 10,
    Gap: 14,
    TextTopSpacing: 6,
    TextLineHeight: 20,
    CornerRadius: 12,
    VisualProfile: new ThumbnailStyleVisualProfile(
        ShadowStrength: 52,
        ShadowBlur: 18,
        BorderContrast: 92,
        GlowStrength: 100,
        HighlightStrength: 190,
        UseGlassOverlay: true,
        UseBottomBand: false));
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet run --project tests\\ImageGallery.Core.Tests\\ImageGallery.Core.Tests.csproj`
Expected: PASS。

- [ ] **Step 5: 提交**

```bash
git add src/ImageGallery.Core/Models/ThumbnailStyleVisualProfile.cs src/ImageGallery.Core/Models/ThumbnailStyleDefinition.cs src/ImageGallery.Core/Models/ThumbnailStyleCatalog.cs tests/ImageGallery.Core.Tests/Program.cs
git commit -m "feat: add card visual profiles for thumbnail styles"
```

### Task 2: 把样式绘制回收到卡片层，并拉开 glass/crystal/shadow

**Files:**
- Modify: `src/ImageGallery.App/Rendering/GalleryRenderer.cs`

- [ ] **Step 1: 写失败测试**

由于这里是 WinForms 绘制层，没有现成的 UI 快照测试框架，本任务先通过第 1 个任务的视觉配置测试锁住样式差异，再通过编译和人工截图确认卡片层效果。

- [ ] **Step 2: 运行失败确认**

Run: `dotnet build src\\ImageGallery.App\\ImageGallery.App.csproj -p:OutDir=E:\\Desktop\\Mryao\\WinForm\\.buildverify\\`
Expected: 先失败于旧的图片层风格实现和新的配置字段未接入。

- [ ] **Step 3: 写最小实现**

```csharp
public void DrawCard(...)
{
    var style = ThumbnailStyleCatalog.Get(displayStyle);
    DrawCardShadow(graphics, bounds, selected, hovered, style.VisualProfile);
    DrawCardBackground(graphics, bounds, selected, hovered, style.VisualProfile);
    DrawThumbnailImage(graphics, thumbnail, imageBounds); // 只画图片本体，不叠风格层
    DrawCardOverlay(graphics, bounds, selected, hovered, style.VisualProfile, displayStyle);
    DrawMetadata(...);
}

private static void DrawCardOverlay(...)
{
    // glass：更软更透，减少边缘高光
    // crystal：冷色高光更强，边缘更硬，发光更明显
    // shadow：阴影更深、更扩散
}
```

```csharp
if (displayStyle == GalleryDisplayStyle.Crystal)
{
    // Crystal: card-level icy highlight, cooler border, and stronger glow.
}
else if (displayStyle == GalleryDisplayStyle.Glass)
{
    // Glass: softer translucent surface with reduced border contrast.
}
else if (displayStyle == GalleryDisplayStyle.Shadow)
{
    // Shadow: heavier offset shadow and more diffuse edge falloff.
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet build src\\ImageGallery.App\\ImageGallery.App.csproj -p:OutDir=E:\\Desktop\\Mryao\\WinForm\\.buildverify\\`
Expected: PASS，`0` warnings，`0` errors。

- [ ] **Step 5: 提交**

```bash
git add src/ImageGallery.App/Rendering/GalleryRenderer.cs
git commit -m "feat: move thumbnail styles back to card layer"
```

### Task 3: 做视觉验收并清理验证产物

**Files:**
- Modify: `README.md`，仅在需要补充风格说明时

- [ ] **Step 1: 手工验收**

启动应用后依次切换 `Glass`、`Crystal`、`Shadow`，确认：
1. 风格变化体现在卡片层而不是图片本体
2. `Glass` 比 `Crystal` 更柔和、更透
3. `Shadow` 的阴影比其他风格更明显
4. 原有导入、预览、删除、筛选、会话恢复行为不变

- [ ] **Step 2: 运行应用**

Run: `dotnet run --project src\\ImageGallery.App\\ImageGallery.App.csproj`
Expected: 可以手动确认三种风格的差异。

- [ ] **Step 3: 清理验证输出**

```powershell
Remove-Item -LiteralPath '.buildverify' -Recurse -Force
```

- [ ] **Step 4: 提交**

```bash
git add README.md
git commit -m "docs: update thumbnail style notes"
```

### Self-Review

- Spec coverage: `glass`、`crystal`、`shadow` 的差异被单独落到视觉配置与卡片渲染层。
- Placeholder scan: 没有 `TBD` / `TODO` / 空占位步骤。
- Type consistency: `ThumbnailStyleVisualProfile`、`ThumbnailStyleDefinition.VisualProfile`、`GalleryRenderer.DrawCard` 的命名一致。
- Scope check: 只改渲染层和风格配置，不引入新的数据流或业务流程。

