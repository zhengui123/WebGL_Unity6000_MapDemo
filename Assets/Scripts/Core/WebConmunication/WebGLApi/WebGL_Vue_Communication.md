# Unity WebGL ↔ Vue 父页面通信说明

本文档说明架构、调用链路与扩展方式。**API 接口字段、完整 JSON 示例见同目录** `[WebGL_Iframe_API.md](./WebGL_Iframe_API.md)`**。**

---

## 相关文件


| 文件                                                                    | 作用                               |
| --------------------------------------------------------------------- | -------------------------------- |
| `Assets/Plugins/Web/WebJs/vue-parent-demo/vue-parent-standalone.html` | 免 npm 的 Vue 3 父页面                |
| `Assets/Plugins/Web/WebJs/vue-parent-demo/useUnityIframeBridge.js`    | Vite 工程版 composable              |
| `Assets/Plugins/Web/WebJs/vue-parent-demo/README_API文档位置.md`          | 文档迁移跳转说明                         |
| `Communication.jslib`                                                 | iframe `postMessage` 桥接          |
| `WebGLAPI.cs`（本目录）                                                    | Unity WebGL 通信入口，物体名 `WebGLAPI`  |
| `WebGL_Iframe_API.md`（本目录）                                            | WebGL 接口字段与完整 JSON 示例            |
| `AndroidMessage.cs`                                                   | Android 通信入口，物体名 `AndroidBridge` |
| `AndroidMessage_API.md`                                               | Android 接口说明（与本目录 WebGL 文档方法名对齐） |


---



## 一、架构与消息协议

Unity WebGL 嵌入 `<iframe>`，跨域时**只使用** `postMessage`，不调用 `parent.xxx()`。

```
vue-parent-standalone.html
    │ postMessage { source:"webgl-unity-parent", method, arg }
    ▼
Unity iframe (index.html)
    │ Communication.jslib 监听 message
    ▼
unityInstance.SendMessage("WebGLAPI", method, arg)
    ▼
WebGLAPI.cs → MapApi

反向：WebGLAPI.CallHost → CallHTMLHandler → parent.postMessage({ source:"unity-webgl", method, message })
```



### 固定约定


| 项目                 | 值                                               |
| ------------------ | ----------------------------------------------- |
| Unity 物体名          | `WebGLAPI`                                      |
| 父 → Unity `source` | `webgl-unity-parent`                            |
| Unity → 父 `source` | `unity-webgl`                                   |
| jslib 全局变量         | `window.unityInstance`（Build 的 index.html 必须赋值） |
| 方法名                | **区分大小写**，与 C# `public` 方法名一致                   |
| 参数类型               | `SendMessage` 仅支持 `string`；结构体用 JSON 字符串        |


---



## 二、父页面调用 Unity（完整链路）

以跳到零件级为例：

### 1. Vue 封装

```javascript
// vue-parent-standalone.html → useUnityIframeBridge
transitionToControlState({ targetState: 4, partId: 'IDC' });
```



### 2. postMessage

```javascript
iframe.contentWindow.postMessage({
  source: 'webgl-unity-parent',
  method: 'TransitionToControlState',
  arg: '{"targetState":4,"partId":"IDC"}'
}, '*');
```



### 3. jslib 转发（Communication.jslib）

```javascript
// InitIframePostMessageBridge 内
instance.SendMessage('WebGLAPI', data.method, data.arg || '');
```

Unity 未就绪时消息进入 `__unityPendingParentMessages`；`NotifyHostReady()` 后 `FlushIframePendingMessages` 冲刷队列。

### 4. C# 入口

```csharp
public void TransitionToControlState(string json)
{
    var request = JsonUtility.FromJson<TransitionToControlStateRequest>(json);
    MapApi.Instance.TransitionToControlState(
        request.targetState, request.provinceCode, ...);
}
```



### 5. Unity 回调父页面

```javascript
// CallHTMLHandler → parent.postMessage
{ source: 'unity-webgl', method: 'onUnityControlStateTransition',
  message: '{"from":3,"to":4,"status":0,"provinceCode":"330000","vin":"","partId":""}' }
```



### 6. Vue 处理

```javascript
handlers: {
  onUnityControlStateTransition(json) {
    const { from, to, partId } = JSON.parse(json);
    // from === -1 表示过渡完成
  }
}
```

**就绪信号：** 收到 `onUnityWebGLReady` 后再调用业务接口。

---



## 三、操控级别


| 值   | 级别   |
| --- | ---- |
| 0   | 地球   |
| 1   | 国家   |
| 2   | 省级   |
| 3   | 车辆   |
| 4   | 零件   |
| 5   | 攻击路径 |


