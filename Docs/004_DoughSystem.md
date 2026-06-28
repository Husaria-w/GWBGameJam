# 004 DoughSystem Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Draft |
| Last Updated | 2026-06-27 |
| Depends On | 010_ConfigSchema, 012_Architecture |
| Required By | 005_BakingSystem, 006_ThrowSystem, 009_UISystem |

---

## Goal

管理面团的水粉比数值与档位状态。

DoughSystem 是数据层：它只负责「比例值是多少、当前是哪个档位」。UI 动画效果由 UISystem 负责，投掷时的命中判定由 ThrowSystem 负责，DoughSystem 不介入。

---

## Scope

**In Scope：**
- 记录当前水粉比（CurrentRatio，立即更新）
- 处理左键点击（加面粉）和右键长按（加水）的输入
- 将比例值 Clamp 在 [0, MaxRatio] 范围内
- 根据 DoughStateBoundaryConfig 推导 CurrentDoughState
- 在 DoughState 变化时广播 OnDoughStateChanged
- 在关卡开始或投掷完成后将比例重置为 InitialRatio

**Out of Scope：**
- 比例 UI 的弹性动画 → 009_UISystem（UISystem 读取 GetCurrentRatio() 并自行插值）
- 投掷方向与命中判定 → 006_ThrowSystem
- 烤制计时 → 005_BakingSystem
- 面团的 Sprite / 外观变化 → 009_UISystem

---

## Gameplay Rules

- 桌面始终存在一块面团（比例值始终有效），投掷期间进入短暂「无面团」窗口。
- 左键单击一次：比例向面粉方向移动 FlourClickAmount 格（比值下降）。
- 右键长按：比例每秒向水方向移动 WaterFillRate 格（比值上升，匀速线性）。
- 比例被 Clamp：下限 0.0，上限 MaxRatio（默认 3.0）。太软/太硬是合法状态，不是错误。
- 当且仅当以下条件同时满足时，玩家输入有效：
  - `GameState == PLAYING`
  - `BakingState == Idle`
  - `DoughState != None`（无面团窗口内不接受输入）
- 关卡开始（OnLevelStarted）时，比例重置为 InitialRatio。
- 投掷开始（OnThrowStarted）时，DoughState 设为 None，不接受输入。
- 投掷完成（OnThrowCompleted）时，比例重置为 InitialRatio，DoughState 从 None 恢复。

---

## State Machine

### DoughSystem 整体

```
[Absent / None]
    │
    ├─(OnLevelStarted)─────────────────► [Present]
    │                                      ratio = InitialRatio
    │                                      state = f(ratio)
    │
    └─(OnThrowCompleted)───────────────► [Present]
                                          ratio = InitialRatio
                                          state = f(ratio)

[Present]
    │
    ├─(左键点击)──► ratio -= FlourClickAmount, clamp, re-derive state
    ├─(右键长按)──► ratio += WaterFillRate * dt per frame, clamp, re-derive state
    │
    └─(OnThrowStarted)─────────────────► [Absent / None]
                                          DoughState = None
```

### DoughState 推导规则

```
CurrentRatio >= ThresholdSoftToSoftest          → DoughState.TooSoft
ThresholdSoftestToMedium <= ratio < TooSoft     → DoughState.Softest
ThresholdMediumToHardest <= ratio < Softest     → DoughState.Medium
ThresholdHardestToHard   <= ratio < Medium      → DoughState.Hardest
ratio < ThresholdHardestToHard                  → DoughState.TooHard
DoughState.None（手动设置，不通过比值推导）
```

每次 ratio 改变后立即重新推导。若推导结果与上一帧不同，广播 OnDoughStateChanged。

---

## Data Model

**本系统拥有（Own）：**
- `float CurrentRatio` — 当前水粉比，立即更新，不做平滑
- `DoughState CurrentDoughState` — 由 ratio 推导，或手动设为 None

