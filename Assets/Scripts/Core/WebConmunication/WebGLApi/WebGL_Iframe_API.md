# WebGL iframe 通信 API 接口文档

本文档为 **Vue / 任意前端父页面** 嵌入 Unity WebGL（iframe）时的 **接口调用规范**，包含完整示例、JSON 字段可空说明及行为说明。

> 架构与扩展指南见同目录 `[WebGL_Vue_Communication.md](./WebGL_Vue_Communication.md)`  
> 示例页面：`[Assets/Plugins/Web/WebJs/vue-parent-demo/vue-parent-standalone.html](../../../../Plugins/Web/WebJs/vue-parent-demo/vue-parent-standalone.html)`  
> Android 对照：`[AndroidMessage_API.md](../../AndroidBridge/AndroidMessage_API.md)`

---

## 1. 前置条件


| 项目                 | 要求                                                            |
| ------------------ | ------------------------------------------------------------- |
| Unity 桥接物体名        | `WebGLAPI`（挂载 `WebGLAPI.cs`）                                  |
| Build `index.html` | `createUnityInstance` 成功后设置 `window.unityInstance = instance` |
| 访问方式               | 建议 HTTP（`python -m http.server`），避免 `file://`                 |
| 就绪时机               | 收到 `onUnityWebGLReady` 后再调用业务接口                               |


---

## 2. 消息信封（Envelope）

所有通信均通过浏览器 `postMessage`，**不**直接调用跨域 `parent` / `iframe` 上的函数。

### 2.1 父页面 → Unity

```javascript
iframe.contentWindow.postMessage({
  source: 'webgl-unity-parent',  // 固定，jslib 据此过滤
  method: 'MethodName',          // 对应 WebGLAPI.cs 的 public 方法名（区分大小写）
  arg: '...'                     // 字符串；无参接口传 ''；JSON 接口传 JSON 字符串
}, '*');
```

**内部转发：**

```
Communication.jslib
  → unityInstance.SendMessage('WebGLAPI', method, arg)
  → WebGLAPI.cs 对应 public 方法
```

### 2.2 Unity → 父页面

```javascript
// Unity 侧 CallHTMLHandler 发出
{
  source: 'unity-webgl',
  method: 'onUnityXxx',   // 回调方法名
  message: '...'          // 字符串；JSON 回调为 JSON 字符串
}
```

**父页面监听：**

```javascript
window.addEventListener('message', (event) => {
  const data = event.data;
  if (!data || data.source !== 'unity-webgl') return;
  // data.method / data.message
});
```

### 2.3 通用调用封装

```javascript
const SOURCE_PARENT = 'webgl-unity-parent';
const SOURCE_UNITY = 'unity-webgl';

function getUnityIframe() {
  return document.querySelector('iframe'); // 或你的 ref
}

/** 父 → Unity */
function callUnity(method, arg = '') {
  const win = getUnityIframe()?.contentWindow;
  if (!win) throw new Error('iframe 未就绪');
  win.postMessage({ source: SOURCE_PARENT, method, arg: arg ?? '' }, '*');
}

let unityReady = false;
window.addEventListener('message', (e) => {
  if (e.data?.source === SOURCE_UNITY && e.data.method === 'onUnityWebGLReady') {
    unityReady = true;
  }
});
```

---

## 3. 操控级别（ControlState）


| 值   | 名称   | 说明      |
| --- | ---- | ------- |
| `0` | 地球   | 地球视图    |
| `1` | 国家   | 国家/板块地图 |
| `2` | 省级   | 省级聚焦    |
| `3` | 车辆   | 车辆视图    |
| `4` | 零件   | 零部件视图   |
| `5` | 攻击路径 | 攻击路径视图  |


层级主干：`0 → 1 → 2 → 3`；`3` 下可分支到 `4`（零件）或 `5`（攻击路径），须经车辆级衔接。

### 3.1 partId 有效取值

`partId` 为场景中 `VehicleToPartTransitionController` 已配置的零件 ID（`partId` 为空时取 GameObject 名），**当前项目支持以下取值**（区分大小写）：


| partId  | 说明   |
| ------- | ---- |
| `IDC`   | 智驾域控 |
| `CCU`   | 中央计算 |
| `TBOX`  | 车联网终端 |
| `ADC`   | ADC  |
| `WG`    | WG   |


文档示例 **仅使用上述取值**。传入其它值时 Unity 可能无法匹配零件或切换失败。

省略 `partId` 或传 `""` 时，使用过渡控制器默认配置或列表首项（通常为 `IDC`）。

---

## 4. 父页面 → Unity 接口

> **共性说明**
>
> - 所有接口均为 **异步发起**，Unity **不会**通过回调告知「调用成功/失败」。
> - 失败时 Unity Console 输出 `[WebGLAPI]` 警告；父页面可通过 `onUnityControlStateTransition` 间接感知过渡是否发生。
> - `method` 必须与 `WebGLAPI.cs` 中 `public` 方法名 **完全一致**（区分大小写）。
> - `SendMessage` 仅支持字符串参数：无参接口 `arg` 传 `""`。

---

### 4.1 TransitionToControlState

