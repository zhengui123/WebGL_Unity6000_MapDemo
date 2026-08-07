# VSOC 大屏地图可视化 Demo（U6_MapDemo）

## 0. 引擎与打包版本（必读）

| 用途 | Unity 版本 | 说明 |
|------|------------|------|
| **日常开发 / 当前工程打开** | **Unity 6 — `6000.3.16f1`** | 以 `ProjectSettings/ProjectVersion.txt` 为准；本仓库当前即此版本。 |
| **Android APK / AAB 正式打包** | **Unity 2022 LTS（必须）** | **Android 出包请使用 Unity 2022 系列**，不要用 Unity 6 直接打 Android 正式包。需在 2022 工程中打开/迁移本项目后再 Build Android。 |

> **重要：**  
> - 功能开发、场景编辑、WebGL 联调：优先使用 **Unity 6（6000.3.16f1）**。  
> - **Android 宿主集成与发布包：固定走 Unity 2022。**  
> - 从 Unity 6 切到 2022 时，注意包版本、渲染管线、Input System、第三方插件兼容性；建议单独维护「Android 出包分支 / 工程副本」，避免与 Unity 6 开发目录互相覆盖未验证资源。

---

## 1. 项目简介

本项目是 **VSOC（车联网安全运营）大屏中央可视化** Unity Demo，产品名 **VsocDemo**。

职责边界（概要）：

- **Unity**：地球 / 世界板块 / 省级聚焦 / 高德二维图 / 城市 / 车辆 / 零件 / 攻击路径等层级可视化与过渡动画；热力点、POI、威胁下钻、HTTP 数据拉取等。
- **Android / Web 宿主**：顶部指标、左右业务面板、事件列表、登录配对等 UI；通过桥接 API 驱动 Unity 层级与业务状态。

应用标识（当前工程配置）：

| 项 | 值 |
|----|-----|
| Product Name | `VsocDemo` |
| Company | `test` |
| Android 包名 | `com.test.VsocDemo` |
| Bundle Version | `0.1` |
| Android Bundle Version Code | `1` |
| Android Min / Target SDK | `30` / `30` |
| Android 架构 | ARM64（`AndroidTargetArchitectures: 2`） |

---

## 2. 目录与模块一览

```
Assets/Scripts/
├── API/                 # MapApi 等对外业务入口
├── AttackPath/          # 攻击路径可视化
├── Camera/              # 国家级缩放/平移等
├── Core/                # GameManager、EventManager、层级跳转、Android/WebGL 桥接
├── Demo/                # 各类 Demo UI
├── Http/                # HttpService、项目 HTTP 配置
├── Map/                 # 板块显示、热力点、POI、世界地图区域、边界数据
├── MessageData/         # 威胁态势、车辆态势、攻击链等业务数据与流程
├── OnlineMap/           # Online Maps / 高德 / 板块→城市编排
├── Transition/          # 地球↔板块、城市隐藏、车辆溶解、零件过渡等
├── UI/                  # 面板 Demo 等
└── Tools/               # 工具脚本
```

常用场景（`Assets/Scenes/`）：

- 主流程/联调常用：`EarthModelDemo.unity`（及若干变体如 `_OnlyApi`、`_NoCity` 等）
- 其它：`CarDemo.unity`、`AttackPathDemo.unity`、`CityScenes.unity` 等专项 Demo

第三方主要依赖：

- **Online Maps**（高德等在线地图）
- **DOTween / DOTweenPro**
- **Volumetric Fog**
- **Cinemachine**、**Post Processing**、**Input System**、**TextMeshPro** 等（见 `Packages/manifest.json`）

---

## 3. 操控层级（ControlState）

| 值 | 级别 | 说明 |
|----|------|------|
| 0 | 地球级 | Earth |
| 1 | 国家级 | 全国 / 国外大板块视图 |
| 2 | 省级 | 板块聚焦（国内省 / 国外国家单元） |
| 3 | 车辆级 | 城市 + 车辆模型 |
| 4 | 零件级 | 零部件拆解视图 |
| 5 | 攻击路径级 | 攻击链路可视化 |

核心控制器：

- `GameManager`：逻辑态、默认省、播放态（Default / Threat）、层级跳转相关开关等
- `ControlStateHierarchyTransitionController`：按邻接图逐步过渡；可配置「跳转中再跳是否加速完成并执行最新指令」
- `MapApi`：统一对外跳转与地图业务 API

---

## 4. 宿主对接

### 4.1 Android