**本系统读取（Read）：**
- `DoughConfig`（FlourClickAmount、WaterFillRate、InitialRatio、MaxRatio）
- `DoughStateBoundaryConfig`（四个边界阈值）
- `OnGameStateChanged`、`OnBakingStateChanged` — 控制输入有效性
- `OnLevelStarted`、`OnThrowStarted`、`OnThrowCompleted` — 控制重置与 None 窗口

**本系统对外暴露（公开 API）：**

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `GetCurrentRatio()` | float | UISystem 读取此值作为弹性动画的目标值 |
| `GetCurrentDoughState()` | DoughState | ThrowSystem 读取以执行命中判定 |

**本系统不拥有：**
- UI 显示的插值比例 → UISystem 内部维护
- 投掷是否成功 → ThrowSystem
- 当前是否处于烤制阶段 → BakingSystem

---

## Config

> 以下字段新增至 `010_ConfigSchema` 中的 `DoughConfig`。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| InitialRatio | float | 1.0 | 0.0 ~ MaxRatio | 关卡开始和每次投掷后重置的水粉比默认值。1.0 = 中等（Medium）起点，玩家需少量调整即可命中任意档位 |
| MaxRatio | float | 3.0 | ThresholdSoftToSoftest+0.1 ~ 10.0 | 比例值上限，限制水方向的最大值。必须大于 ThresholdSoftToSoftest（1.75），否则无法进入太软状态 |

*已在 ConfigSchema 中定义的字段（FlourClickAmount、WaterFillRate）此处不重复列出。*

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnDoughStateChanged | newState (DoughState), previousState (DoughState) | DoughState 发生变化时（包括变为 None 或从 None 恢复）|

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后做什么 |
|--------|---------|------------|
| OnLevelStarted | GameLoop | 重置 ratio = InitialRatio，重新推导 DoughState，广播 OnDoughStateChanged |
| OnThrowStarted | ThrowSystem | 设 DoughState = None，广播 OnDoughStateChanged(None, prev) |
| OnThrowCompleted | ThrowSystem | 重置 ratio = InitialRatio，重新推导 DoughState，广播 OnDoughStateChanged |
| OnGameStateChanged | GameLoop | PAUSED → 停止接受输入（停止右键长按计时）；PLAYING → 恢复 |
| OnBakingStateChanged | BakingSystem | 非 Idle → 禁用输入；Idle → 恢复输入（仅在 DoughState != None 时） |

---

## Edge Cases

**#1 比例到达 MaxRatio 后继续右键长按**
处理方式：Clamp(ratio, 0, MaxRatio)，超出时静默忽略增量，DoughState 维持 TooSoft 不变（不重复广播相同状态）。

**#2 比例到达 0 后继续左键点击**
处理方式：Clamp 到 0，DoughState 维持 TooHard。

**#3 左键点击使比例从某档位精确落在边界值上**
边界值属于哪一侧：约定 `>= Threshold` 才进入更软一侧（不含下界的区间定义见状态推导表）。这确保了边界行为唯一。

**#4 比例推导结果与上一帧相同（ratio 变化但未跨越边界）**
处理方式：不广播 OnDoughStateChanged，避免无意义的事件洪流（尤其是右键长按时每帧都可能触发）。

**#5 OnThrowStarted 和 OnThrowCompleted 之间发生游戏暂停**
处理方式：暂停期间输入已因 OnGameStateChanged(PAUSED) 停止；ThrowSystem 的动画由 TimeScale=0 冻结；OnThrowCompleted 在 Resume 后正常到达，DoughSystem 正常重置。不需要额外处理。

**#6 OnLevelStarted 与 OnThrowCompleted 同帧触发**
理论上不应发生（关卡开始时不存在飞行中的面包）；若发生，以 OnLevelStarted 处理结果为准（两者均设为 InitialRatio，结果幂等）。

**#7 DoughState = None 时玩家尝试操作面团**
处理方式：输入检测在 Update 中先判断 DoughState != None，若为 None 直接 return，不执行任何比例运算。

