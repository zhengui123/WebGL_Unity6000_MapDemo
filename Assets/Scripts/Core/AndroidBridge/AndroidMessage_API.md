# Unity ↔ Android 桥接 API

本文档描述 **Unity 中央可视化** 与 **Android 原生宿主** 之间的双向通信接口。

实现脚本：`Assets/Scripts/Core/AndroidBridge/AndroidMessage.cs`  
Unity 场景中需存在名为 **`AndroidBridge`** 的 GameObject，并挂载 `AndroidMessage` 组件。

> WebGL 同源业务接口见：`Assets/Scripts/Core/WebConmunication/Web/WebGLApi/WebGL_Iframe_API.md`（方法名与本表对齐，通信通道不同）。

---

## 一、通信约定

| 项目 | 说明 |
|------|------|
| Unity 接收物体名 | `AndroidBridge`（固定） |
| Android → Unity | `UnityPlayer.UnitySendMessage("AndroidBridge", 方法名, 参数)` |
| Unity → Android | `MainActivity` 中实现 `public` 回调方法，Unity 通过 `activity.Call(方法名, json)` 调用 |
| 数据格式 | JSON 字符串，字段名**区分大小写**，编码 UTF-8 |
| 调用特性 | Android → Unity 多为**异步发起**；结果通过 Unity → Android 回调感知 |

### 操控级别（`targetState` / `from` / `to`）

| 值 | 级别 |
|----|------|
| 0 | 地球级 |
| 1 | 国家级 |
| 2 | 省级 |
| 3 | 车辆级 |
| 4 | 零件级 |
| 5 | 攻击路径级 |

---

## 二、Android → Unity（`UnitySendMessage`）

### 2.1 `TransitionToControlState` — 跳转到指定操控级别

**调用：**

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

**请求 JSON 字段：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `targetState` | int | 是 | 目标级别 0~5 |
| `provinceCode` | string | 否 | 省/国家 code（国内 adcode / 国外 SOC）；省略或 `""` 使用 Unity 默认单元 |
| `partId` | string | 否 | 业务零部件 ID；进入零件级、零件切换、攻击路径→零件时使用 |
| `useInstantTransition` | bool | 否 | 是否跳过过渡动画；省略为 `false` |

**示例 — 跳到省级：**

```java
String json = "{"
    + "\"targetState\":2,"
    + "\"provinceCode\":\"370000\","
    + "\"useInstantTransition\":false"
    + "}";
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

**示例 — 仅跳到车辆级：**

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState",
    "{\"targetState\":3}");
```

**示例 — 瞬时跳到零件级：**

```java
String json = "{"
    + "\"targetState\":4,"
    + "\"partId\":\"IDC\","
    + "\"useInstantTransition\":true"
    + "}";
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

**说明：**

- `targetState` 非法（非 0~5）时 Unity 忽略请求，Console 输出警告。
- 可选字符串字段可不传；传空字符串与省略等价。
- 跳转是否成功无同步返回值；请结合 `onUnityControlStateTransition` 回调判断。

---

### 2.2 `TransitionToNextControlState` — 进入下一操控级别

等同 Unity 内双击操作。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToNextControlState", "");
```

---

### 2.3 `TransitionToPreviousControlState` — 返回上一操控级别

等同 Unity 内系统返回键。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToPreviousControlState", "");
```

---

### 2.4 `SetBigScreenAutoCarouselEnabled` — 大屏自动轮播开关

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "SetBigScreenAutoCarouselEnabled",
    "{\"enabled\":true}");
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `enabled` | bool | 是 | `true` 开启四屏自动轮播，`false` 关闭 |

---

### 2.5 `PauseGame` / `ResumeGame` — 暂停 / 恢复游戏

暂停或恢复 `Time.timeScale`，并暂停/播放全部 DOTween。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "PauseGame", "");
UnityPlayer.UnitySendMessage("AndroidBridge", "ResumeGame", "");
```

对应 Unity：`MapApi.PauseGame` / `MapApi.ResumeGame` → `GameManager`。

---

### 2.6 `ExitThreatDrill` — 主动退出威胁下钻

保持**当前操控级别**，退出威胁下钻流程并进入冷却（默认约 180s，冷却期间不再检测威胁）。自然跑完威胁流程不会进入冷却。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "ExitThreatDrill", "");
```

对应 Unity：`MapApi.ExitThreatDrill` → `ThreatProvinceAlertController.ExitThreatDrill`。

---

### 2.7 `RefreshThreatCooldown` — 刷新威胁冷却

仅在**威胁冷却中**有效：重新计满配置的冷却秒数。未在冷却中时调用会失败（Unity Console 警告）。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "RefreshThreatCooldown", "");
```