`onUnityControlStateTransition` 中 `from = -1` 表示过渡**完成**（`to` 为已就绪级别）。

**partId 有效值：** `IDC`、`CCU`、`TBOX`、`ADC`、`WG`（详见 `WebGL_Iframe_API.md` §3.1）。

---



## 四、方法对照表



### 4.1 父页面 → Unity


| postMessage `method`                      | `arg`              | WebGLAPI 方法                                       | MapApi                                    | standalone 已封装 |
| ----------------------------------------- | ------------------ | ------------------------------------------------- | ----------------------------------------- | -------------- |
| `TransitionToControlState`                | JSON               | `TransitionToControlState(string)`                | `TransitionToControlState`                | ✅              |
| `TransitionToNextControlState`            | `""`               | `TransitionToNextControlState()`                  | `TransitionToNextControlState`            | ✅              |
| `TransitionToPreviousControlState`        | `""`               | `TransitionToPreviousControlState()`              | `TransitionToPreviousControlState`        | ✅              |
| `SetBigScreenAutoCarouselEnabled`         | `{"enabled":bool}` | `SetBigScreenAutoCarouselEnabled(string)`         | `SetBigScreenAutoCarouselEnabled`         | ✅              |
| `PauseGame`                               | `""`               | `PauseGame()`                                     | `PauseGame`                               | ✅              |
| `ResumeGame`                              | `""`               | `ResumeGame()`                                    | `ResumeGame`                              | ✅              |
| `ExitThreatDrill`                         | `""`               | `ExitThreatDrill()`                               | `ExitThreatDrill`                         | 按需             |
| `RefreshThreatCooldown`                   | `""`               | `RefreshThreatCooldown()`                         | `RefreshThreatCooldown`                   | 按需             |
| `StartThreatHighRiskPolling`              | `""`               | `StartThreatHighRiskPolling()`                    | `StartThreatHighRiskPolling`              | 按需             |
| `StopThreatHighRiskPolling`               | `""`               | `StopThreatHighRiskPolling()`                     | `StopThreatHighRiskPolling`               | 按需             |
| `SetWorldMapRegionDefaults`               | JSON               | `SetWorldMapRegionDefaults(string)`               | `SetWorldMapRegionDefaults`               | 按需             |
| `CloseCarUI`                              | `""`               | `CloseCarUI()`                                    | `CloseCarVehicleDataUi`                   | ✅              |
| `CloseGJPanel`                            | `""`               | `CloseGJPanel()`                                  | `CloseGJPanel()`                          | ✅              |
| `StartVehicleHeatmapSpecifiedTimePolling` | JSON               | `StartVehicleHeatmapSpecifiedTimePolling(string)` | `StartVehicleHeatmapSpecifiedTimePolling` | ✅              |
| `StopVehicleHeatmapSpecifiedTimePolling`  | `""`               | `StopVehicleHeatmapSpecifiedTimePolling()`        | `StopVehicleHeatmapSpecifiedTimePolling`  | ✅              |
| `RequestVehicleHeatmapOnce`               | JSON / `""`        | `RequestVehicleHeatmapOnce(string)`               | `RequestVehicleHeatmapOnce`               | ✅              |
| `RequestCarVehicleData`                   | `""` 或 JSON        | `RequestCarVehicleData(string)`                   | `RequestCarVehicleData`                   | ✅              |
| `RequestSecurityEventDetail`              | `""` 或 JSON        | `RequestSecurityEventDetail(string)`              | `RequestSecurityEventDetail`              | ✅              |
| `SetHttpRequestHeaders`                   | JSON               | `SetHttpRequestHeaders(string)`                   | `SetHttpRequestHeaders`                   | apiHost/appSecret/头 |




#### TransitionToControlState 请求 JSON

定义：`TransitionToControlStateRequest`（`AndroidMessage.cs`）


| 字段                     | 类型     | 必填  | 说明                                          |
| ---------------------- | ------ | --- | ------------------------------------------- |
| `targetState`          | int    | ✅   | 0~5                                         |
| `provinceCode`         | string |     | 省/国家 code（国内 adcode / 国外 SOC）               |
| `partId`               | string |     | 零件 ID：`IDC` / `CCU` / `TBOX` / `ADC` / `WG` |
| `useInstantTransition` | bool   |     | 跳过动画，默认 false                               |


```javascript
callUnity('TransitionToControlState', JSON.stringify({
  targetState: 2,
  provinceCode: '370000'
}));
```



