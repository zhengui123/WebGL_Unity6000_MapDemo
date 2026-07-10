# Unity ↔ Android 桥接 API

本文档描述 **Unity 中央可视化** 与 **Android 原生宿主** 之间的双向通信接口。

实现脚本：`Assets/Scripts/Core/AndroidBridge/AndroidMessage.cs`  
Unity 场景中需存在名为 **`AndroidBridge`** 的 GameObject，并挂载 `AndroidMessage` 组件。

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
| `provinceName` | string | 否 | 省名，如「山东」；省略或 `""` 使用 Unity 默认 |
| `provinceModuleName` | string | 否 | 省级 3D 板块 GameObject 名 |
| `partId` | string | 否 | 业务零部件 ID；进入零件级、零件切换、攻击路径→零件时使用 |
| `useInstantTransition` | bool | 否 | 是否跳过过渡动画；省略为 `false` |

**示例 — 跳到省级：**

```java
String json = "{"
    + "\"targetState\":2,"
    + "\"provinceName\":\"山东\","
    + "\"provinceModuleName\":\"polySurface3\","
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
    + "\"partId\":\"PART-1575\","
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

### 2.5 `SetCarYawRotation` — 设置车辆 Y 轴旋转角度

> **特殊说明（重要）**  
> **Android 侧在正式业务中无需调用本接口。**  
> 车辆旋转由 Unity 大屏侧用户拖拽（`MouseDragYawRotate`）驱动；Android 只需实现并监听 **`onUnityCarYawRotationChanged`**（见 3.2 节），用于平板端同步展示当前朝向。  
> 本接口仅供 Unity Editor / Demo 联调、自动化测试等场景使用，**不作为 Android 生产集成项**。

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
| `partId` | string | 业务零部件 ID；零件进入/切换/攻击路径→零件完成时可带值，其它为空字符串 |
| `status` | int | **（预留）** 大屏跳转状态：`0` 普通、`1` 信息跳转、`2` 威胁下钻；当前 Unity 暂统一回传 `0` |

**过渡开始示例：**

```json
{"from":1,"to":2,"partId":"","status":0}
```

**过渡完成示例：**

```json
{"from":-1,"to":4,"partId":"Group01","status":0}
```

**接收示例：**

```java
public void onUnityControlStateTransition(String json) {
    try {
        JSONObject obj = new JSONObject(json);
        int from = obj.getInt("from");
        int to = obj.getInt("to");
        String partId = obj.optString("partId", "");
        int status = obj.optInt("status", 0);
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
| `SetCarYawRotation` | JSON | 设置车辆 Yaw（**Android 无需调用**，仅联调/测试） |

---

## 五、联调建议

1. `UnitySendMessage` 第一个参数固定为 **`AndroidBridge`**。
2. JSON 使用 UTF-8；中文省名按标准 JSON 字符串传递即可。
3. 收到 `onUnityControlStateTransition` 且 `from=-1` 时，表示目标级别已可交互。
4. 车辆旋转：Android **监听** `onUnityCarYawRotationChanged` 即可，**勿调用** `SetCarYawRotation`。
5. 需要主动跳转时调用 `TransitionToControlState`。
6. 若长时间无回调，检查 MainActivity 方法是否为 `public`、方法名是否与上表完全一致。
7. Unity Editor 下无真实 Activity 时，回调以 `[AndroidMessage] Editor mock ...` 日志输出；`SetCarYawRotation` 可在 Demo 菜单 **Android 桥接 API** 面板中测试。
