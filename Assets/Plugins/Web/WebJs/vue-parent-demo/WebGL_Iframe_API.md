# WebGL iframe 通信 API 接口文档

本文档为 **Vue / 任意前端父页面** 嵌入 Unity WebGL（iframe）时的 **接口调用规范**，包含完整示例、JSON 字段可空说明及行为说明。

> 架构与扩展指南见同目录 `[WebGL_Vue_Communication.md](./WebGL_Vue_Communication.md)`  
> 示例页面：`[vue-parent-standalone.html](./vue-parent-standalone.html)`

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

`partId` 为场景中已配置的零件组 ID，**当前项目仅支持以下三个值**（区分大小写）：

| partId | 说明 |
|--------|------|
| `Group01` | 零件组 1 |
| `Group02` | 零件组 2 |
| `Group03` | 零件组 3 |

文档示例 **仅使用上述取值**。传入其它值时 Unity 可能无法匹配零件或切换失败。

省略 `partId` 或传 `""` 时，使用过渡控制器默认配置或列表首项（通常为 `Group01`）。

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


| 字段                     | 类型     | 必填  | 可空/默认                          | 说明                       |
| ---------------------- | ------ | --- | ------------------------------ | ------------------------ |
| `targetState`          | int    | ✅   | —                              | 目标级别 `0~5`；非法值忽略请求       |
| `provinceName`         | string |     | 可省略 / `""` / 空白 → Unity 用默认省配置 | 省名，如 `"山东"`；省级↔车辆阶段使用    |
| `provinceModuleName`   | string |     | 可省略 / `""` / 空白 → 默认板块         | 场景中省级板块 GameObject 名     |
| `partId`               | string |     | 可省略 / `""` / 空白 → 控制器默认或列表首项   | 零件组 ID，有效值：`Group01`、`Group02`、`Group03` |
| `useInstantTransition` | bool   |     | 省略 → `false`                   | `true` 时跳过过渡动画（临时置 0 时长） |


**可空规则（Unity 侧）：**

- `provinceName` / `provinceModuleName` / `partId`：字段省略、空字符串、仅空白，均视为 `null`，走 Unity 默认逻辑。
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
  "provinceName": "山东",
  "provinceModuleName": "polySurface3",
  "partId": "",
  "useInstantTransition": false
}
```

```javascript
callUnity('TransitionToControlState', JSON.stringify({
  targetState: 2,
  provinceName: '山东',
  provinceModuleName: 'polySurface3',
  partId: '',
  useInstantTransition: false,
}));
```

**跳到省级（常用）：**

```json
{"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3"}
```

**跳到零件级：**

```json
{"targetState":4,"partId":"Group01"}
```

**已在零件级时切换零件（targetState 仍为 4）：**

```json
{"targetState":4,"partId":"Group02"}
```

**切换到第三组零件：**

```json
{"targetState":4,"partId":"Group03"}
```

**攻击路径级跳到零件：**

```json
{"targetState":4,"partId":"Group01"}
```

**瞬时跳转（无动画）：**

```json
{"targetState":1,"useInstantTransition":true}
```

**从国家直接跳到车辆（Unity 内部多步执行）：**

```json
{"targetState":3,"provinceName":"广东"}
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



### 4.5 接口汇总表（父 → Unity）

| method                             | arg  | JSON | 说明        |
| ---------------------------------- | ---- | ---- | --------- |
| `TransitionToControlState`         | JSON | ✅    | 跳转到指定操控级别 |
| `TransitionToNextControlState`     | `""` |      | 下一级       |
| `TransitionToPreviousControlState` | `""` |      | 上一级       |
| `SetBigScreenAutoCarouselEnabled`  | JSON | ✅    | 大屏轮播开关    |

> 已移除历史测试接口：`OnAndroidNotifyA/B`、`OnDataSyncResult`、`ShowMessage` 等不再由 `WebGLAPI` 暴露。

---



## 5. Unity → 父页面回调

父页面通过 `window.addEventListener('message', ...)` 接收，`data.source === 'unity-webgl'`。

```javascript
const handlers = {
  onUnityWebGLReady(message) { /* 桥接就绪 */ },
  onUnityControlStateTransition(message) { /* JSON，含 partId：Group01/Group02/Group03 */ },
};

window.addEventListener('message', (event) => {
  const data = event.data;
  if (!data || data.source !== 'unity-webgl' || typeof data.method !== 'string') return;
  handlers[data.method]?.(data.message ?? '');
});
```

> 当前 `WebGLAPI` **仅**向父页面推送 `onUnityWebGLReady` 与 `onUnityControlStateTransition`。`onUnityShowToast`、`onUnityUpdateNativeTitle`、`onUnityRequestDataSync` 等测试回调已移除。

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


