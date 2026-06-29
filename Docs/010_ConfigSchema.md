# 010 ConfigSchema Spec

| 字段 | 内容 |
|------|------|
| Version | 1.3 |
| Status | Approved |
| Last Updated | 2026-06-27 |
| Depends On | 001_GameLoop, 012_Architecture |
| Required By | 002, 003, 004, 005, 006, 007, 008, 009, 011 |

---

## Goal

集中定义项目内所有 ScriptableObject 的字段名称、类型、默认值与合法范围，作为整个项目的数据契约。

后续每个 System Spec 引用 Config 字段时，直接引用本文档中的名称，确保命名一致、不产生歧义。

---

## Scope

**In Scope：**
- 所有运行时 ScriptableObject 的字段定义
- 字段的类型、默认值、合法范围、策划说明
- SO 之间的引用关系
- 枚举类型定义

**Out of Scope：**
- 字段的具体使用逻辑 → 各 System Spec
- SO 的 C# 类实现细节 → 013_CodingRules + 编码阶段
- 球道点位的计算方式 → 002_LaneSystem
- UI 如何展示这些数据 → 009_UISystem

---

## 枚举定义

所有枚举定义于 `Scripts/Core/GameEnums.cs`。

### DoughState
```
None        ← 桌面无面团（初始状态 / 投掷后）
TooSoft     ← 太软（比例超出上限）
Softest     ← 最软（3:2）
Medium      ← 中等（1:1）
Hardest     ← 最硬（1:2）
TooHard     ← 太硬（比例低于下限）
```

### BakingState
```
Idle        ← 未开始烤制
Undercooked ← 不熟（0 ~ UndercookedDuration）
Cooked      ← 熟了（UndercookedDuration ~ CookedDuration）
Burnt       ← 烤焦（CookedDuration 之后）
```

### ThrowResult
```
EmptyLane   ← 目标球道无怪物
Hit         ← 命中且比例正确（怪物消灭）
WrongRatio  ← 命中但比例错误（怪物闪白）
```

### GameState
```
MainMenu
Playing
Paused
LevelTransition
Death
Victory
```

---

## SO 一览

| SO 类名 | Asset 路径 | 实例数量 | 说明 |
|---------|-----------|---------|------|
| GameLoopConfig | ScriptableObjects/Configs/GameLoopConfig.asset | 1 | 全局游戏流程参数 |
| BakingConfig | ScriptableObjects/Configs/BakingConfig.asset | 1 | 烤制时间阈值 |
| MonsterConfig | ScriptableObjects/Configs/MonsterConfig.asset | 1 | 怪物移动与缩放参数 |
| LevelConfig | ScriptableObjects/Configs/LevelConfig.asset | 1 | 含3关数据的数组 |
| TableConfig | ScriptableObjects/Configs/TableConfig.asset | 1 | 桌子HP参数 |
| DoughConfig | ScriptableObjects/Configs/DoughConfig.asset | 1 | 面团输入参数 |
| DoughStateBoundaryConfig | ScriptableObjects/Configs/DoughStateBoundaryConfig.asset | 1 | 比例档位边界与容错 |
| LaneWaypointConfig | ScriptableObjects/Configs/LaneWaypointConfig.asset | 1 | 40个球道点位（由Editor工具生成）|
| MonsterData | ScriptableObjects/MonsterData/MonsterData_[X].asset | 3（每种怪一个）| 怪物图片与对应面团档位 |
| ThrowConfig | ScriptableObjects/Configs/ThrowConfig.asset | 1 | 面包投射物飞行参数 |

---

## GameLoopConfig

**职责：** 全局游戏流程控制参数。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| TotalLevels | int | 3 | 1 ~ 10 | 游戏总关卡数。修改后必须同步 LevelConfig 中的关卡数组长度 |
| DevSpeedMultiplier | float | 3.0 | 1.5 ~ 10.0 | Ctrl+Shift+P 加速倍率。超过 10 可能导致物理/碰撞异常 |
| LevelTransitionDuration | float | 0.0 | 0.0 ~ 5.0 | 关卡过渡界面最短停留秒数。设为 0 时仍需按任意键，不自动跳过 |