---

## Acceptance Criteria

- [ ] Given 游戏进入 PLAYING 且 BakingState = Idle，When 左键单击，Then CurrentRatio 立即减少 FlourClickAmount，OnDoughStateChanged 若档位变化则广播
- [ ] Given 右键长按，When 持续 1s，Then CurrentRatio 增加 WaterFillRate（线性匀速），与帧率无关
- [ ] Given CurrentRatio 在 Medium 区间中部，When 右键长按至超过 ThresholdSoftestToMedium，Then OnDoughStateChanged(Softest, Medium) 广播一次
- [ ] Given CurrentRatio 已到达 MaxRatio，When 继续右键长按，Then CurrentRatio 不变，不广播 OnDoughStateChanged
- [ ] Given CurrentRatio 已到达 0，When 继续左键点击，Then CurrentRatio 不变，不变 TooHard
- [ ] Given OnLevelStarted 触发，Then CurrentRatio = InitialRatio，DoughState = f(InitialRatio)
- [ ] Given OnThrowStarted 触发，Then DoughState = None，广播 OnDoughStateChanged(None, prev)
- [ ] Given OnThrowCompleted 触发，Then CurrentRatio = InitialRatio，DoughState = f(InitialRatio)，广播 OnDoughStateChanged
- [ ] Given DoughState = None，When 左键点击或右键长按，Then CurrentRatio 不变
- [ ] Given BakingState != Idle（正在烤制），When 左键点击，Then CurrentRatio 不变
- [ ] Given 游戏暂停，When 右键处于长按状态，Then 比例不继续增加

---

## Test Plan

**Test 1 — 左键加粉**
1. 运行游戏，PLAYING 且 BakingState = Idle
2. 在 Inspector 中观察 DoughSystem.CurrentRatio
3. 左键点击一次
4. ✓ CurrentRatio 减少 FlourClickAmount（0.75），Console 若跨档位则广播 OnDoughStateChanged

**Test 2 — 右键加水（线性）**
1. 右键长按 2 秒
2. ✓ CurrentRatio 增加 = WaterFillRate × 2（误差 < 0.05）
3. ✓ 增加速率在整个过程中均匀，无加速或减速

**Test 3 — Clamp 验证**
1. 右键长按直到 CurrentRatio 不再增加
2. ✓ CurrentRatio = MaxRatio（默认 3.0）
3. 左键多次点击直到 CurrentRatio 不再减少
4. ✓ CurrentRatio = 0.0

**Test 4 — 状态推导**
1. 从 Medium 区间手动调节到 Softest（右键短按）
2. ✓ Inspector 中 CurrentDoughState = Softest，Console 显示 OnDoughStateChanged(Softest, Medium)
3. 同一档位内继续右键长按
4. ✓ 无重复的 OnDoughStateChanged 事件

**Test 5 — None 窗口**
1. 触发投掷（ThrowSystem.OnThrowStarted）
2. ✓ DoughState = None，左键点击无效
3. 投掷动画完成（OnThrowCompleted）
4. ✓ CurrentRatio = InitialRatio，DoughState = Medium（若 InitialRatio = 1.0）

**Test 6 — 烤制期间禁止输入**
1. 按住空格进入烤制
2. ✓ 左键点击和右键长按不改变 CurrentRatio

---

## Future Extensions

- **面团种类（不同初始比例或特殊属性）**：当前 InitialRatio 为全局单一值；若要按关卡或随机变化，需将 InitialRatio 扩展为 LevelData 中的字段，**DoughSystem 接口不变**。
- **多投掷物（同时持有多个面团）**：当前单一 CurrentRatio 设计；扩展需要 DoughSystem 维护一个比例槽位数组，**需中等改动**。
- **面团外观随比例变化**：UISystem 读取 GetCurrentRatio() 已支持，**无需修改 DoughSystem**。