#### Android 有、WebGL 未暴露（请用 TransitionToControlState 替代）


| Android 方法              | 说明     |
| ----------------------- | ------ |
| `TransitionToPlateMap`  | 地球→国家  |
| `TransitionToEarth`     | 国家→地球  |
| `FocusPlateMapModule`   | 聚焦省级板块 |
| `RestorePlateMapCamera` | 还原省级相机 |


---



### 4.2 Unity → 父页面


| postMessage `method`            | `message` | 触发                | standalone handler   |
| ------------------------------- | --------- | ----------------- | -------------------- |
| `onUnityWebGLReady`             | `""`      | `NotifyHostReady` | 内置 `unityReady=true` |
| `onUnityControlStateTransition` | JSON      | EventManager 过渡事件 | ✅                    |


> Unity → 父页面当前仅推送 `onUnityWebGLReady` 与 `onUnityControlStateTransition`。



#### onUnityControlStateTransition JSON

定义：`ControlStateTransitionNotify`


| 字段             | 说明                                                     |
| -------------- | ------------------------------------------------------ |
| `from`         | 0~5 过渡开始；-1 过渡完成                                       |
| `to`           | 目标级别 0~5                                               |
| `status`       | 当前大屏业务播放状态：`0` 默认、`1` 告警定位、`2` 威胁 |
| `provinceCode` | 当前区域 code（国内 adcode / 国外 SOC）                          |
| `vin`          | 当前车辆 VIN；无上下文时为 `""`                                   |
| `partId`       | 零件 ID：`IDC` / `CCU` / `TBOX` / `ADC` / `WG`，无零件时为 `""` |


示例：

```json
{"from":3,"to":4,"status":0,"provinceCode":"330000","vin":"","partId":""}
{"from":-1,"to":4,"status":0,"provinceCode":"330000","vin":"","partId":"IDC"}
```



#### 过渡场景（from → to）


| 跳转  | 说明        |
| --- | --------- |
| 0↔1 | 地球 ↔ 国家   |
| 1↔2 | 国家 ↔ 省级   |
| 2↔3 | 省级 ↔ 车辆   |
| 3↔4 | 车辆 ↔ 零件   |
| 4→4 | 零件切换      |
| 3↔5 | 车辆 ↔ 攻击路径 |
| 5→4 | 攻击路径 → 零件 |


每级过渡：**开始**回调一次（from 0~5），**完成**再回调一次（from=-1）。

---



## 五、Unity Build 配置

`index.html` 中 `createUnityInstance` 成功后：

```javascript
createUnityInstance(canvas, config, onProgress).then((instance) => {
  unityInstance = instance;
  window.unityInstance = instance;  // 必须，jslib 依赖
});
```

场景挂载 `WebGLAPI`，运行时物体名 `WebGLAPI`。

**访问方式：** 建议 HTTP，不要用 `file://`（wasm 常被拦截）。

```powershell
cd F:\UnityProjects\U6_MapDemo\Assets\Plugins\Web\WebJs\vue-parent-demo
python -m http.server 8080
# http://localhost:8080/vue-parent-standalone.html
```

---



## 六、日志与联调


| 端     | 位置                                                     | 格式示例                                                   |
| ----- | ------------------------------------------------------ | ------------------------------------------------------ |
| Unity | Console / WebGLAPI Inspector `_enableCommunicationLog` | `[WebGLAPI] ← Host | TransitionToControlState | {...}` |
| 父页面   | 控制台 + 页面底部日志面板                                         | `[UnityBridge] → Unity | ...`                          |


**联调清单：**

1. iframe 能加载 Unity Build
2. `window.unityInstance` 已设置
3. 收到 `onUnityWebGLReady` 后再发指令
4. `method` 与 `WebGLAPI` public 方法名完全一致
5. JSON 字段名区分大小写（`JsonUtility` 限制）

---



## 七、新增接口指南



### 7.1 父页面 → Unity（新增调用）

**规则：** `postMessage.method` = `WebGLAPI` 的 `public` 方法名。jslib **无需改**（已通用转发）。

**Unity（WebGLAPI.cs）：**

```csharp
public void MyNewCommand(string json)
{
    LogCommunication("← Host", nameof(MyNewCommand), json);
    if (string.IsNullOrWhiteSpace(json)) return;

    var req = JsonUtility.FromJson<MyNewCommandRequest>(json);
    // MapApi.Instance.XXX(req);

    LogCommunication("← Host", nameof(MyNewCommand), "已接受并启动");
}
```

无参方法：`public void MyAction()`，父页面 `arg: ""`。

**Vue：**