按目标级别执行层级跳转（可跨多级，Unity 内部按邻接图逐步过渡）。


| 项目       | 值                                                |
| -------- | ------------------------------------------------ |
| `method` | `TransitionToControlState`                       |
| `arg`    | JSON 字符串                                         |
| Unity 方法 | `WebGLAPI.TransitionToControlState(string json)` |
| 业务       | `MapApi.TransitionToControlState`                |


#### 请求 JSON 字段

结构体：`TransitionToControlStateRequest`（`AndroidMessage.cs`）


| 字段                     | 类型     | 必填  | 可空/默认                         | 说明                                                                 |
| ---------------------- | ------ | --- | ----------------------------- | ------------------------------------------------------------------ |
| `targetState`          | int    | ✅   | —                             | 目标级别 `0~5`；非法值忽略请求                                                 |
| `provinceCode`         | string |     | 可省略 / `""` / 空白 → Unity 用默认单元 | 省/国家 code：国内 adcode（如 `"370000"`）/ 国外 SOC（如 `"392"`）；内部解析显示名与板块模块名 |
| `partId`               | string |     | 可省略 / `""` / 空白 → 控制器默认或列表首项  | 零件 ID，有效值：`IDC`、`CCU`、`TBOX`、`ADC`、`WG`                           |
| `useInstantTransition` | bool   |     | 省略 → `false`                  | `true` 时跳过过渡动画（临时置 0 时长）                                           |


**可空规则（Unity 侧）：**

- `provinceCode` / `partId`：字段省略、空字符串、仅空白，均视为 `null`，走 Unity 默认逻辑。
- `useInstantTransition`：JSON 中省略时，`JsonUtility` 解析为 `false`。
- `targetState`：**不可省略**；若省略会被解析为 `0`（地球级），通常非预期。

#### 完整 JSON 示例

**最小（仅目标级别）：**

```json
{"targetState":3}
```

```javascript
callUnity('TransitionToControlState', '{"targetState":3}');
```

**完整（所有字段）：**

```json
{
  "targetState": 2,
  "provinceCode": "370000",
  "partId": "",
  "useInstantTransition": false
}
```

```javascript
callUnity('TransitionToControlState', JSON.stringify({
  targetState: 2,
  provinceCode: '370000',
  partId: '',
  useInstantTransition: false,
}));
```

**跳到省级（常用）：**

```json
{"targetState":2,"provinceCode":"370000"}
```

**跳到零件级：**

```json
{"targetState":4,"partId":"IDC"}
```

**已在零件级时切换零件（targetState 仍为 4）：**

```json
{"targetState":4,"partId":"CCU"}
```

**切换到 TBOX：**

```json
{"targetState":4,"partId":"TBOX"}
```

**攻击路径级跳到零件：**

```json
{"targetState":4,"partId":"IDC"}
```

**瞬时跳转（无动画）：**

```json
{"targetState":1,"useInstantTransition":true}
```

**从国家直接跳到车辆（Unity 内部多步执行）：**

```json
{"targetState":3,"provinceCode":"440000"}
```

#### 特殊行为说明


| 场景                                    | 行为                                             |
| ------------------------------------- | ---------------------------------------------- |
| 当前已在零件级，`targetState=4` 且带 `partId`   | 走零件切换，非完整层级重跳                                  |
| 当前已在攻击路径级，`targetState=4` 且带 `partId` | 走攻击路径→零件直连                                     |
| 跳转进行中再次调用                             | 可能返回 false 并被忽略（控制器忙）                          |
| 跨多级（如 1→4）                            | 每一级过渡开始/完成均会回调 `onUnityControlStateTransition` |


---

### 4.2 TransitionToNextControlState

进入操控层级 **下一级**（等同 Unity 内双击操作）。


| 项目       | 值                                         |
| -------- | ----------------------------------------- |
| `method` | `TransitionToNextControlState`            |
| `arg`    | `""`（空字符串）                                |
| Unity 方法 | `WebGLAPI.TransitionToNextControlState()` |


```javascript
callUnity('TransitionToNextControlState', '');
```

**说明：** 无 JSON 参数；在当前级别无法继续「下一级」时 Unity 侧启动失败并打日志。

---

### 4.3 TransitionToPreviousControlState

返回操控层级 **上一级**（等同系统返回键）。


| 项目       | 值                                             |
| -------- | --------------------------------------------- |
| `method` | `TransitionToPreviousControlState`            |
| `arg`    | `""`                                          |
| Unity 方法 | `WebGLAPI.TransitionToPreviousControlState()` |


```javascript
callUnity('TransitionToPreviousControlState', '');
```

---

### 4.4 SetBigScreenAutoCarouselEnabled

开启或关闭四个大屏的自动轮播。


| 项目       | 值                                                       |
| -------- | ------------------------------------------------------- |
| `method` | `SetBigScreenAutoCarouselEnabled`                       |
| `arg`    | JSON 字符串                                                |
| Unity 方法 | `WebGLAPI.SetBigScreenAutoCarouselEnabled(string json)` |


#### 请求 JSON 字段

结构体：`BigScreenAutoCarouselRequest`


