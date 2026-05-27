# Mercedes 模型 — 材质与子网格分析

> 由 `Tools/Car/分析 Mercedes 材质与子网格` 在 Unity 中可刷新完整 MeshRenderer 表

## 参考图材质关键词

| 关键词 | 说明 |
|--------|------|
| Hologram / 全息 | 半透明数字投影感 |
| Fresnel / Rim | 边缘比正面更亮，勾勒轮廓 |
| Wireframe / Grid | 表面网格线随几何起伏 |
| Emission / Bloom | 青蓝色自发光，需后处理 Bloom |
| Transparent / Alpha | 可透视内饰结构 |
| Scanlines | 轻微水平扫描纹 |
| X-Ray | 正面较暗、结构可透视 |
| Cyberpunk / Sci-fi | 青蓝霓虹配色 |

## FBX 内嵌材质（Blender 导出）

| 材质名 | 典型部位 |
|--------|----------|
| body | 车身外壳 |
| glass | 玻璃 |
| interior plastic | 内饰塑料 |
| underbody.003 | 底盘 |
| wheel1 ~ wheel4 | 四个车轮 |

贴图：`illinoisplatemerc.jpg`、`plate_normal_merc.png`（车牌）

## 项目资源

| 类型 | 路径 |
|------|------|
| Shader | `Assets/Shaders/Car/CarHologram.shader` |
| Material | `Assets/Materials/Car/M_MercedesHologram.mat` |
| 菜单 | `Tools/Car/创建全息材质并应用到 Mercedes` |
| FBX Remap | `mercedes.fbx.meta` 已将 8 个材质映射到全息材质 |
| 场景启动 | `Car` 场景中 `CarHologramSetup` 会在运行时自动应用 |

## FBX 材质 Remap（已完成）

`body` / `glass` / `interior plastic` / `underbody.003` / `wheel1~4` → `M_MercedesHologram`

Unity 中执行 **`Tools/Car/重新导入 Mercedes（应用 FBX 材质映射）`** 可强制刷新导入。

## 后处理建议

1. 深色背景 + **Bloom** 增强青蓝发光。
2. 玻璃可单独复制材质并降低 `_Alpha`。
