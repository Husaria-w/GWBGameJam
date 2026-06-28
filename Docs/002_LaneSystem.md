# 002 LaneSystem Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Approved |
| Last Updated | 2026-06-27 |
| Depends On | 010_ConfigSchema, 012_Architecture |
| Required By | 003_MonsterSystem, 006_ThrowSystem, 011_DevTools |

---

## Goal

定义5条透视球道的几何形状、悬停高亮交互，以及供其他系统查询的40个怪物点位数据。

LaneSystem 是空间层面的基础系统：它不处理怪物或投掷逻辑，只负责"场地"的形状与状态。

---

## Scope

**In Scope：**
- 5条球道的形状定义（PolygonCollider2D）
- 鼠标悬停检测与球道高亮切换（替换Sprite + 轻微放大）
- 对外暴露 `GetWaypoint(laneIndex, posIndex)` API
- 对外暴露 `GetHoveredLaneIndex()` API
- 启动时验证 LaneWaypointConfig 数据是否过期

**Out of Scope：**
- 40个点位的生成计算 → 011_DevTools（Editor点位计算器）
- 怪物在球道上的生成与移动 → 003_MonsterSystem
- 投掷方向的决定 → 006_ThrowSystem（读取本系统暴露的API）
- 球道的UI渲染细节（字体、颜色方案）→ 009_UISystem

---

## Gameplay Rules

- 屏幕上半部分显示5条透视球道，由近到远逐渐变窄。
- 仅在烤制阶段（BakingState = Undercooked / Cooked / Burnt），鼠标悬停在某条球道上，该球道替换为高亮Sprite并轻微放大。
- 烤制阶段外（揉面、投掷动画期间），所有球道保持正常Sprite，不响应鼠标悬停。
- 鼠标离开球道区域，该球道恢复正常Sprite和大小。
- 同一时间最多只有一条球道处于高亮状态。
- 鼠标在球道之间的空隙或屏幕下半部分时，所有球道均为正常状态。
- 游戏暂停时，球道视觉状态冻结（不响应新的鼠标事件）。

---

## State Machine

### 单条球道的状态

```
[Normal]
    │
    ├─(OnMouseEnter, PLAYING状态)──► [Hovered]
    │                                     │
    └─────────────────────────────────────┤
                                    (OnMouseExit)
                                    (PAUSED / 非PLAYING)
                                          │
                                       [Normal]
```

### 整体悬停管理（LaneManager）

```
同一时间只允许一条球道处于 Hovered 状态。
新的 OnMouseEnter 事件触发时：
  1. 将旧的 Hovered 球道切换回 Normal
  2. 将新球道切换为 Hovered
  3. 广播 OnLaneHoverChanged(newLaneIndex)
```

### 激活条件

- 仅在 `GameState == PLAYING` **且** `BakingState != Idle` 时响应鼠标事件
- 订阅 `OnGameStateChanged`：非 PLAYING 状态 → 停止响应，重置所有球道为 Normal
- 订阅 `OnBakingStateChanged`：BakingState 变为 Idle → 停止响应，重置所有球道为 Normal；变为 Undercooked → 激活悬停检测

---

## Data Model

**本系统拥有（Own）：**
- 5条球道的 PolygonCollider2D 形状（在 Prefab 中定义，运行时只读）
- 当前悬停的球道索引（`int _hoveredLaneIndex`，-1表示无悬停）

**本系统读取（Read）：**
- `LaneWaypointConfig.Lanes[laneIndex].Positions[posIndex]` — 点位坐标
- `LaneWaypointConfig.RecordedStepCount` vs `MonsterConfig.MoveStepCount` — 过期检测
- `OnGameStateChanged` — 控制悬停激活
- `OnBakingStateChanged` — 仅在 Baking 阶段开启悬停高亮

**本系统对外暴露（公开API）：**
- `Vector2 GetWaypoint(int laneIndex, int posIndex)` — 返回指定点位的世界坐标
- `int GetHoveredLaneIndex()` — 返回当前悬停球道索引（-1 = 无）
- 事件 `OnLaneHoverChanged(int laneIndex)` — laneIndex = -1 表示取消悬停

**本系统不拥有：**
- 哪条球道上有怪物 → MonsterSystem
- 当前是否处于烤制状态 → BakingSystem

---

## Config

LaneSystem 无独立 ScriptableObject Config。以下参数作为 `LaneManager` 的 `[SerializeField]` 字段，在 Inspector 中配置。