对应 Unity：`MapApi.RefreshThreatCooldown`。

---

### 2.8 `SetWorldMapRegionDefaults` — 设置国内外默认并立刻切换

设置国内/国外、国外大板块、默认单元 code，并立刻调用 `WorldMapRegionController` 切换（同 Inspector 面板按钮）。  
取代原 `SetDefaultProvinceCode`。

```java
// 国内：默认省浙江
UnityPlayer.UnitySendMessage("AndroidBridge", "SetWorldMapRegionDefaults",
    "{\"regionMode\":0,\"foreignPlateCode\":\"\",\"defaultUnitCode\":\"330000\"}");

// 国外：东亚 + 默认国家日本 392
UnityPlayer.UnitySendMessage("AndroidBridge", "SetWorldMapRegionDefaults",
    "{\"regionMode\":1,\"foreignPlateCode\":\"EAST_ASIA\",\"defaultUnitCode\":\"392\"}");
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `regionMode` | int | 是 | `0`=国内，`1`=国外 |
| `foreignPlateCode` | string | 国外是 | 大板块 firstClassCode，如 `EAST_ASIA`；国内可空 |
| `defaultUnitCode` | string | 否 | 国内=省级 adcode；国外=国家 SOC。空：国内保留现有默认省，国外用绑定 `defaultCountryCode` |

对应 Unity：`MapApi.SetWorldMapRegionDefaults` → `WorldMapRegionController.ApplyRegionDefaults`。

---

### 2.9 `CloseCarUI` — 关闭车辆 UI

停止零部件轮播并关闭车辆 UI / 连线面板。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "CloseCarUI", "");
```

对应 Unity：`MapApi.CloseCarVehicleDataUi`。

---

### 2.10 `CloseGJPanel` — 关闭告警面板 GJ_Panel

关闭场景中的 `GJ_Panel`（告警事件展示面板）。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "CloseGJPanel", "");
```

对应 Unity：`MapApi.CloseGJPanel` → `GJPanel.HidePanel()`。

---

### 2.11 `RequestVehicleHeatmapOnce` — 主动请求一次热力图（不轮询）

按起止时间与 `isReplay` **仅请求一次**后端并执行现有点位处理；不启停、不改轮询模式。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "RequestVehicleHeatmapOnce",
    "{\"startTime\":\"2026-06-30 00:00:00\",\"endTime\":\"2026-06-30 23:00:00\",\"isReplay\":true}");

// 起止可空：start 空、end 当前时间；也可传 ""（isReplay=false）
UnityPlayer.UnitySendMessage("AndroidBridge", "RequestVehicleHeatmapOnce", "");
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `startTime` | string | 否 | 查询开始时间；空则不传 start |
| `endTime` | string | 否 | 查询结束时间；空则用当前时间 |
| `isReplay` | bool | 否 | 是否使用历史数据，对应后端 `isReplay`；默认 false |

对应 Unity：`MapApi.RequestVehicleHeatmapOnce` → `VehicleHeatmapApiController.RequestOnceWithParams`。

---

### 2.12 `StartVehicleHeatmapSpecifiedTimePolling` — 开启热力图指定时段轮询

固定起止时间轮询车辆热力图，请求参数 `isReplay=true`。轮询间隔与默认模式相同。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "StartVehicleHeatmapSpecifiedTimePolling",
    "{\"startTime\":\"2026-06-30 00:00:00\",\"endTime\":\"2026-06-30 23:00:00\"}");
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `startTime` | string | 是 | 查询开始时间 |
| `endTime` | string | 是 | 查询结束时间 |

对应 Unity：`MapApi.StartVehicleHeatmapSpecifiedTimePolling` → `VehicleHeatmapApiController.StartSpecifiedTimePolling`。

---

### 2.13 `StopVehicleHeatmapSpecifiedTimePolling` — 关闭指定时段，恢复默认轮询

关闭指定时段模式：`isReplay=false`，`startTime` 空，`endTime` 为每次请求时的当前时间。

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "StopVehicleHeatmapSpecifiedTimePolling", "");
```

对应 Unity：`MapApi.StopVehicleHeatmapSpecifiedTimePolling`。

