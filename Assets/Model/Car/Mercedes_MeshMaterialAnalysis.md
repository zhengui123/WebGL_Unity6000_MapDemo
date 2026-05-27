# Mercedes 模型 — 材质与子网格分析

> 由 `Tools/Car/分析 Mercedes 材质与子网格` 自动生成

## 参考图材质关键词

| 关键词 | 说明 |
|--------|------|
| Hologram / 全息 | 半透明数字投影感 |
| Fresnel / Rim | 边缘比正面更亮，勾勒轮廓 |
| Wireframe / Grid | 表面网格线随几何起伏 |
| Emission / Bloom | 青蓝色自发光，需后处理 Bloom |
| Transparent / Alpha | 可透视内饰结构 |
| Scanlines | 轻微水平扫描纹 |
| Cyberpunk / Sci-fi | 整体青蓝霓虹配色 |

## FBX 内嵌材质（Blender 导出）

| 材质名 | 典型部位 |
|--------|----------|
| body | 车身外壳 |
| glass | 玻璃 |
| interior plastic | 内饰塑料 |
| underbody.003 | 底盘 |
| wheel1 ~ wheel4 | 四个车轮 |

贴图引用：`illinoisplatemerc.jpg`、`plate_normal_merc.png`（车牌）

## Unity 导入后 MeshRenderer 明细

共 **6** 个 MeshRenderer

| 节点路径 | Mesh | SubMesh 数 | 当前材质 |
|----------|------|------------|----------|
| mercedes(Clone)/body | body | 13 | black, black plastic, M_MercedesHologram, bulb red, black_black, metallic, M_MercedesHologram, M_MercedesHologram, bulb, rear bulb, Material, illinoisplatemerc, red |
| mercedes(Clone)/suspension | suspension | 1 | black |
| mercedes(Clone)/wheel1 | wheel1 | 4 | tyre, metallic rim, metallic, red |
| mercedes(Clone)/wheel2 | wheel2 | 3 | tyre, metallic rim, metallic |
| mercedes(Clone)/wheel3 | wheel3 | 4 | tyre, metallic rim, metallic, red |
| mercedes(Clone)/wheel4 | wheel4 | 3 | tyre, metallic rim, metallic |

## 项目内全息材质

- Shader: `Assets/Shaders/Car/CarHologram.shader`
- Material: `Assets/Materials/Car/M_MercedesHologram.mat`
- 菜单: `Tools/Car/创建全息材质并应用到 Mercedes`

## 项目约定（材质与运行时）

1. **单一 Body 材质**：全车统一 `M_MercedesHologram`，不分部件、不按子网格区分。
2. **运行时不再自动赋材质**：Play 以场景中已保存的材质为准；勿在 `Awake` 中批量改写 `sharedMaterials`。
3. **手调材质不覆盖**：`M_MercedesHologram` / `M_MercedesEdgeOutline` 为基准；实验请新建命名区分的 `.mat`。
4. **双层场景**：`mercedes`（全息）+ `mercedes_edge`（边缘线静态副本），禁止运行时生成边缘子物体。

> AI/协作规则详见：`.cursor/rules/car-mercedes-hologram.mdc`

## 建议

1. 主相机 Background 设为深色；开启 **Bloom** 增强发光。
2. 效果微调请直接改 `M_MercedesHologram` / `M_MercedesEdgeOutline`，或复制为新材质球再试。