| 字段        | 类型   | 必填  | 可空/默认        | 说明                   |
| --------- | ---- | --- | ------------ | -------------------- |
| `enabled` | bool | ✅   | 省略 → `false` | `true` 开启，`false` 关闭 |


**开启：**

```json
{"enabled":true}
```

```javascript
callUnity('SetBigScreenAutoCarouselEnabled', '{"enabled":true}');
```

**关闭：**

```json
{"enabled":false}
```

```javascript
callUnity('SetBigScreenAutoCarouselEnabled', JSON.stringify({ enabled: false }));
```

---

### 4.5 PauseGame / ResumeGame

暂停或恢复游戏（`Time.timeScale` + 全部 DOTween）。


| 项目       | 值                                       |
| -------- | --------------------------------------- |
| `method` | `PauseGame` / `ResumeGame`              |
| `arg`    | `""`                                    |
| Unity 方法 | `WebGLAPI.PauseGame()` / `ResumeGame()` |
| MapApi   | `PauseGame()` / `ResumeGame()`          |


```javascript
callUnity('PauseGame', '');
callUnity('ResumeGame', '');
```

---

### 4.6 ExitThreatDrill

主动退出威胁下钻：保持**当前操控级别**，进入冷却（默认约 180s）。冷却期间不再检测威胁，并**暂停**高危事件定时轮询；冷却结束后若此前已开启 `StartThreatHighRiskPolling`，会先请求一次接口再恢复轮询。自然跑完威胁流程不会进入冷却。


| 项目       | 值                            |
| -------- | ---------------------------- |
| `method` | `ExitThreatDrill`            |
| `arg`    | `""`                         |
| Unity 方法 | `WebGLAPI.ExitThreatDrill()` |
| MapApi   | `ExitThreatDrill()`          |


```javascript
callUnity('ExitThreatDrill', '');
```

---

### 4.7 RefreshThreatCooldown

刷新威胁冷却倒计时（**仅冷却中有效**，重新计满配置秒数）。未在冷却中时 Unity Console 警告并失败。


| 项目       | 值                                  |
| -------- | ---------------------------------- |
| `method` | `RefreshThreatCooldown`            |
| `arg`    | `""`                               |
| Unity 方法 | `WebGLAPI.RefreshThreatCooldown()` |
| MapApi   | `RefreshThreatCooldown()`          |


```javascript
callUnity('RefreshThreatCooldown', '');
```

---

### 4.8 StartThreatHighRiskPolling

开启威胁高危安全事件定时轮询（默认间隔 60s，启动后立即请求一次）。组件默认开局自动轮询（Inspector `_autoStart`，可关）；宿主仍可用本接口手动启停。威胁流程进行中仍继续轮询。冷却中调用只记录意图，冷却结束后再真正请求。


| 项目       | 值                                       |
| -------- | --------------------------------------- |
| `method` | `StartThreatHighRiskPolling`            |
| `arg`    | `""`                                    |
| Unity 方法 | `WebGLAPI.StartThreatHighRiskPolling()` |
| MapApi   | `StartThreatHighRiskPolling()`          |


```javascript
callUnity('StartThreatHighRiskPolling', '');
```

---

### 4.9 StopThreatHighRiskPolling

停止威胁高危事件定时轮询并清除意图；冷却结束后也不会自动恢复。


| 项目       | 值                                      |
| -------- | -------------------------------------- |
| `method` | `StopThreatHighRiskPolling`            |
| `arg`    | `""`                                   |
| Unity 方法 | `WebGLAPI.StopThreatHighRiskPolling()` |
| MapApi   | `StopThreatHighRiskPolling()`          |


```javascript
callUnity('StopThreatHighRiskPolling', '');
```

---

### 4.10 SetWorldMapRegionDefaults

设置国内/国外、国外大板块、默认单元 code，并立刻切换世界地图区域（同 `WorldMapRegionController` 面板）。  
取代原 `SetDefaultProvinceCode`。


| 项目       | 值                                                 |
| -------- | ------------------------------------------------- |
| `method` | `SetWorldMapRegionDefaults`                       |
| `arg`    | JSON                                              |
| Unity 方法 | `WebGLAPI.SetWorldMapRegionDefaults(string json)` |
| MapApi   | `SetWorldMapRegionDefaults(...)`                  |



| 字段                 | 类型     | 必填  | 说明                                 |
| ------------------ | ------ | --- | ---------------------------------- |
| `regionMode`       | int    | 是   | `0`=国内，`1`=国外                      |
| `foreignPlateCode` | string | 国外是 | 大板块 code，如 `EAST_ASIA`；国内可空        |
| `defaultUnitCode`  | string | 否   | 国内=省级 adcode；国外=国家 SOC。空则沿用现有/绑定默认 |


```javascript
// 国内
callUnity('SetWorldMapRegionDefaults', JSON.stringify({
  regionMode: 0,
  foreignPlateCode: '',
  defaultUnitCode: '330000'
}));

// 国外东亚
callUnity('SetWorldMapRegionDefaults', JSON.stringify({
  regionMode: 1,
  foreignPlateCode: 'EAST_ASIA',
  defaultUnitCode: '392'
}));
```