---

### 2.14 `RequestCarVehicleData` — 请求车辆态势数据

同参并发请求「零部件防护状态」与「攻击链路」；均成功后覆盖缓存。若当前已是车辆级，会打开车辆 UI 并开始零部件轮播。无结果回调（只发不回）。

```java
// 使用默认参数
UnityPlayer.UnitySendMessage("AndroidBridge", "RequestCarVehicleData", "");

// 指定参数
UnityPlayer.UnitySendMessage("AndroidBridge", "RequestCarVehicleData",
    "{\"encryptVin\":\"ed49f47afa23e45b18d342767495643c\",\"startTime\":\"\",\"endTime\":\"2026-06-30 23:00:00\"}");
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `encryptVin` | string | 否 | 加密 VIN；空则用 Unity 默认 |
| `startTime` | string | 否 | 查询开始时间；可空串 |
| `endTime` | string | 否 | 查询结束时间；空则用 Unity 默认 |

对应 Unity：`MapApi.RequestCarVehicleData` → `CarVehicleDataController.Request`。

---

### 2.15 `RequestSecurityEventDetail` — 请求事件溯源详情

请求 `getSourceEventDetail`；成功后缓存数据、刷新 `GJ_Panel`，并按经纬度生成 POI。无结果回调（只发不回）。

```java
// 使用默认参数
UnityPlayer.UnitySendMessage("AndroidBridge", "RequestSecurityEventDetail", "");

// 指定参数
UnityPlayer.UnitySendMessage("AndroidBridge", "RequestSecurityEventDetail",
    "{\"eventId\":\"123dfdsafffff\",\"processStartTime\":\"2026-06-30 17:41:23\",\"processEndTime\":\"2026-06-30 17:41:23\",\"tenantId\":1}");
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `eventId` | string | 否 | 事件 ID；空则用 Unity 默认 |
| `processStartTime` | string | 否 | 处理开始时间 |
| `processEndTime` | string | 否 | 处理结束时间 |
| `tenantId` | int | 否 | 租户 ID；默认 1 |

对应 Unity：`MapApi.RequestSecurityEventDetail` → `SecurityEventDetailApi.Request`。

---

### 2.16 `SetCarYawRotation` — 设置车辆 Y 轴旋转角度

> **特殊说明（重要）**  
> **Android / WebGL 正式业务中通常无需调用本接口。**  
> 车辆旋转由 Unity 大屏侧用户拖拽（`MouseDragYawRotate`）驱动；宿主只需实现并监听 **`onUnityCarYawRotationChanged`**（见 3.2 节），用于同步展示当前朝向。  
> 本接口仅供 Unity Editor / Demo 联调、自动化测试等场景使用，**不作为生产集成必选项**。  
> WebGL 同名方法见 `WebGLApi/WebGL_Iframe_API.md` §4.15。

用于控制车辆 3D 模型绕 Y 轴旋转（对应 `MouseDragYawRotate`）。

```java
// 以下仅为联调示例，Android 生产代码请勿调用
UnityPlayer.UnitySendMessage("AndroidBridge", "SetCarYawRotation",
    "{\"yawAngle\":90.0}");
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `yawAngle` | float | 是 | 目标 Yaw 角度，0~360 |
| `instant` | bool | 否 | 是否立即到位；省略为 `false`（平滑旋转，与拖拽相同 Slerp 效果） |

**示例 — 平滑旋转到 180°：**

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "SetCarYawRotation",
    "{\"yawAngle\":180.0,\"instant\":false}");
```

**示例 — 立即到位：**

```java
UnityPlayer.UnitySendMessage("AndroidBridge", "SetCarYawRotation",
    "{\"yawAngle\":90.0,\"instant\":true}");
```

---

### 2.17 其它地图过渡（可选 / 联调）

以下接口仍暴露，一般优先使用 `TransitionToControlState` 统一跳转：

| 方法名 | 第 3 参数 | 说明 |
|--------|-----------|------|
| `TransitionToPlateMap` | `""` | 地球 → 板块过渡 |
| `TransitionToEarth` | `""` | 板块 → 地球过渡 |
| `FocusPlateMapModule` | 模块名字符串 | 聚焦指定板块模块 |
| `RestorePlateMapCamera` | `""` | 还原板块相机 |

---

## 三、Unity → Android（MainActivity 回调）

在 `MainActivity` 中实现下列 **`public`** 方法（方法名区分大小写，须完全一致）：