| 字段名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| HoverScaleMultiplier | float | 1.05 | 悬停时球道的缩放倍率（相对于正常大小）。视觉层缩放，不影响Collider判定逻辑 |
| LaneWaypointConfig | LaneWaypointConfig | 必填 | Inspector引用，40个点位数据 |
| MonsterConfig | MonsterConfig | 必填 | Inspector引用，用于过期检测 |

---

## GameObject 结构

```
_World/Lanes/
├── Lane_0          ← 最左侧球道（laneIndex = 0）
│   ├── Visual      ← SpriteRenderer（Normal Sprite / Hovered Sprite）
│   │               ← 悬停时此节点 Scale 放大至 HoverScaleMultiplier
│   └── Collider    ← PolygonCollider2D，形状为透视四边形
│                   ← 挂载 LaneHoverDetector 脚本
├── Lane_1
├── Lane_2          ← 中间球道
├── Lane_3
└── Lane_4          ← 最右侧球道（laneIndex = 4）
```

**Visual / Collider 分离原因：**
悬停缩放只作用于 `Visual` 子节点，`Collider` 节点保持原始形状不变，避免缩放导致碰撞判定边界偏移。

**每个 Lane 包含的 Sprite 引用：**

| 字段 | 类型 | 说明 |
|------|------|------|
| NormalSprite | Sprite | 默认显示的球道图片 |
| HoveredSprite | Sprite | 鼠标悬停时替换的高亮图片 |

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnLaneHoverChanged | laneIndex (int, -1=无) | 悬停球道发生变化时（包括从有→无） |

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后做什么 |
|--------|---------|------------|
| OnGameStateChanged | GameLoop | PLAYING → 待机（由BakingState决定是否激活）；其他状态 → 停用悬停检测，重置所有球道为 Normal |
| OnBakingStateChanged | BakingSystem | BakingState ≠ Idle → 激活悬停检测；Idle → 停用悬停检测，重置所有球道为 Normal |

---

## Editor 点位计算器（概述）

*详细实现见 011_DevTools。本节仅说明 LaneSystem 侧的数据接口。*

**计算器的输入：**
- 5条球道的 PolygonCollider2D 顶点坐标（从场景中读取）
- 8条水平线的 Y 坐标（存储于 Editor-only 的 `LaneCalculatorData` SO）

**计算器的输出（写入 LaneWaypointConfig）：**
- `Lanes[laneIndex].Positions[posIndex]` — 对应水平线与球道左右边缘的交点连线中点
- `RecordedStepCount` — 本次生成时的 `MonsterConfig.MoveStepCount`

**点位顺序约定：**
- `posIndex = 0` → 最远离桌子的点（怪物生成位置，屏幕上方）
- `posIndex = MoveStepCount - 1` → 最靠近桌子的点（怪物下一步将碰桌）

---

## Edge Cases

**#1 鼠标处于两条球道的边缘重叠区域**
若相邻球道的 PolygonCollider2D 边缘有像素级重叠：
处理方式：Unity Raycast 返回最先命中的 Collider。通过调整 Collider 的 `Order in Layer` 或确保球道间无重叠来规避，不在代码中处理多重命中。

**#2 悬停状态下游戏被暂停**
处理方式：收到 `OnGameStateChanged(PAUSED)` 时，立即将当前 Hovered 球道切回 Normal 视觉状态，并广播 `OnLaneHoverChanged(-1)`。恢复 PLAYING 后重新检测鼠标位置（可在下一帧的 Update 中用 `OverlapPoint` 检查）。

**#3 LaneWaypointConfig 未赋值或为空**
处理方式：LaneManager 在 Awake 中检查引用是否为 null，若为 null 输出 Error 并阻止游戏进入 Playing 状态。

**#4 GetWaypoint 传入越界的 laneIndex 或 posIndex**
处理方式：返回 `Vector2.zero` 并输出 Warning（含传入参数值）。不抛出异常，避免连锁崩溃。

**#5 点位数据过期（RecordedStepCount ≠ MoveStepCount）**
处理方式：Awake 中输出 Error「点位数据已过期，请重新运行 Lane 点位计算器」，游戏不进入 Playing 状态（由 GameLoop 在进入前检查该错误标志）。