---

### 4.11 CloseCarUI

停止零部件防护状态轮播，并关闭车辆 UI / 连线。


| 项目       | 值                                          |
| -------- | ------------------------------------------ |
| `method` | `CloseCarUI`                               |
| `arg`    | `""`                                       |
| Unity 方法 | `WebGLAPI.CloseCarUI()`                    |
| MapApi   | `CloseCarVehicleDataUi()` / `CloseCarUI()` |


```javascript
callUnity('CloseCarUI', '');
```

---

### 4.12 CloseGJPanel

关闭告警面板 `GJ_Panel`。


| 项目       | 值                         |
| -------- | ------------------------- |
| `method` | `CloseGJPanel`            |
| `arg`    | `""`                      |
| Unity 方法 | `WebGLAPI.CloseGJPanel()` |
| MapApi   | `CloseGJPanel()`          |


```javascript
callUnity('CloseGJPanel', '');
```

---

### 4.13 RequestVehicleHeatmapOnce

主动请求一次车辆热力图（**不轮询**）：按起止时间与 `isReplay` 发一次后端请求并走现有点位处理；不改轮询状态。


| 项目       | 值                                                         |
| -------- | --------------------------------------------------------- |
| `method` | `RequestVehicleHeatmapOnce`                               |
| `arg`    | JSON，或 `""`                                               |
| Unity 方法 | `WebGLAPI.RequestVehicleHeatmapOnce(string json)`         |
| MapApi   | `RequestVehicleHeatmapOnce(startTime, endTime, isReplay)` |



| 字段          | 类型     | 必填  | 说明                      |
| ----------- | ------ | --- | ----------------------- |
| `startTime` | string | 否   | 空则不传 start              |
| `endTime`   | string | 否   | 空则用当前时间                 |
| `isReplay`  | bool   | 否   | 是否使用历史数据（后端 `isReplay`） |


```javascript
callUnity('RequestVehicleHeatmapOnce', JSON.stringify({
  startTime: '2026-06-30 00:00:00',
  endTime: '2026-06-30 23:00:00',
  isReplay: true
}));

callUnity('RequestVehicleHeatmapOnce', '');
```

---

### 4.14 StartVehicleHeatmapSpecifiedTimePolling

开启车辆热力图指定时段轮询：固定起止时间，请求 `isReplay=true`。轮询间隔与默认模式相同。


| 项目       | 值                                                               |
| -------- | --------------------------------------------------------------- |
| `method` | `StartVehicleHeatmapSpecifiedTimePolling`                       |
| `arg`    | JSON                                                            |
| Unity 方法 | `WebGLAPI.StartVehicleHeatmapSpecifiedTimePolling(string json)` |
| MapApi   | `StartVehicleHeatmapSpecifiedTimePolling(startTime, endTime)`   |



| 字段          | 类型     | 必填  | 说明     |
| ----------- | ------ | --- | ------ |
| `startTime` | string | 是   | 查询开始时间 |
| `endTime`   | string | 是   | 查询结束时间 |


```javascript
callUnity('StartVehicleHeatmapSpecifiedTimePolling', JSON.stringify({
  startTime: '2026-06-30 00:00:00',
  endTime: '2026-06-30 23:00:00'
}));
```

---

### 4.15 StopVehicleHeatmapSpecifiedTimePolling

关闭指定时段模式，恢复默认轮询（`startTime` 空、`endTime` 当前时间、`isReplay=false`）。


| 项目       | 值                                                   |
| -------- | --------------------------------------------------- |
| `method` | `StopVehicleHeatmapSpecifiedTimePolling`            |
| `arg`    | `""`                                                |
| Unity 方法 | `WebGLAPI.StopVehicleHeatmapSpecifiedTimePolling()` |
| MapApi   | `StopVehicleHeatmapSpecifiedTimePolling()`          |


```javascript
callUnity('StopVehicleHeatmapSpecifiedTimePolling', '');
```

---

### 4.16 RequestCarVehicleData

请求车辆态势双接口（零部件防护状态 + 攻击链路）。两端均成功后覆盖本地缓存；若当前已在车辆级，会打开车辆 UI 并开始零部件轮播。无成功/失败回调（只发不回）。


| 项目       | 值                                                       |
| -------- | ------------------------------------------------------- |
| `method` | `RequestCarVehicleData`                                 |
| `arg`    | `""`（默认参数）或 JSON                                        |
| Unity 方法 | `WebGLAPI.RequestCarVehicleData(string json)`           |
| MapApi   | `RequestCarVehicleData(encryptVin, startTime, endTime)` |



| 字段           | 类型     | 必填  | 说明                  |
| ------------ | ------ | --- | ------------------- |
| `encryptVin` | string | 否   | 加密 VIN；空则用 Unity 默认 |
| `startTime`  | string | 否   | 查询开始时间；可空串          |
| `endTime`    | string | 否   | 查询结束时间；空则用 Unity 默认 |