```javascript
myNewCommand: (p) => callUnity('MyNewCommand',
  typeof p === 'string' ? p : JSON.stringify(p)),
```

**Android 对齐（可选）：** 在 `AndroidMessage.cs` 增加同名方法。

---



### 7.2 Unity → 父页面（新增回调）

**规则：** `CallHost("onUnityXxx", msg)` ↔ `handlers.onUnityXxx`。

**Unity：**

```csharp
public void CallAndroidMyEvent(string message)
{
    CallHost("onUnityMyEvent", message ?? string.Empty);
}
```

**Vue：**

```javascript
handlers: {
  onUnityMyEvent(message) { /* 处理 */ }
}
```

---



### 7.3 JSON 数据类

```csharp
[System.Serializable]
public struct MyNewCommandRequest
{
    public string foo;
    public int count;
}
```

`JsonUtility`：字段须 `public`，名与 JSON key 一致。

---



## 八、Android vs WebGL 速查


| 项目        | Android                               | WebGL + Vue               |
| --------- | ------------------------------------- | ------------------------- |
| 脚本        | `AndroidMessage`                      | `WebGLAPI`                |
| 物体名       | `AndroidBridge`                       | `WebGLAPI`                |
| 宿主→Unity  | `UnitySendMessage`                    | `postMessage` + jslib     |
| Unity→宿主  | `activity.Call`                       | `parent.postMessage`      |
| 就绪通知      | 无（原生侧自行管理生命周期）                        | `onUnityWebGLReady`       |
| 车辆 Yaw 回调 | `onUnityCarYawRotationChanged`        | 同名                        |
| 接口说明      | `AndroidBridge/AndroidMessage_API.md` | 本目录 `WebGL_Iframe_API.md` |


业务方法名两端对齐（含 `SetCarYawRotation`、地图过渡联调方法）。JSON 结构共用。

---



## 九、最小调用示例

```javascript
// 父 → Unity
function callUnity(method, arg = '') {
  document.querySelector('iframe').contentWindow.postMessage(
    { source: 'webgl-unity-parent', method, arg }, '*'
  );
}

callUnity('TransitionToControlState', '{"targetState":3}');
callUnity('SetBigScreenAutoCarouselEnabled', '{"enabled":true}');
callUnity('PauseGame', '');
callUnity('ResumeGame', '');
callUnity('ExitThreatDrill', '');
callUnity('RefreshThreatCooldown', '');
callUnity('StartThreatHighRiskPolling', '');
callUnity('StopThreatHighRiskPolling', '');
callUnity('SetWorldMapRegionDefaults', JSON.stringify({
  provinceCode: '330000'
}));
callUnity('RequestCarVehicleData', '');
callUnity('RequestCarVehicleData', JSON.stringify({
  encryptVin: 'ed49f47afa23e45b18d342767495643c',
  startTime: '',
  endTime: '2026-06-30 23:00:00'
}));
callUnity('RequestSecurityEventDetail', '');
callUnity('RequestSecurityEventDetail', JSON.stringify({
  eventId: '123dfdsafffff',
  processStartTime: '2026-06-30 17:41:23',
  processEndTime: '2026-06-30 17:41:23',
  tenantId: 1
}));
callUnity('CloseCarUI', '');
callUnity('CloseGJPanel', '');
callUnity('StartVehicleHeatmapSpecifiedTimePolling', JSON.stringify({
  startTime: '2026-06-30 00:00:00',
  endTime: '2026-06-30 23:00:00'
}));
callUnity('StopVehicleHeatmapSpecifiedTimePolling', '');
callUnity('RequestVehicleHeatmapOnce', JSON.stringify({
  startTime: '2026-06-30 00:00:00',
  endTime: '2026-06-30 23:00:00',
  isReplay: true
}));
callUnity('SetCarYawRotation', JSON.stringify({ yawAngle: 90.0, instant: false }));
callUnity('TransitionToPlateMap', '');
callUnity('FocusPlateMapModule', 'polySurface3');
callUnity('RestorePlateMapCamera', '');
callUnity('TransitionToEarth', '');

// Unity → 父
window.addEventListener('message', (e) => {
  const d = e.data;
  if (d?.source !== 'unity-webgl') return;
  if (d.method === 'onUnityWebGLReady') console.log('就绪');
  if (d.method === 'onUnityControlStateTransition') {
    const t = JSON.parse(d.message);
    console.log(t.from === -1 ? '完成' : '开始', t);
  }
  if (d.method === 'onUnityCarYawRotationChanged') {
    console.log('车辆 Yaw', JSON.parse(d.message));
  }
});
```