### 3.1 `onUnityControlStateTransition` — 操控级别过渡通知

```java
public void onUnityControlStateTransition(String json) { }
```

同一回调承载**过渡开始**与**过渡完成**两类通知，通过 `from` 区分：

| `from` | 含义 |
|--------|------|
| `0~5` | 过渡**开始**（动画尚未结束） |
| `-1` | 过渡**完成**（目标级别已就绪） |

**回调 JSON 字段：**

| 字段 | 类型 | 说明 |
|------|------|------|
| `from` | int | 起始级别 `0~5`；完成通知时为 `-1` |
| `to` | int | 目标级别 `0~5` |
| `status` | int | **（预留）** 大屏跳转状态：`0` 普通、`1` 信息跳转、`2` 威胁下钻；当前 Unity 暂统一回传 `0` |
| `provinceCode` | string | 当前区域 code；国内为省 adcode，国外大屏为国家/区域 code。优先取当前聚焦板块 / 进省缓存，无则回落默认单元；取不到时为空字符串 |
| `vin` | string | 当前车辆 VIN；当前无车辆上下文时为空字符串 |
| `partId` | string | 业务零部件 ID；零件进入/切换/攻击路径→零件完成时可带值，其它为空字符串 |

**过渡开始示例：**

```json
{"from":1,"to":2,"status":0,"provinceCode":"330000","vin":"","partId":""}
```

**过渡完成示例：**

```json
{"from":-1,"to":4,"status":0,"provinceCode":"330000","vin":"ed49f47afa23e45b18d342767495643c","partId":"IDC"}
```

**接收示例：**

```java
public void onUnityControlStateTransition(String json) {
    try {
        JSONObject obj = new JSONObject(json);
        int from = obj.getInt("from");
        int to = obj.getInt("to");
        int status = obj.optInt("status", 0);
        String provinceCode = obj.optString("provinceCode", "");
        String vin = obj.optString("vin", "");
        String partId = obj.optString("partId", "");
        if (from == -1) {
            Log.d("UnityBridge", "过渡完成, level=" + to + ", partId=" + partId);
            // 隐藏 Loading、刷新原生界面
        } else {
            Log.d("UnityBridge", "过渡开始: " + from + " -> " + to);
            // 展示 Loading
        }
    } catch (JSONException e) {
        Log.e("UnityBridge", "invalid transition json: " + json, e);
    }
}
```

#### 会触发本回调的场景

**过渡开始（`from` 为 0~5）：**

| from → to | 场景 |
|-----------|------|
| 0 → 1 | 地球 → 国家 |
| 1 → 0 | 国家 → 地球 |
| 1 → 2 | 国家 → 省级 |
| 2 → 1 | 省级 → 国家 |
| 2 → 3 | 省级 → 车辆 |
| 3 → 2 | 车辆 → 省级 |
| 3 → 4 | 车辆 → 零件 |
| 4 → 3 | 零件 → 车辆 |
| 4 → 4 | 零件 → 零件切换 |
| 3 → 5 | 车辆 → 攻击路径 |
| 5 → 3 | 攻击路径 → 车辆 |
| 5 → 4 | 攻击路径 → 零件 |

**过渡完成（`from` 为 -1）：**

| to | 场景 | partId |
|----|------|--------|
| 0 | 地球级就绪 | 空 |
| 1 | 国家级就绪 | 空 |
| 2 | 省级就绪 | 空 |
| 3 | 车辆级就绪 | 空 |
| 4 | 零件级就绪 | 进入/切换/攻击路径→零件时可有值 |
| 5 | 攻击路径级就绪 | 空 |

> 跨多级跳转时，每一级过渡完成都会分别回调一次 `from=-1`。

---

### 3.2 `onUnityCarYawRotationChanged` — 车辆旋转变化通知

大屏用户拖拽车辆或 Unity 内部设角后，Unity 将当前 Yaw 通知 Android。**Android 通过本回调同步平板展示即可，无需反向调用 `SetCarYawRotation`。**