---

## BakingConfig

**职责：** 控制烤制三段时间阈值。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| UndercookedDuration | float | 0.5 | 0.1 ~ 2.0 | 按下空格后，持续此秒数内为「不熟」状态 |
| CookedDuration | float | 1.5 | UndercookedDuration+0.1 ~ 5.0 | 超过此秒数后变为「烤焦」。此值必须大于 UndercookedDuration |
| BurntForcedThrowDuration | float | 2.5 | CookedDuration+0.1 ~ 8.0 | 超过此秒数后强制投出，无论玩家是否松开空格 |

**约束：** `UndercookedDuration < CookedDuration < BurntForcedThrowDuration`，违反时在 Inspector 中以警告提示。

---

## MonsterConfig

**职责：** 控制怪物移动节奏与透视缩放。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| MoveIntervalSeconds | float | 1.0 | 0.2 ~ 5.0 | 怪物每步移动的时间间隔（秒）。值越小怪物移动越快 |
| MoveStepCount | int | 8 | 4 ~ 16 | 怪物从生成到抵达桌子的总步数。修改后需重新生成 LaneWaypointConfig |
| PendingMoveThreshold | float | 0.5 | 0.1 ~ MoveIntervalSeconds | 距下次移动不足此秒数时，进入「待移动」状态，面包将预判下一点位 |
| ScaleCurve | AnimationCurve | 见下方说明 | — | X轴=点位序号(0~MoveStepCount-1)，Y轴=Sprite缩放比例。调整怪物透视大小变化 |
| MoveDuration | float | 0.3 | 0.05 ~ MoveIntervalSeconds-0.1 | 每步移动动画的持续时长（秒）。含在 MoveIntervalSeconds 周期内，不额外增加总间隔。不得 ≥ MoveIntervalSeconds，否则动画未完成就开始下一步 |
| WrongHitFlashCount | int | 2 | 1 ~ 5 | 怪物受到错误面团命中时闪白的次数 |
| WrongHitFlashDuration | float | 0.1 | 0.05 ~ 0.5 | 每次闪白的持续时长（秒）|

**约束：** `MoveDuration < MoveIntervalSeconds`，违反时 Awake 中输出 Error 并强制 `MoveDuration = MoveIntervalSeconds * 0.3f`。

**ScaleCurve 默认关键帧：**

| 点位序号（X） | 缩放比例（Y） |
|-------------|------------|
| 0 | 0.01 |
| 2 | 0.50 |
| 5 | 1.00 |
| 7 | 1.50 |

---

## LevelConfig

**职责：** 定义各关卡的怪物生成节奏与总量。

顶层字段：

| 字段名 | 类型 | 说明 |
|--------|------|------|
| Levels | LevelData[] | 关卡数组，长度须与 GameLoopConfig.TotalLevels 一致 |

**LevelData（数组元素，每关一个）：**

| 字段名 | 类型 | 默认值（Lv1 / Lv2 / Lv3） | 合法范围 | 策划说明 |
|--------|------|--------------------------|---------|---------|
| SpawnIntervalSeconds | float | 9.0 / 7.0 / 5.0 | 1.0 ~ 30.0 | 相邻两次怪物生成的间隔（秒） |
| TotalMonsters | int | 10 / 15 / 20 | 1 ~ 100 | 本关总出怪数量。全部退场（击败或逃跑）后本关结束 |

---

## TableConfig

**职责：** 控制桌子承受怪物冲击的次数上限。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| MaxHits | int | 5 | 1 ~ 20 | 桌子能被怪物碰到的最大次数。超过后触发死亡 |

---

