# Unity 操控级别桥接 API（Android 侧）

本文档供 **Android 原生开发**接入 Unity 地图 Demo 的操控级别跳转能力。

---

## 约定

| 项目 | 值 |
|------|-----|
| Unity 接收物体名 | `AndroidBridge` |
| Android → Unity | `UnityPlayer.UnitySendMessage(...)` |
| Unity → Android | `MainActivity` 中实现对应 `public` 方法，由 Unity 通过 `activity.Call(...)` 调用 |

---

## 操控级别

| 值 | 级别 |
|----|------|
| 0 | 地球级 |
| 1 | 国家级 |
| 2 | 省级 |
| 3 | 车辆级 |
| 4 | 零件级 |
| 5 | 攻击路径级 |

---

## 一、Android 调用 Unity：发起层级跳转

### 调用方式

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

### 请求 JSON 字段

字段名区分大小写，须与下表一致。

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `targetState` | int | 是 | 目标级别 0~5 |
| `provinceName` | string | 否 | 省名，如「山东」；省略或 `""` 表示使用 Unity 默认 |
| `provinceModuleName` | string | 否 | 省级 3D 板块对象名 |
| `partId` | string | 否 | 业务零部件 ID，用于进入零件级、零件切换、攻击路径 → 零件 |
| `useInstantTransition` | bool | 否 | 是否跳过过渡动画；省略为 `false` |

### 调用示例

**跳到省级，并指定省名与板块模块：**

```java
String json = "{"
    + "\"targetState\":2,"
    + "\"provinceName\":\"山东\","
    + "\"provinceModuleName\":\"polySurface3\","
    + "\"useInstantTransition\":false"
    + "}";
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

**仅指定目标为车辆级（其余参数走默认）：**

```java
String json = "{\"targetState\":3}";
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

**瞬时跳到零件级：**

