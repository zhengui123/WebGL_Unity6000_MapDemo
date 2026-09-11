# Android / WebGL 开放接口说明 — 新旧对照

> 对比基线：旧版文档 `6f0bf71`（调整开放的 api 接口说明）→ 当前 `HEAD`  
> 适用范围：`AndroidMessage_API.md` 与 `WebGL_Iframe_API.md`（方法名对齐，通信通道不同）  
> 当前完整说明：同目录 [`AndroidMessage_API.md`](./AndroidMessage_API.md)；WebGL 见 `WebConmunication/WebGLApi/WebGL_Iframe_API.md`

---

## 一、新增接口（宿主 → Unity）

| 方法 | 作用 |
|------|------|
| `StartThreatHighRiskPolling` | 开启威胁高危事件定时轮询（默认约 60s） |
| `StopThreatHighRiskPolling` | 停止威胁高危事件定时轮询 |
| `SetWorldMapRegionDefaults` | 设置国内外默认区域并立刻切换（替代旧默认省接口） |
| `RequestVehicleHeatmapOnce` | 按时间 / `isReplay` **只请求一次**热力图（不改轮询） |
| `SetHttpRequestHeaders` | 运行时覆盖 `apiHost` / `appSecret` / 请求头 |
| `SetUiLanguage` | 切换场景 UI 语言（`zh` / `en` 等，仅固定标签） |

---

## 二、移除 / 替换的接口

| 旧版 | 当前 |
|------|------|
| `SetDefaultProvinceCode`（code / JSON 设默认省 adcode） | **已移除**，改用 `SetWorldMapRegionDefaults` |

---

## 三、有变动的接口（方法名保留，语义 / 字段变更）

| 方法 | 变动要点 |
|------|----------|
| `TransitionToControlState` | 请求侧：`provinceName` / `provinceModuleName` → **`provinceCode`**（国内 adcode / 国外 SOC） |
| `ExitThreatDrill` | 冷却期间会**暂停**高危轮询；冷却结束后若曾 `StartThreatHighRiskPolling`，会先请求再恢复 |
| `RefreshThreatCooldown` | 文档与冷却 / 轮询联动说明同步更新（方法名未改） |
| `onUnityControlStateTransition`（Unity → 宿主回调） | `status`：由「预留恒 0」→ **真实播放状态** `0` 默认 / `1` 告警定位 / `2` 威胁；**新增** `provinceCode`、`vin` |
| 零件示例 `partId` | 示例由 `Group01/02/03` 等改为业务码如 `IDC` / `CCU` / `TBOX` / `ADC` / `WG` |

---

## 四、未改名的主要存量接口（对照）

宿主 → Unity：

- `TransitionToNextControlState` / `TransitionToPreviousControlState`
- `SetBigScreenAutoCarouselEnabled`
- `PauseGame` / `ResumeGame`
- `CloseCarUI` / `CloseGJPanel`
- `StartVehicleHeatmapSpecifiedTimePolling` / `StopVehicleHeatmapSpecifiedTimePolling`
- `RequestCarVehicleData` / `RequestSecurityEventDetail`
- `SetCarYawRotation`
- 地图过渡辅助：`TransitionToPlateMap` / `FocusPlateMapModule` / `RestorePlateMapCamera` / `TransitionToEarth` 等

Unity → 宿主：

- `onUnityCarYawRotationChanged`
- WebGL 另有：`onUnityWebGLReady`

---

## 五、相关提交（文档演进摘要）

| Commit | 说明 |
|--------|------|
| `b0eb709` | 更新 API 接口 |
| `dcf8a01` | 修改对外接口（含国内外 / 热力图一次请求等方向） |
| `83467c6` | 回调增加当前屏幕播放状态等字段 |
| `e7b2539` | 威胁定时请求检测接口 |
| `40614be` | 修改国内外切换接口 |
| `85bca3d` | 后端 IP / HTTP 配置接口 |
| `b7119ea` | 中英文切换接口 |

---

## 六、接入迁移提示

1. 原调用 `SetDefaultProvinceCode` 的代码，改为 `SetWorldMapRegionDefaults`（国内传省 `provinceCode`，国外传国家 SOC 等，见现行 API 文档示例）。
2. `TransitionToControlState` 不要再传 `provinceName` / `provinceModuleName`，改传 `provinceCode`。
3. 解析 `onUnityControlStateTransition` 时请读取 `status`、`provinceCode`、`vin`（可能为空字符串）。
4. 需要运行时切后端主机或 Token 时用 `SetHttpRequestHeaders`；切 UI 语言用 `SetUiLanguage`。