## DoughConfig

**职责：** 控制面团输入的响应速度与幅度。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| FlourClickAmount | float | 0.75 | 0.1 ~ 2.0 | 每次左键单击，比例条向面粉方向移动的格数（0.75 = 3/4格）|
| WaterFillRate | float | 0.5 | 0.1 ~ 3.0 | 右键长按时，比例条每秒向水方向移动的格数（固定速率，无加速）|
| InitialRatio | float | 1.0 | 0.0 ~ MaxRatio | 关卡开始及每次投掷后面团比例的重置值。1.0 = 中等（Medium）|
| MaxRatio | float | 3.0 | ThresholdSoftToSoftest+0.1 ~ 10.0 | 比例值上限，限制加水方向的最大值，必须大于 ThresholdSoftToSoftest |

**说明：** 比例条以"格"为单位（1格 = 一个档位宽度），两个字段的单位相同，策划可直接对比两者速度差异。

---

## DoughStateBoundaryConfig

**职责：** 定义五档面团状态的比例边界与命中容错区间。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| ThresholdSoftToSoftest | float | 1.75 | 1.5 ~ 3.0 | 「太软」与「最软」的分界水粉比。高于此值 = 太软 |
| ThresholdSoftestToMedium | float | 1.25 | 1.0 ~ 1.5 | 「最软」与「中等」的分界水粉比 |
| ThresholdMediumToHardest | float | 0.75 | 0.5 ~ 1.0 | 「中等」与「最硬」的分界水粉比 |
| ThresholdHardestToHard | float | 0.25 | 0.1 ~ 0.5 | 「最硬」与「太硬」的分界水粉比。低于此值 = 太硬 |
| ToleranceHalfWidth | float | 0.25 | 0.05 ~ 0.5 | 命中容错半宽（以格为单位）。面团比例落在目标档位中心 ±此值内，判定为命中。同步控制 UI 参考线的显示宽度 |

**约束：** `ThresholdSoftToSoftest > ThresholdSoftestToMedium > ThresholdMediumToHardest > ThresholdHardestToHard > 0`，违反时 Inspector 警告。

**水粉比参照：**

```
0.0 ──── 0.25 ──── 0.5 ──── 0.75 ──── 1.0 ──── 1.25 ──── 1.5 ──── 1.75 ────►
[太硬]         [最硬]        [中]        [最软]               [太软]
               ↑0.5         ↑1.0        ↑1.5
            (1:2)          (1:1)       (3:2)
```

---

## ThrowConfig

**职责：** 控制面包投射物的飞行动画参数。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| ThrowDuration | float | 0.4 | 0.1 ~ 2.0 | 面包从桌子飞至目标的总时长（秒）。值越小投掷越快 |
| PeakHeight | float | 3.0 | 0.5 ~ 10.0 | 抛物线弧顶高度（世界单位）。影响视觉高度感，不影响飞行时间 |

*`ThrowOrigin`（投掷起点）为 ThrowSystem 组件上的 `[SerializeField] Transform`，绑定桌子中心空节点，不放入 SO。*

---

## LaneWaypointConfig

**职责：** 存储由 Editor 点位计算器生成的 40 个球道点位坐标。不手动编辑，仅由工具写入。

| 字段名 | 类型 | 说明 |
|--------|------|------|
| Lanes | LaneWaypoints[] | 长度 = 5（每条球道一个元素）|
| RecordedStepCount | int | 生成此数据时所用的 MoveStepCount 值，由计算器自动写入，不手动修改 |

**LaneWaypoints（可序列化类）：**

| 字段名 | 类型 | 说明 |
|--------|------|------|
| Positions | Vector2[] | 长度 = MonsterConfig.MoveStepCount，index 0 为最远处（生成点），末位为最近桌子处 |

**访问方式：** `LaneWaypointConfig.Lanes[laneIndex].Positions[posIndex]`