```javascript
// 使用默认参数
callUnity('RequestCarVehicleData', '');

// 指定参数
callUnity('RequestCarVehicleData', JSON.stringify({
  encryptVin: 'ed49f47afa23e45b18d342767495643c',
  startTime: '',
  endTime: '2026-06-30 23:00:00'
}));
```

---

### 4.17 RequestSecurityEventDetail

请求事件溯源详情（getSourceEventDetail）。成功后 Unity 侧缓存数据、刷新 `GJ_Panel`，并按经纬度生成 POI。无成功/失败回调（只发不回）。


| 项目       | 值                                                                                 |
| -------- | --------------------------------------------------------------------------------- |
| `method` | `RequestSecurityEventDetail`                                                      |
| `arg`    | `""`（默认参数）或 JSON                                                                  |
| Unity 方法 | `WebGLAPI.RequestSecurityEventDetail(string json)`                                |
| MapApi   | `RequestSecurityEventDetail(eventId, processStartTime, processEndTime, tenantId)` |



| 字段                 | 类型     | 必填  | 说明                 |
| ------------------ | ------ | --- | ------------------ |
| `eventId`          | string | 否   | 事件 ID；空则用 Unity 默认 |
| `processStartTime` | string | 否   | 处理开始时间             |
| `processEndTime`   | string | 否   | 处理结束时间             |
| `tenantId`         | int    | 否   | 租户 ID；默认 1         |


```javascript
// 使用默认参数
callUnity('RequestSecurityEventDetail', '');

// 指定参数
callUnity('RequestSecurityEventDetail', JSON.stringify({
  eventId: '123dfdsafffff',
  processStartTime: '2026-06-30 17:41:23',
  processEndTime: '2026-06-30 17:41:23',
  tenantId: 1
}));
```

---

### 4.18 SetCarYawRotation

设置车辆 3D 模型绕 Y 轴旋转（对应 `MouseDragYawRotate`）。

> **正式业务通常无需调用。** 车辆旋转由大屏拖拽驱动；父页面应监听 `onUnityCarYawRotationChanged` 同步朝向。本接口供联调 / 自动化测试使用。


| 项目       | 值                                         |
| -------- | ----------------------------------------- |
| `method` | `SetCarYawRotation`                       |
| `arg`    | JSON 字符串                                  |
| Unity 方法 | `WebGLAPI.SetCarYawRotation(string json)` |



| 字段         | 类型    | 必填  | 说明                 |
| ---------- | ----- | --- | ------------------ |
| `yawAngle` | float | 是   | 目标 Yaw，0~360       |
| `instant`  | bool  | 否   | 是否立即到位；省略为 `false` |


```javascript
callUnity('SetCarYawRotation', JSON.stringify({ yawAngle: 90.0, instant: false }));
```

---

### 4.19 其它地图过渡（可选 / 联调）

一般优先使用 `TransitionToControlState`；以下为底层地图过渡直调：


| method                  | arg    | 说明       |
| ----------------------- | ------ | -------- |
| `TransitionToPlateMap`  | `""`   | 地球 → 板块  |
| `TransitionToEarth`     | `""`   | 板块 → 地球  |
| `FocusPlateMapModule`   | 模块名字符串 | 聚焦指定板块模块 |
| `RestorePlateMapCamera` | `""`   | 还原板块相机   |


```javascript
callUnity('TransitionToPlateMap', '');
callUnity('FocusPlateMapModule', 'polySurface3');
callUnity('RestorePlateMapCamera', '');
callUnity('TransitionToEarth', '');
```

---

### 4.20 SetHttpRequestHeaders

> **编号约定：** 新增父页面 → Unity 接口一律追加在本章节末尾（本条之后继续递增），不插入既有章节中间。

运行时合并覆盖 HTTP 默认请求头（叠在 `HttpBackendConfig.json` / 程序默认之上）。后续业务请求自动使用。


| 项目       | 值                                             |
| -------- | --------------------------------------------- |
| `method` | `SetHttpRequestHeaders`                       |
| `arg`    | JSON                                          |
| Unity 方法 | `WebGLAPI.SetHttpRequestHeaders(string json)` |
| MapApi   | `SetHttpRequestHeaders(headers)`              |



| 字段                | 类型     | 必填  | 说明                                          |
| ----------------- | ------ | --- | ------------------------------------------- |
| `headers`         | array  | 是   | 请求头列表                                       |
| `headers[].key`   | string | 是   | 如 `Satoken` / `X-Tenant-Id` / `Sys-Lang` |
| `headers[].value` | string | 是   | 非空才写入；空/空白不改变该 key                          |


**规则：** 未传入的 key 不动；value 为空不动；仅 key+value 均非空时覆盖/新增。

```javascript
callUnity('SetHttpRequestHeaders', JSON.stringify({
  headers: [
    { key: 'Satoken', value: '新token' },
    { key: 'X-Tenant-Id', value: '1' },
    { key: 'Sys-Lang', value: 'zh-CN' },
  ],
}));
```

---

### 4.21 接口汇总表（父 → Unity）