```java
String json = "{"
    + "\"targetState\":4,"
    + "\"partId\":\"PART-1575\","
    + "\"useInstantTransition\":true"
    + "}";
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

### 说明

- 本接口为**异步发起**，Unity 不通过回调告知成功或失败。
- `targetState` 必须为 0~5；非法值时 Unity 侧会忽略请求。
- 可选字符串字段可不传；传空字符串与省略等价。

---

## 二、Unity 回调 Android / WebGL：级别跳转通知

### 需在 MainActivity / 宿主页面实现的方法

```java
public void onUnityControlStateTransition(String json) { }
```

```javascript
function onUnityControlStateTransition(json) { }
```

同一回调承载两类通知，通过 `from` 区分：

| `from` | 含义 |
|--------|------|
| `0~5` | 过渡**开始**（动画尚未结束） |
| `-1` | 过渡**完成**（目标级别已加载就绪） |

### 回调 JSON 字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `from` | int | 起始级别 `0~5`；完成通知时为 `-1` |
| `to` | int | 目标级别 `0~5` |
| `partId` | string | 业务零部件 ID；零件进入/切换/攻击路径→零件完成时可带值，其它场景为空字符串 |
| `status` | int | **（预留）** 大屏跳转状态：`0` 普通跳转、`1` 信息跳转、`2` 威胁下钻；当前 Unity 暂统一回传 `0` |

**过渡开始示例：**

```json
{"from":1,"to":2,"partId":"","status":0}
```

表示从**国家级**进入**省级**的过渡已开始。

**过渡完成示例：**

```json
{"from":-1,"to":4,"partId":"Group01","status":0}
```

表示**零件级**过渡已完成并可交互。

### 接收示例

```java
public void onUnityControlStateTransition(String json) {
    try {
        JSONObject obj = new JSONObject(json);
        int from = obj.getInt("from");
        int to = obj.getInt("to");
        String partId = obj.optString("partId", "");
        int status = obj.optInt("status", 0); // 预留：大屏状态
        if (from == -1) {
            Log.d("UnityBridge", "transition completed, level=" + to + ", partId=" + partId);
            // 根据 to / partId 隐藏 Loading、刷新原生界面
        } else {
            Log.d("UnityBridge", "transition start: " + from + " -> " + to);
            // 根据 from / to 展示 Loading
        }
    } catch (JSONException e) {
        Log.e("UnityBridge", "invalid transition json: " + json, e);
    }
}
```


---



## 三、会触发 `onUnityControlStateTransition` 的场景

### 过渡开始（`from` 为 0~5）

以下为 Unity 侧可能发起的跳转；在对应过渡**开始时**回调一次。

| from → to | 场景说明 |
|-----------|----------|
| 0 → 1 | 地球 → 国家（进入板块地图） |
| 1 → 0 | 国家 → 地球（返回地球视图） |
| 1 → 2 | 国家 → 省级（开始聚焦某省模块） |
| 2 → 1 | 省级 → 国家（相机还原到国家视图） |
| 2 → 3 | 省级 → 车辆（进入车辆视图） |
| 3 → 2 | 车辆 → 省级（返回省级视图） |
| 3 → 4 | 车辆 → 零件 |
| 4 → 3 | 零件 → 车辆 |
| 4 → 4 | 零件 → 零件切换 |
| 3 → 5 | 车辆 → 攻击路径 |
| 5 → 3 | 攻击路径 → 车辆 |
| 5 → 4 | 攻击路径 → 零件 |

### 过渡完成（`from` 为 -1）

在对应过渡动画结束、目标级别就绪后回调一次；`to` 为已就绪级别。

| to | 场景说明 | partId |
|----|----------|--------|
| 0 | 地球级就绪 | 空 |
| 1 | 国家级就绪 | 空 |
| 2 | 省级就绪 | 空 |
| 3 | 车辆级就绪 | 空 |
| 4 | 零件级就绪 | 进入/切换/攻击路径→零件时可有值 |
| 5 | 攻击路径级就绪 | 空 |

> 跨多级跳转（如国家 → 零件）时，每一级过渡完成都会分别回调一次 `from=-1`。



---



## 四、MainActivity 接口汇总



### Android 需实现（Unity → Android）



| 方法 | 参数 | 说明 |

|------|------|------|

| `onUnityControlStateTransition` | JSON `{"from":n,"to":m}` | 操控级别过渡开始 |



### Android 可调用（Android → Unity）



| UnitySendMessage 方法名 | 第 3 参数 | 说明 |

|-------------------------|-----------|------|

| `TransitionToControlState` | JSON，见第一节 | 请求跳转到指定操控级别 |



---

## 五、WebGL 跨域 iframe 嵌入（Vue 父页面）

Unity 打包产物作为 `<iframe src=".../index.html">` 嵌入前端时，**仅使用 `postMessage`**，不直接调用 `parent` 上的函数。

### 消息协议

| 方向 | `source` | 字段 | 说明 |
|------|----------|------|------|
| Unity → 父页面 | `unity-webgl` | `method`, `message` | 与 Android 回调方法名一致，如 `onUnityControlStateTransition` |
| 父页面 → Unity | `webgl-unity-parent` | `method`, `arg` | `method` 对应 `WebGLAPI` 公共方法，如 `TransitionToControlState` |

就绪通知：Unity 启动后发送 `{ source:"unity-webgl", method:"onUnityWebGLReady", message:"" }`。

### iframe 内 index.html（Build 后合并）

```javascript
var unityInstance = null;
createUnityInstance(canvas, config, onProgress).then((instance) => {
  unityInstance = instance;
  window.unityInstance = instance; // jslib 依赖此全局变量
});
```

参考：`Assets/Plugins/Web/WebJs/WebGLEmbedIframe.sample.html`

### Vue 父页面示例

完整 API 见：**`Assets/Plugins/Web/WebJs/vue-parent-demo/WebGL_Iframe_API.md`**

免 npm 单文件：`vue-parent-demo/vue-parent-standalone.html`

```javascript
// composables/useUnityIframeBridge.js — 见 Assets/Plugins/Web/WebJs/vue-parent-embed.sample.js
iframeRef.value.contentWindow.postMessage({
  source: 'webgl-unity-parent',
  method: 'TransitionToControlState',
  arg: JSON.stringify({ targetState: 4, partId: 'Group01' })
}, '*');

window.addEventListener('message', (event) => {
  const data = event.data;
  if (!data || data.source !== 'unity-webgl') return;
  if (data.method === 'onUnityControlStateTransition') {
    const { from, to, partId } = JSON.parse(data.message);
    // from=-1 表示过渡完成；0~5 表示过渡开始
  }
});
```

### WebGL 与 Android API 对齐

| 父页面 → Unity `method` | `arg` | 说明 |
|-------------------------|-------|------|
| `TransitionToControlState` | JSON | 同 Android `TransitionToControlState` |
| `TransitionToNextControlState` | `""` | 下一级 |
| `TransitionToPreviousControlState` | `""` | 上一级 |
| `SetBigScreenAutoCarouselEnabled` | `{"enabled":true}` | 大屏轮播 |

| Unity → 父页面 `method` | `message` | 说明 |
|-------------------------|-----------|------|
| `onUnityWebGLReady` | `""` | 桥接就绪 |
| `onUnityControlStateTransition` | JSON | 同 Android；`from=-1` 为完成通知 |

---

## 六、联调建议



1. 确认 `UnityPlayer.UnitySendMessage` 的第一个参数固定为 **`AndroidBridge`**。

2. JSON 使用 UTF-8，中文省名无需额外转义（标准 JSON 字符串即可）。

3. 收到 `onUnityControlStateTransition` 后按 `to` 切换界面；需要主动跳转时调用 `TransitionToControlState`。

4. 若长时间无回调，检查 MainActivity 方法是否为 `public`、方法名是否与上表完全一致（区分大小写）。