**注意：** 若 `MonsterConfig.MoveStepCount` 被修改，必须重新运行 Editor 点位计算器，否则 Positions 数组长度与步数不匹配。

---

## MonsterData

**职责：** 定义单种怪物的视觉资产与对应的有效面团档位，每种怪物一个 SO 实例。

| 字段名 | 类型 | 说明 |
|--------|------|------|
| MonsterName | string | 怪物显示名称（调试 / 日志用）|
| IdleSprite | Sprite | 怪物正常状态图片 |
| HitSprite | Sprite | 怪物受到错误面团攻击时的闪白底图（可与 IdleSprite 相同，由代码控制闪白效果）|
| TargetDoughState | DoughState | 能击杀此怪物的面团档位（枚举）。仅 Softest / Medium / Hardest 有效 |
| DisplayScale | float | 额外整体缩放倍率，乘在 MonsterConfig.ScaleCurve 之上（≥0.01，默认 1）。sprite 太小时调大此值，不影响透视缩放比例 |

**实例清单：**

| Asset 文件名 | TargetDoughState | 说明 |
|-------------|-----------------|------|
| MonsterData_A.asset | Softest | 被最软面团击杀 |
| MonsterData_B.asset | Medium | 被中等面团击杀 |
| MonsterData_C.asset | Hardest | 被最硬面团击杀 |

**MonsterSystem 引用方式：** MonsterConfig（或 MonsterSystem Inspector）中维护一个 `MonsterData[]` 数组，按类型索引引用，不使用字符串查找。

---

## SO 引用关系

```
GameLoopConfig ◄── GameLoop
BakingConfig   ◄── BakingSystem
MonsterConfig  ◄── MonsterSystem
               └── LaneSystem（读取 MoveStepCount 校验点位数）
LevelConfig    ◄── LevelSystem
TableConfig    ◄── TableSystem
DoughConfig    ◄── DoughSystem
DoughStateBoundaryConfig ◄── DoughSystem
                          └── UISystem（绘制参考线）
                          └── ThrowSystem（命中判定）
ThrowConfig    ◄── ThrowSystem
LaneWaypointConfig ◄── LaneSystem
MonsterData[]  ◄── MonsterSystem（数组，按生成时随机索引）
```

---

## Edge Cases

**#1 LevelConfig.Levels 长度与 TotalLevels 不一致**
若 `GameLoopConfig.TotalLevels = 3` 但 `LevelConfig.Levels` 只有 2 个元素：
处理方式：LevelSystem 在初始化时 Validate，若数组越界，在 Console 输出 Error 并使用最后一个关卡的数据重复填充，不崩溃。

**#2 LaneWaypointConfig 点位数据已过期（MoveStepCount 被修改后未重新烘焙）**
检测方式：LaneSystem 在 Awake 中对比 `MonsterConfig.MoveStepCount` 与 `LaneWaypointConfig.RecordedStepCount`，若不一致视为数据过期。
处理方式：输出 Error「点位数据已过期，请重新运行 Lane 点位计算器」，游戏停止进入 Playing 状态，避免数组越界崩溃。

**#3 MonsterData.TargetDoughState 设置为 ToSoft / TooHard / None**
这三个枚举值对应"无法击杀任何怪物"的状态，不应赋给 MonsterData。
处理方式：Editor 侧对 `TargetDoughState` 字段添加 `[CustomPropertyDrawer]` 过滤，仅显示 Softest / Medium / Hardest 三个选项。

**#6 MonsterConfig.MoveDuration ≥ MoveIntervalSeconds**
若策划误设动画时长超过总间隔：
处理方式：MonsterSystem 在 Awake 中 Validate，输出 Error 并强制 `MoveDuration = MoveIntervalSeconds * 0.3f`，游戏不崩溃。