| method                                    | arg         | JSON | 说明                         |
| ----------------------------------------- | ----------- | ---- | -------------------------- |
| `TransitionToControlState`                | JSON        | ✅    | 跳转到指定操控级别                  |
| `TransitionToNextControlState`            | `""`        |      | 下一级                        |
| `TransitionToPreviousControlState`        | `""`        |      | 上一级                        |
| `SetBigScreenAutoCarouselEnabled`         | JSON        | ✅    | 大屏轮播开关                     |
| `PauseGame`                               | `""`        |      | 暂停游戏                       |
| `ResumeGame`                              | `""`        |      | 恢复游戏                       |
| `ExitThreatDrill`                         | `""`        |      | 退出威胁下钻并进入冷却                |
| `RefreshThreatCooldown`                   | `""`        |      | 刷新威胁冷却（仅冷却中）               |
| `StartThreatHighRiskPolling`              | `""`        |      | 开启威胁高危事件定时轮询（默认 60s）       |
| `StopThreatHighRiskPolling`               | `""`        |      | 停止威胁高危事件定时轮询               |
| `SetWorldMapRegionDefaults`               | JSON        | ✅    | 设置国内外默认并立刻切换               |
| `CloseCarUI`                              | `""`        |      | 关闭车辆 UI / 停止轮播             |
| `CloseGJPanel`                            | `""`        |      | 关闭告警面板 GJ_Panel            |
| `RequestVehicleHeatmapOnce`               | JSON / `""` | ✅    | 主动请求一次热力图（不轮询）             |
| `StartVehicleHeatmapSpecifiedTimePolling` | JSON        | ✅    | 开启热力图指定时段轮询                |
| `StopVehicleHeatmapSpecifiedTimePolling`  | `""`        |      | 关闭指定时段，恢复默认轮询              |
| `RequestCarVehicleData`                   | `""` / JSON | ✅    | 请求车辆态势双接口                  |
| `RequestSecurityEventDetail`              | `""` / JSON | ✅    | 请求事件溯源详情并刷新 GJ_Panel / POI |
| `SetCarYawRotation`                       | JSON        | ✅    | 设置车辆 Yaw（联调；生产一般监听回调）      |
| `TransitionToPlateMap`                    | `""`        |      | 地球 → 板块（可选联调）              |
| `TransitionToEarth`                       | `""`        |      | 板块 → 地球（可选联调）              |
| `FocusPlateMapModule`                     | 模块名         |      | 聚焦板块模块（可选联调）               |
| `RestorePlateMapCamera`                   | `""`        |      | 还原板块相机（可选联调）               |
| `SetHttpRequestHeaders`                   | JSON        | ✅    | 运行时合并覆盖 HTTP 默认请求头         |


> 已移除历史测试接口：`OnAndroidNotifyA/B`、`OnDataSyncResult`、`ShowMessage` 等不再由 `WebGLAPI` 暴露。  
> 与 Android `AndroidMessage` 业务方法名对齐；通道差异见 `WebGL_Vue_Communication.md` §八。

---

## 5. Unity → 父页面回调

父页面通过 `window.addEventListener('message', ...)` 接收，`data.source === 'unity-webgl'`。

```javascript
const handlers = {
  onUnityWebGLReady(message) { /* 桥接就绪 */ },
  onUnityControlStateTransition(message) { /* JSON，含 partId：IDC/CCU/TBOX/ADC/WG */ },
  onUnityCarYawRotationChanged(message) { /* JSON：yawAngle / isDragging */ },
};

window.addEventListener('message', (event) => {
  const data = event.data;
  if (!data || data.source !== 'unity-webgl' || typeof data.method !== 'string') return;
  handlers[data.method]?.(data.message ?? '');
});
```

> 当前 `WebGLAPI` 向父页面推送：`onUnityWebGLReady`、`onUnityControlStateTransition`、`onUnityCarYawRotationChanged`。

---

### 5.1 onUnityWebGLReady


| 项目        | 值                                          |
| --------- | ------------------------------------------ |
| 触发时机      | Unity `WebGLAPI.Start` → `NotifyHostReady` |
| `message` | `""`（空字符串）                                 |


```javascript
// 收到即表示可安全 callUnity
if (data.method === 'onUnityWebGLReady') {
  unityReady = true;
}
```

**说明：** 同时会冲刷 iframe 加载期间排队的父页面消息。

---

### 5.2 onUnityControlStateTransition

操控级别过渡 **开始** 与 **完成** 的统一回调。


| 项目        | 值                              |
| --------- | ------------------------------ |
| `message` | JSON 字符串                       |
| 结构体       | `ControlStateTransitionNotify` |


#### 回调 JSON 字段


| 字段             | 类型     | 必填  | 可空/默认        | 说明                                                         |
| -------------- | ------ | --- | ------------ | ---------------------------------------------------------- |
| `from`         | int    | ✅   | —            | 过渡**开始**：起始级别 `0~5`；**完成**：固定 `-1`                         |
| `to`           | int    | ✅   | —            | 目标级别 `0~5`                                                 |
| `status`       | int    |     | `0`（无 GameManager 时） | 当前大屏业务播放状态：`0` 默认、`1` 告警定位、`2` 威胁 |
| `provinceCode` | string |     | 取不到时为 `""`   | 当前区域 code；国内为省 adcode，国外大屏为国家/区域 code。优先聚焦板块 / 进省缓存，无则默认单元 |
| `vin`          | string |     | 无车辆上下文为 `""` | 当前车辆 VIN                                                   |
| `partId`       | string |     | 无零件场景为 `""`  | 零件相关场景为 `IDC` / `CCU` / `TBOX` / `ADC` / `WG`                  |