**#6 烤制结束时鼠标仍悬停于球道上**
投掷动作触发（面包飞出）→ BakingState 回到 Idle → LaneSystem 收到 OnBakingStateChanged(Idle)。
处理方式：立即重置所有球道为 Normal，广播 OnLaneHoverChanged(-1)，不等待 OnMouseExit 事件。

**#7 Dev模式下球道Gizmos与实际Collider不对齐**
处理方式：Editor 计算器直接读取 PolygonCollider2D 顶点，不使用独立的 Gizmo 定义顶点，确保计算结果与实际碰撞形状一致。

---

## Acceptance Criteria

- [ ] Given PLAYING 且 BakingState = Idle（未烤制），When 鼠标移入 Lane_2，Then 无任何视觉变化（球道不高亮）
- [ ] Given PLAYING 且 BakingState = Cooked（烤制中），When 鼠标移入 Lane_2，Then Lane_2 显示 HoveredSprite，Visual 节点 Scale = HoverScaleMultiplier，其余4条球道保持 NormalSprite
- [ ] Given Lane_2 处于 Hovered，When 鼠标移入 Lane_3，Then Lane_2 恢复 Normal，Lane_3 变为 Hovered，广播 OnLaneHoverChanged(3)
- [ ] Given 鼠标在任意球道上，When 按 Esc 暂停，Then 所有球道立即恢复 Normal，广播 OnLaneHoverChanged(-1)
- [ ] Given PAUSED，When 鼠标移过球道，Then 无任何视觉变化
- [ ] Given Resume 后，When 鼠标静止于 Lane_1 上方，Then Lane_1 在下一帧自动进入 Hovered 状态
- [ ] Given LaneWaypointConfig 已正确烘焙，When 调用 GetWaypoint(2, 0)，Then 返回 Lane_2 最远点的 Vector2 坐标（非 Vector2.zero）
- [ ] Given LaneWaypointConfig 已正确烘焙，When 调用 GetWaypoint(2, 7)，Then 返回 Lane_2 最近桌子处的 Vector2 坐标
- [ ] Given GetWaypoint(-1, 0) 越界调用，Then 返回 Vector2.zero，Console 输出 Warning
- [ ] Given RecordedStepCount ≠ MonsterConfig.MoveStepCount，When 运行游戏，Then Console 输出 Error，不进入 Playing 状态
- [ ] Given Dev模式，When 在 Scene 视图中查看，Then 40个点位以 Gizmo 球体可见

---

## Test Plan

**Test 1 — 悬停高亮**
1. 运行游戏，进入 Playing 状态
2. 将鼠标移入第3条球道（Lane_2）
3. ✓ Game视图：Lane_2 显示高亮图案，轻微放大
4. 将鼠标移到球道间隙
5. ✓ Lane_2 恢复正常

**Test 2 — 暂停时冻结**
1. 鼠标悬停于 Lane_0
2. 按 Esc 暂停
3. ✓ Lane_0 立即恢复 Normal
4. 移动鼠标经过其他球道
5. ✓ 无任何球道高亮变化

**Test 3 — GetWaypoint 验证**
1. 在 Scene 视图开启 Gizmos
2. ✓ 40个点位球体可见，5组×8个，排列符合透视远近关系
3. 调用 GetWaypoint(0, 0) 与 GetWaypoint(0, 7)
4. ✓ 两个坐标的 Y 值不同，posIndex=0 的 Y 值更高（更远离桌子）

**Test 4 — 过期检测**
1. 将 MonsterConfig.MoveStepCount 从 8 改为 10（不重新烘焙）
2. 运行游戏
3. ✓ Console 输出 Error，停留在 MainMenu，不进入游戏

**Test 5 — Collider 形状验证**
1. 在 Scene 视图选中每条球道
2. ✓ PolygonCollider2D 的 4 个顶点构成透视四边形，近端宽、远端窄

---

## Future Extensions

- **球道数量可配置（5改7）**：当前5条球道为硬编码数量（5个子对象）。若要改为7条，需修改场景结构和 LaneWaypointConfig 数组长度，工作量中等。建议改动前先在 DecisionLog 记录原因。
- **动态球道动画（球道闪烁/震动）**：Visual 子节点的分离设计支持在 Visual 上添加动画而不影响 Collider，**扩展成本低**。
- **触屏支持**：当前悬停检测依赖鼠标事件，触屏需改为 Touch 输入。LaneSystem 内部输入处理可替换为 InputAdapter，**系统逻辑无需修改**。