- 脚本：`Assets/Scripts/Core/AndroidBridge/AndroidMessage.cs`
- 场景物体名固定：**`AndroidBridge`**
- 文档：[`Assets/Scripts/Core/AndroidBridge/AndroidMessage_API.md`](Assets/Scripts/Core/AndroidBridge/AndroidMessage_API.md)
- 通信：`UnitySendMessage("AndroidBridge", 方法名, json)`；Unity → Android 经 `MainActivity` 回调

### 4.2 WebGL（iframe / Vue）

- 脚本：`Assets/Scripts/Core/WebConmunication/WebGLApi/WebGLAPI.cs`
- 场景物体名：**`WebGLAPI`**
- 文档：[`Assets/Scripts/Core/WebConmunication/WebGLApi/WebGL_Iframe_API.md`](Assets/Scripts/Core/WebConmunication/WebGLApi/WebGL_Iframe_API.md)
- 通信：`postMessage` 信封（`source` / `method` / `arg`）

> Android 与 WebGL **业务方法名尽量对齐**，仅通道不同。

### 4.3 Android 打包专项说明

1. **必须使用 Unity 2022** 打开用于出包的工程副本 / 分支，再执行 Android Build。  
2. 确认 Player Settings：包名、签名、Min SDK、Target SDK、ARM64、IL2CPP 等与宿主要求一致。  
3. 桥接物体 `AndroidBridge` 必须在**首包场景**中存在并激活。  
4. Unity 6 开发中新增的包或 API 若在 2022 不可用，出包前需降级或条件编译，避免 Android 包编译失败。  
5. 建议出包前用 Android 真机验证：`TransitionToControlState`、威胁轮询启停、区域默认设置等关键 API。

---

## 5. 业务能力摘要

| 模块 | 说明 | 关键入口 |
|------|------|---------|
| 世界区域 | 国内 / 国外大板块切换与默认单元 | `SetWorldMapRegionDefaults`（`provinceCode`） |
| 板块热力点 | GPU Instancing 车辆点；经纬度→局部坐标 | `PlateMapVehiclePointController` / `PlateMapGeoConverter` |
| 威胁态势 | 高危事件轮询、达标省下钻、Vin 车辆/攻击路径/零件流程 | `ThreatAlertFlowRunner`、`HighRiskSecurityEventApiController` |
| HTTP | 队列 + 有限并发；业务 Controller 同业务防重入 | `HttpService` |
| 大屏轮播 | 自动轮播；威胁触发时可打断并从全国重进 | `BigScreenCarouselController` |
| 层级跳转打断策略 | GameManager 开关：跳转中是否加速完成并覆盖为最新指令（默认关） | `AcceleratePendingHierarchyTransition` |

---

## 6. 建议联调入口

1. 用 **Unity 6（6000.3.16f1）** 打开工程。  
2. 打开主场景（如 `EarthModelDemo`），确认 `GameManager`、`MapApi`、`AndroidBridge` / `WebGLAPI`、`ThreatAlertFlowRunner` 等常驻引用完整。  
3. Demo 面板：本地威胁测试、状态跳转 Demo、HTTP Demo 等（`Assets/Scripts/Demo`、`MessageData/Threat/Demo`）。  
4. 宿主联调：对照 Android / WebGL API 文档，先测层级跳转再测威胁与热力图。

---

## 7. 暂停开发时的交接注意

- 当前开发引擎：**Unity 6 `6000.3.16f1`**。  
- **Android 正式包：Unity 2022。**  
- 勿将 `Library/`、`Temp/`、大体量 WebGL 构建产物当作源码提交重点。  
- 接口变更请同步更新：  
  - `AndroidMessage_API.md`  
  - `WebGL_Iframe_API.md`  
- 威胁 / HTTP / 区域切换近期改动较多，恢复开发时优先回归：国外↔国内区域切换、威胁非国家级触发回全国、层级跳转中打断开关。

---

## 8. 文档索引

| 文档 | 路径 |
|------|------|
| 本说明 | `PROJECT_README.md`（仓库根目录） |
| Android 桥接 API | `Assets/Scripts/Core/AndroidBridge/AndroidMessage_API.md` |
| WebGL iframe API | `Assets/Scripts/Core/WebConmunication/WebGLApi/WebGL_Iframe_API.md` |
| Unity 编辑器版本 | `ProjectSettings/ProjectVersion.txt` |

---

*文档整理日期：2026-08-07（暂停开发交接用）*