**过渡开始示例：**

```json
{"from":3,"to":4,"status":0,"provinceCode":"330000","vin":"ed49f47afa23e45b18d342767495643c","partId":""}
```

```javascript
function onUnityControlStateTransition(json) {
  const { from, to, status, provinceCode, vin, partId } = JSON.parse(json);
  // status: 0 默认 | 1 告警定位 | 2 威胁
  if (from === -1) {
    console.log('过渡完成，就绪级别', to, '零件', partId, '区域', provinceCode, '车辆', vin, '大屏播放状态', status);
    // 隐藏 Loading、刷新 UI
  } else {
    console.log('过渡开始', from, '→', to, '区域', provinceCode, '车辆', vin, '大屏播放状态', status);
    // 显示 Loading
  }
}
```

**过渡完成示例：**

```json
{"from":-1,"to":4,"status":0,"provinceCode":"330000","vin":"ed49f47afa23e45b18d342767495643c","partId":"IDC"}
```

**零件切换开始（4→4）：**

```json
{"from":4,"to":4,"status":0,"provinceCode":"330000","vin":"ed49f47afa23e45b18d342767495643c","partId":"CCU"}
```

**零件切换完成（切到 TBOX）：**

```json
{"from":-1,"to":4,"status":0,"provinceCode":"330000","vin":"ed49f47afa23e45b18d342767495643c","partId":"TBOX"}
```

#### 会触发的 from → to 场景


| from → to | 说明        |
| --------- | --------- |
| 0↔1       | 地球 ↔ 国家   |
| 1↔2       | 国家 ↔ 省级   |
| 2↔3       | 省级 ↔ 车辆   |
| 3↔4       | 车辆 ↔ 零件   |
| 4→4       | 零件切换      |
| 3↔5       | 车辆 ↔ 攻击路径 |
| 5→4       | 攻击路径 → 零件 |


> 跨多级跳转时，**每一级**都会：先回调开始（`from` 为 0~5），再回调完成（`from=-1`）。

---

### 5.3 onUnityCarYawRotationChanged

车辆 Yaw 变化回调（拖拽中连续推送；松手或 API 设角也会推送）。


| 项目        | 值                      |
| --------- | ---------------------- |
| `message` | JSON 字符串               |
| 结构体       | `CarYawRotationNotify` |



| 字段           | 类型    | 说明                                  |
| ------------ | ----- | ----------------------------------- |
| `yawAngle`   | float | 当前 Yaw，0~360                        |
| `isDragging` | bool  | `true` 拖拽中；`false` 松手 / API 设角过程或到位 |


```json
{"yawAngle":126.5,"isDragging":true}
```

```javascript
function onUnityCarYawRotationChanged(json) {
  const { yawAngle, isDragging } = JSON.parse(json);
  // 同步宿主端朝向展示
}
```

---

### 5.4 回调汇总表（Unity → 父）


| method                          | message 类型 | 说明             |
| ------------------------------- | ---------- | -------------- |
| `onUnityWebGLReady`             | 空          | 桥接就绪（WebGL 独有） |
| `onUnityControlStateTransition` | JSON       | 级别过渡开始/完成      |
| `onUnityCarYawRotationChanged`  | JSON       | 车辆 Yaw 变化      |


---

## 6. 端到端完整示例

### 6.1 父页面：等待就绪 → 跳省级 → 处理回调

```html
<iframe id="unity" src="./unity-build/index.html"></iframe>
<script>
  const iframe = document.getElementById('unity');
  let ready = false;

  function callUnity(method, arg = '') {
    iframe.contentWindow.postMessage(
      { source: 'webgl-unity-parent', method, arg }, '*'
    );
  }

  window.addEventListener('message', (e) => {
    const d = e.data;
    if (!d || d.source !== 'unity-webgl') return;

    switch (d.method) {
      case 'onUnityWebGLReady':
        ready = true;
        console.log('Unity 就绪');
        break;

      case 'onUnityControlStateTransition': {
        const t = JSON.parse(d.message);
        if (t.from === -1) {
          console.log('完成，级别', t.to, '零件', t.partId); // partId: IDC/CCU/TBOX/ADC/WG
        } else {
          console.log('开始', t.from, '→', t.to);
        }
        break;
      }

      case 'onUnityCarYawRotationChanged': {
        const y = JSON.parse(d.message);
        console.log('车辆 Yaw', y.yawAngle, 'dragging=', y.isDragging);
        break;
      }
    }
  });

  function jumpToPart() {
    if (!ready) return alert('Unity 未就绪');
    callUnity('TransitionToControlState', '{"targetState":4,"partId":"IDC"}');
  }

  function jumpToShandong() {
    if (!ready) return alert('Unity 未就绪');
    callUnity('TransitionToControlState', JSON.stringify({
      targetState: 2,
      provinceCode: '370000',
    }));
  }
</script>
```