```java
public void onUnityCarYawRotationChanged(String json) { }
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `yawAngle` | float | 当前 Yaw 角度，0~360 |
| `isDragging` | bool | `true` 拖拽中连续回调；`false` 松手、API 平滑插值中或到位 |

**拖拽中示例：**

```json
{"yawAngle":45.2,"isDragging":true}
```

**松手或 API 到位示例：**

```json
{"yawAngle":90.0,"isDragging":false}
```

**说明：**

- 拖拽过程中角度变化 ≥ 0.5° 时连续回调。
- API 设角且 `instant=false` 时，平滑插值过程中按实际显示角度连续回调（`isDragging=false`），到位后再回调一次最终角度。
- API 设角且 `instant=true` 时，立即回调一次最终角度。

**接收示例：**

```java
public void onUnityCarYawRotationChanged(String json) {
    try {
        JSONObject obj = new JSONObject(json);
        float yawAngle = (float) obj.getDouble("yawAngle");
        boolean isDragging = obj.getBoolean("isDragging");
        Log.d("UnityBridge", "car yaw=" + yawAngle + ", dragging=" + isDragging);
    } catch (JSONException e) {
        Log.e("UnityBridge", "invalid car yaw json: " + json, e);
    }
}
```

---

## 四、接口汇总

### Android 需在 MainActivity 实现（Unity → Android）

| 方法 | 参数 | 说明 |
|------|------|------|
| `onUnityControlStateTransition` | JSON | 操控级别过渡开始 / 完成 |
| `onUnityCarYawRotationChanged` | JSON | 车辆 Yaw 旋转变化 |

### Android 可调用（Android → Unity）

| `UnitySendMessage` 方法名 | 第 3 参数 | 说明 |
|---------------------------|-----------|------|
| `TransitionToControlState` | JSON | 跳转到指定操控级别 |
| `TransitionToNextControlState` | `""` | 进入下一级别 |
| `TransitionToPreviousControlState` | `""` | 返回上一级别 |
| `SetBigScreenAutoCarouselEnabled` | JSON | 开启/关闭大屏自动轮播 |
| `PauseGame` | `""` | 暂停游戏 |
| `ResumeGame` | `""` | 恢复游戏 |
| `ExitThreatDrill` | `""` | 主动退出威胁下钻并进入冷却 |
| `RefreshThreatCooldown` | `""` | 刷新威胁冷却（仅冷却中有效） |
| `SetWorldMapRegionDefaults` | JSON | 设置国内外默认并立刻切换 |
| `CloseCarUI` | `""` | 关闭车辆 UI / 停止零部件轮播 |
| `CloseGJPanel` | `""` | 关闭告警面板 GJ_Panel |
| `RequestVehicleHeatmapOnce` | JSON / `""` | 主动请求一次热力图（不轮询） |
| `StartVehicleHeatmapSpecifiedTimePolling` | JSON | 开启热力图指定时段轮询（isReplay=true） |
| `StopVehicleHeatmapSpecifiedTimePolling` | `""` | 关闭指定时段，恢复默认热力图轮询 |
| `RequestCarVehicleData` | `""` / JSON | 请求车辆态势双接口（防护状态 + 攻击链路） |
| `RequestSecurityEventDetail` | `""` / JSON | 请求事件溯源详情并刷新 GJ_Panel / POI |
| `SetCarYawRotation` | JSON | 设置车辆 Yaw（**生产一般无需调用**，联调/测试用；WebGL 同名） |
| `TransitionToPlateMap` | `""` | 地球 → 板块（可选联调；WebGL 同名） |
| `TransitionToEarth` | `""` | 板块 → 地球（可选联调；WebGL 同名） |
| `FocusPlateMapModule` | 模块名 | 聚焦板块模块（可选联调；WebGL 同名） |
| `RestorePlateMapCamera` | `""` | 还原板块相机（可选联调；WebGL 同名） |

---

## 五、联调建议

1. `UnitySendMessage` 第一个参数固定为 **`AndroidBridge`**。
2. JSON 使用 UTF-8；中文省名按标准 JSON 字符串传递即可。
3. 收到 `onUnityControlStateTransition` 且 `from=-1` 时，表示目标级别已可交互。
4. 车辆旋转：Android **监听** `onUnityCarYawRotationChanged` 即可，**勿调用** `SetCarYawRotation`。
5. 需要主动跳转时调用 `TransitionToControlState`。
6. 威胁下钻：用户主动打断用 `ExitThreatDrill`；冷却中续期用 `RefreshThreatCooldown`。
7. 若长时间无回调，检查 MainActivity 方法是否为 `public`、方法名是否与上表完全一致。
8. Unity Editor 下无真实 Activity 时，回调以 `[AndroidMessage] Editor mock ...` 日志输出；`SetCarYawRotation` 可在 Demo 菜单 **Android 桥接 API** 面板中测试。