**#4 BakingConfig 时间约束违反**
若策划误设 `CookedDuration < UndercookedDuration`：
处理方式：BakingSystem 在 Awake 中 Validate，若约束违反输出 Error，并强制 `CookedDuration = UndercookedDuration + 0.1f`。

**#5 DoughStateBoundaryConfig 边界约束违反**
若四个 Threshold 未保持严格递减：
处理方式：DoughSystem 在 Awake 中 Validate，输出 Error 并以默认值覆盖违规字段。

---

## Acceptance Criteria

- [ ] 所有 SO asset 文件存在于指定路径，可在 Inspector 中打开并显示正确字段
- [ ] `GameLoopConfig.TotalLevels` 修改后，LevelSystem 读取的关卡数随之变化
- [ ] `BakingConfig` 三个时间值修改后，BakingSystem 行为随之变化（Test 6 验证）
- [ ] `MonsterConfig.ScaleCurve` 修改后，怪物在 Game 视图中的缩放随之变化
- [ ] `TableConfig.MaxHits = 1` 时，第一次怪物到达桌子即触发死亡界面
- [ ] `DoughConfig.FlourClickAmount` 修改后，每次左键移动的格数量随之变化
- [ ] `DoughStateBoundaryConfig.ToleranceHalfWidth` 修改后，UI 参考线宽度和命中判定范围同步变化
- [ ] `LaneWaypointConfig` 由 Editor 工具写入后，Inspector 中显示 5 × 8 = 40 个 Vector2 坐标，且 `RecordedStepCount` = `MonsterConfig.MoveStepCount`
- [ ] 修改 `MonsterConfig.MoveStepCount` 后未重新烘焙，运行游戏时 Console 输出 Error 且不进入 Playing
- [ ] `MonsterData_A/B/C` 的 `TargetDoughState` 字段在 Inspector 中仅显示三个有效选项
- [ ] 所有 Validate 逻辑在 Awake 中执行，违规时 Console 有明确 Error / Warning 输出

---

## Test Plan

**Test 1 — SO 资产存在性**
1. 打开 Project 窗口，导航至 `ScriptableObjects/Configs/`
2. ✓ 所有 8 个 Config asset 存在

**Test 2 — BakingConfig 约束**
1. 将 `CookedDuration` 设为小于 `UndercookedDuration` 的值
2. 运行游戏
3. ✓ Console 输出 Error，`CookedDuration` 被自动修正

**Test 3 — LevelConfig 越界保护**
1. 将 `TotalLevels = 3`，但 `LevelConfig.Levels` 只填 2 个元素
2. 运行游戏
3. ✓ Console 输出 Error，第三关使用第二关数据，游戏不崩溃

**Test 4 — MonsterData TargetDoughState 过滤**
1. 在 Inspector 中打开任意 MonsterData asset
2. 点击 `TargetDoughState` 下拉菜单
3. ✓ 仅显示 Softest / Medium / Hardest，不出现 TooSoft / TooHard / None

**Test 5 — LaneWaypointConfig 写入**
1. 运行 Editor 点位计算器（011_DevTools）
2. 点击「烘焙」
3. ✓ `LaneWaypointConfig` 中 `Lanes` 数组长度 = 5，每个 `Positions` 长度 = MoveStepCount

---

## Future Extensions

- **更多怪物类型**：新增 `MonsterData_D.asset` 并在 MonsterSystem 引用数组中添加，**无需修改任何 Config 类代码**。
- **更多关卡**：修改 `GameLoopConfig.TotalLevels` 并在 `LevelConfig.Levels` 数组中追加，**支持扩展**。
- **动态难度（根据玩家表现调整生成间隔）**：当前 `SpawnIntervalSeconds` 为静态值，若要动态化需在 LevelSystem 中增加运行时覆盖逻辑，**Config 结构无需修改**。
- **本地化（怪物名称多语言）**：`MonsterData.MonsterName` 当前为 string，若要本地化需替换为本地化键值，**需小改 MonsterData 类**。