| 字段       | 类型     | 必填  | 可空/默认       | 说明                                 |
| -------- | ------ | --- | ----------- | ---------------------------------- |
| `from`   | int    | ✅   | —           | 过渡**开始**：起始级别 `0~5`；**完成**：固定 `-1` |
| `to`     | int    | ✅   | —           | 目标级别 `0~5`                         |
| `partId` | string |     | 无零件场景为 `""` | 零件相关场景为 `Group01` / `Group02` / `Group03` |
| `status` | int    |     | 当前暂为 `0`  | 大屏跳转状态，见下表；**预留**，后续按触发源区分 |

#### status 取值（`BigScreenStatus`）

表示本次层级过渡由何种**大屏业务场景**触发（与操控级别 `from`/`to`、四个态势轮播类型无关）：

| 值 | 含义 | 典型场景 |
|----|------|----------|
| `0` | 普通跳转 | 默认状态（未区分触发源的常规跳转） |
| `1` | 信息跳转 | 宿主或用户主动查看信息触发的跳转 |
| `2` | 威胁下钻 | 威胁态势联动下钻 |

> **预留说明：** Unity 当前暂统一回传 `0`；宿主可解析字段但不必依赖，待业务接入后按实际上下文填充。

**过渡开始示例：**

```json
{"from":3,"to":4,"partId":"","status":0}
```

```javascript
function onUnityControlStateTransition(json) {
  const { from, to, partId, status } = JSON.parse(json);
  // status: 0 普通跳转 | 1 信息跳转 | 2 威胁下钻（预留）
  if (from === -1) {
    console.log('过渡完成，就绪级别', to, '零件', partId, '大屏状态', status);
    // 隐藏 Loading、刷新 UI
  } else {
    console.log('过渡开始', from, '→', to);
    // 显示 Loading
  }
}
```

**过渡完成示例：**

```json
{"from":-1,"to":4,"partId":"Group01","status":0}
```

**零件切换开始（4→4）：**

```json
{"from":4,"to":4,"partId":"Group02","status":0}
```

**零件切换完成（切到 Group03）：**

```json
{"from":-1,"to":4,"partId":"Group03","status":0}
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

### 5.3 回调汇总表（Unity → 父）

| method                          | message 类型 | 说明        |
| ------------------------------- | ---------- | --------- |
| `onUnityWebGLReady`             | 空          | 桥接就绪      |
| `onUnityControlStateTransition` | JSON       | 级别过渡开始/完成 |

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
          console.log('完成，级别', t.to, '零件', t.partId); // partId: Group01/Group02/Group03
        } else {
          console.log('开始', t.from, '→', t.to);
        }
        break;
      }
    }
  });

  function jumpToPart() {
    if (!ready) return alert('Unity 未就绪');
    callUnity('TransitionToControlState', '{"targetState":4,"partId":"Group01"}');
  }

  function jumpToShandong() {
    if (!ready) return alert('Unity 未就绪');
    callUnity('TransitionToControlState', JSON.stringify({
      targetState: 2,
      provinceName: '山东',
      provinceModuleName: 'polySurface3',
    }));
  }
</script>
```



### 6.2 典型业务流程 JSON 对照


| 业务意图     | 调用                                                                          |
| -------- | --------------------------------------------------------------------------- |
| 回到地球     | `{"targetState":0}`                                                         |
| 进入国家地图   | `{"targetState":1}`                                                         |
| 聚焦山东省    | `{"targetState":2,"provinceName":"山东","provinceModuleName":"polySurface3"}` |
| 进入车辆视图   | `{"targetState":3,"provinceName":"山东"}`                                     |
| 查看零件 Group01 | `{"targetState":4,"partId":"Group01"}`                                      |
| 切换零件 Group02 | `{"targetState":4,"partId":"Group02"}`                                      |
| 切换零件 Group03 | `{"targetState":4,"partId":"Group03"}`                                      |
| 查看攻击路径   | `{"targetState":5}`                                                         |
| 攻击路径下看零件 | `{"targetState":4,"partId":"Group01"}`                                      |
| 关闭大屏轮播   | `{"enabled":false}`                                                         |


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


| 日期      | 说明                                                  |
| ------- | --------------------------------------------------- |
| 2026-07 | `ControlStateTransitionNotify` 增加预留字段 `status`（大屏状态） |
| 2026-07 | `partId` 示例统一为 `Group01`、`Group02`、`Group03`          |
| 2026-07 | 父页面 `source` 由 `parent-app` 改为 `webgl-unity-parent` |
| 2026-07 | 统一 `partId`，移除 `partName`                           |
| 2026-07 | `onUnityControlStateTransition` 支持 `from=-1` 完成通知   |


