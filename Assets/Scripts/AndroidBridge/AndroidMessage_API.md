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
| `partName` | string | 否 | 车辆零件对象名 |
| `partId` | string | 否 | 业务零部件 ID，仅零件→零件切换时生效 |
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
    + "\"partName\":\"Group1575\","
    + "\"useInstantTransition\":true"
    + "}";
UnityPlayer.UnitySendMessage("AndroidBridge", "TransitionToControlState", json);
```

### 说明

- 本接口为**异步发起**，Unity 不通过回调告知成功或失败。
- `targetState` 必须为 0~5；非法值时 Unity 侧会忽略请求。
- 可选字符串字段可不传；传空字符串与省略等价。

---

## 二、Unity 回调 Android：级别跳转开始通知

### 需在 MainActivity 实现的方法

```java
public void onUnityControlStateTransition(String json) { }
```

Unity 在**场景层级过渡刚开始**（动画尚未结束）时调用，用于 Android 提前切换 UI、展示 Loading 或埋点。

### 回调 JSON 字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `from` | int | 起始级别 0~5 |
| `to` | int | 目标级别 0~5 |
| `partId` | string | 业务零部件 ID；仅零件→零件 (`from=4,to=4`) 与攻击路径→零件 (`from=5,to=4`) 时有效，其它场景为空字符串 |

示例：

```json
{"from":1,"to":2,"partId":""}
```

表示从**国家级**进入**省级**的过渡已开始。

### 接收示例

```java
public void onUnityControlStateTransition(String json) {
    try {
        JSONObject obj = new JSONObject(json);
        int from = obj.getInt("from");
        int to = obj.getInt("to");
        Log.d("UnityBridge", "transition start: " + from + " -> " + to);
        // 根据 from / to 更新原生界面
    } catch (JSONException e) {
        Log.e("UnityBridge", "invalid transition json: " + json, e);
    }
}

```


---



## 三、会触发 `onUnityControlStateTransition` 的场景



以下为 Unity 侧可能发起的跳转；**仅在过渡开始时**回调一次。



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

| 3 → 5 | 车辆 → 攻击路径 |

| 5 → 3 | 攻击路径 → 车辆 |



> 同一对用户操作只会在**开始跳转**时收到通知，不会在动画结束时再次回调。



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



## 五、联调建议



1. 确认 `UnityPlayer.UnitySendMessage` 的第一个参数固定为 **`AndroidBridge`**。

2. JSON 使用 UTF-8，中文省名无需额外转义（标准 JSON 字符串即可）。

3. 收到 `onUnityControlStateTransition` 后按 `to` 切换界面；需要主动跳转时调用 `TransitionToControlState`。

4. 若长时间无回调，检查 MainActivity 方法是否为 `public`、方法名是否与上表完全一致（区分大小写）。