### 6.2 典型业务流程 JSON 对照


| 业务意图         | 调用                                                                          |
| ------------ | --------------------------------------------------------------------------- |
| 回到地球         | `{"targetState":0}`                                                         |
| 进入国家地图       | `{"targetState":1}`                                                         |
| 聚焦山东省        | `{"targetState":2,"provinceCode":"370000"}`                                 |
| 进入车辆视图       | `{"targetState":3,"provinceCode":"370000"}`                                 |
| 查看零件 IDC     | `{"targetState":4,"partId":"IDC"}`                                          |
| 切换零件 CCU     | `{"targetState":4,"partId":"CCU"}`                                          |
| 切换零件 TBOX    | `{"targetState":4,"partId":"TBOX"}`                                         |
| 查看攻击路径       | `{"targetState":5}`                                                         |
| 攻击路径下看零件     | `{"targetState":4,"partId":"IDC"}`                                          |
| 关闭大屏轮播       | `SetBigScreenAutoCarouselEnabled` → `{"enabled":false}`                     |
| 设置国内默认省浙江    | `SetWorldMapRegionDefaults` → `{"regionMode":0,"defaultUnitCode":"330000"}` |
| 退出威胁下钻       | `ExitThreatDrill` → `""`                                                    |
| 刷新威胁冷却       | `RefreshThreatCooldown` → `""`（仅冷却中）                                        |
| 开启威胁轮询       | `StartThreatHighRiskPolling` → `""`                                         |
| 停止威胁轮询       | `StopThreatHighRiskPolling` → `""`                                          |


---

## 7. JsonUtility 与可空字段注意事项

Unity 使用 `JsonUtility.FromJson`，请遵守：


| 类型       | 字段省略时             | 建议                           |
| -------- | ----------------- | ---------------------------- |
| `int`    | 解析为 `0`           | `targetState` 必须显式传递         |
| `bool`   | 解析为 `false`       | 需要 `true` 时必须显式传             |
| `string` | 解析为 `null` 或 `""` | 省略与 `""` 等效，Unity 侧统一当「使用默认」 |


**字段名区分大小写**，必须与 C# struct 一致：

- ✅ `targetState`、`provinceName`、`partId`
- ❌ `target_state`、`ProvinceName`

**不支持：** `null` JSON 字面量作为类型化字段（`"partId": null` 在部分环境下行为不一致，建议省略字段或传 `""`）。

---

## 8. 联调与排错


| 现象                | 排查                                                          |
| ----------------- | ----------------------------------------------------------- |
| 父页面发了消息 Unity 无反应 | `source` 是否为 `webgl-unity-parent`；是否已收到 `onUnityWebGLReady` |
| iframe 空白         | 是否用 HTTP；Build 路径是否正确                                       |
| method 无效应        | 方法名大小写是否与 `WebGLAPI.cs` 一致                                  |
| JSON 不生效          | 字段名大小写；`targetState` 是否遗漏                                   |
| 收不到 Unity 回调      | 是否监听 `source === 'unity-webgl'`；是否跨域 iframe（须 postMessage）  |
| Unity Console 有警告 | `[WebGLAPI] TransitionToControlState 启动失败` 等，多为级别非法或正在过渡中   |


**Unity 日志：** `WebGLAPI` 组件 `_enableCommunicationLog` 开启后，Console 输出 `← Host` / `→ Host` 通信记录。

---

## 9. 相关源码索引


| 文件                           | 内容                              |
| ---------------------------- | ------------------------------- |
| `WebGLAPI.cs`                | 全部 iframe 接口实现                  |
| `Communication.jslib`        | postMessage ↔ SendMessage 桥接    |
| `AndroidMessage.cs`          | JSON 结构体定义                      |
| `MapApi.cs`                  | `TransitionToControlState` 业务逻辑 |
| `vue-parent-standalone.html` | 可运行演示页                          |


---

## 10. 版本记录


| 日期         | 说明                                                                                     |
| ---------- | -------------------------------------------------------------------------------------- |
| 2026-08    | 新增 `StartThreatHighRiskPolling` / `StopThreatHighRiskPolling`；冷却结束先请求再评估 |
| 2026-08    | `status` 改为：0 默认 / 1 告警定位 / 2 威胁 |
| 2026-07-24 | 对齐 `WebGLAPI.cs`：新增 `ExitThreatDrill`、`RefreshThreatCooldown`、`SetDefaultProvinceCode` |
| 2026-07    | `ControlStateTransitionNotify` 增加字段 `status`（大屏播放状态）                                   |
| 2026-08    | `partId` 示例统一为场景实际值：`IDC`、`CCU`、`TBOX`、`ADC`、`WG`                                           |
| 2026-07    | 父页面 `source` 由 `parent-app` 改为 `webgl-unity-parent`                                    |
| 2026-07    | 统一 `partId`，移除 `partName`                                                              |
| 2026-07    | `onUnityControlStateTransition` 支持 `from=-1` 完成通知                                      |


