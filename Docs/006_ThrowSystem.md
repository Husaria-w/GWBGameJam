# 006 ThrowSystem Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Draft |
| Last Updated | 2026-06-27 |
| Depends On | 003_MonsterSystem, 004_DoughSystem, 010_ConfigSchema, 012_Architecture |
| Required By | 009_UISystem |

---

## Goal

接收投掷请求，执行面包飞行动画（抛物线），在到达时即时完成命中判定并通知各系统。

ThrowSystem 是投掷流程的唯一执行者：它捕获投掷时刻的面团比例、驱动抛物线动画、完成命中判定，其余系统（MonsterSystem、UISystem）只需监听结果事件。

---

## Scope

**In Scope：**
- 订阅 OnThrowRequested，在同帧捕获当前面团比例与目标坐标
- 生成面包投射物 Prefab 并驱动抛物线飞行动画
- 发布 OnThrowStarted（面包离桌）
- 动画到达时：计算 ThrowResult，调用 MonsterSystem API，发布 OnThrowCompleted
- 处理游戏暂停时冻结动画

**Out of Scope：**
- 决定投掷球道 → BakingSystem（已在 OnThrowRequested 中给出）
- 待移动状态目标预判 → MonsterSystem.GetTargetPosition()（ThrowSystem 直接调用）
- 怪物闪白动画的播放 → MonsterSystem.TriggerWrongHitFeedback()（ThrowSystem 调用触发）
- 爆炸特效的资产制作 → UISystem / VFX（ThrowSystem 只负责在命中时实例化特效 Prefab）
- 面团比例 UI 的弹性动画 → UISystem
- 桌子 HP → TableSystem

---

## Gameplay Rules

- 收到 OnThrowRequested 后，**在同帧**读取面团比例（DoughSystem.GetCurrentRatio()）和目标点（MonsterSystem.GetTargetPosition() 或空置球道默认点），然后发布 OnThrowStarted。
- 面包沿抛物线从桌子中心飞向目标点，飞行时间 = ThrowDuration（固定时长，与距离无关）。
- 动画到达时**立即**（同帧）完成命中判定与系统通知，不等待特效播放结束。
- **命中判定逻辑**（基于捕获的比例，非到达时的实时比例）：
  - 目标球道无怪物 → `ThrowResult.EmptyLane`
  - 有怪物，且 `|capturedRatio - targetDoughStateCenter| ≤ ToleranceHalfWidth` → `ThrowResult.Hit`
  - 有怪物，但比例不在容错区间内 → `ThrowResult.WrongRatio`
- **Hit**：调用 `MonsterSystem.DefeatMonster(laneIndex)`，实例化爆炸特效 Prefab。
- **WrongRatio**：调用 `MonsterSystem.TriggerWrongHitFeedback(laneIndex)`，面包消失。
- **EmptyLane**：面包消失，无任何效果。

### 各 DoughState 的目标中心比例（targetDoughStateCenter）

| 怪物 TargetDoughState | 中心比例（ratio） |
|----------------------|----------------|
| Softest | (ThresholdSoftToSoftest + ThresholdSoftestToMedium) / 2，默认 1.50 |
| Medium | (ThresholdSoftestToMedium + ThresholdMediumToHardest) / 2，默认 1.00 |
| Hardest | (ThresholdMediumToHardest + ThresholdHardestToHard) / 2，默认 0.50 |

---

## Parabolic Arc

面包飞行轨迹公式（每帧由参数 t ∈ [0, 1] 驱动）：

```
position(t) = Lerp(startPos, endPos, t)
            + Vector2.up * PeakHeight * 4 * t * (1 - t)
```

- `startPos`：桌子中心世界坐标（ThrowOrigin，Inspector 配置）
- `endPos`：目标点世界坐标（捕获时确定，动画期间不再更新）
- `PeakHeight`：弧顶高度（世界单位，ThrowConfig 配置）
- `t`：= elapsed / ThrowDuration，线性增长

特性：弧顶在 t = 0.5（路程中点），全程固定时长，短距离弧偏平、长距离弧更陡，视觉上有抛物线感。

---

## 空置球道默认落点

若目标球道无怪物（EmptyLane），面包仍需飞向某处：

```
endPos = LaneSystem.GetWaypoint(laneIndex, MoveStepCount / 2)
```

取该球道的中间点位，使面包视觉上落入球道中部，视觉合理。

---

## State Machine

```
[Idle]（无投掷进行中）
    │
    └─(OnThrowRequested)──► 同帧捕获 ratio、targetPos
                             实例化 Projectile
                             发布 OnThrowStarted(laneIndex)
                             → [InFlight]

[InFlight]（动画进行中，t: 0→1）
    │
    ├─(PAUSED)──► 冻结 t（不再累加）
    │             → [InFlight_Paused]
    │
    └─(t >= 1)──► 执行命中判定
                  调用 MonsterSystem API（按结果）
                  销毁 Projectile
                  发布 OnThrowCompleted(laneIndex, result)
                  → [Idle]

[InFlight_Paused]
    └─(PLAYING 恢复)──► 从当前 t 值继续
                        → [InFlight]
```

*同时只有一个投掷进行中（DoughState.None 保护阻止二次触发，见 005_BakingSystem Edge Case #5）。*

---

## Data Model

**本系统拥有（Own）：**
- `float _capturedRatio` — 投掷触发时捕获的面团比例（动画期间不变）
- `Vector2 _targetPos` — 目标世界坐标（动画期间不变）
- `int _targetLaneIndex` — 目标球道索引
- `float _flightTimer` — 当前动画进度（0 → ThrowDuration）
- `GameObject _activeProjectile` — 飞行中的面包实例（null = 无投掷）

**本系统读取（Read）：**
- `DoughSystem.GetCurrentRatio()` — 仅在 OnThrowRequested 同帧读取一次
- `MonsterSystem.GetTargetPosition(laneIndex)` — 仅在 OnThrowRequested 同帧读取一次
- `MonsterSystem.GetMonsterInLane(laneIndex)` — 命中判定时读取
- `DoughStateBoundaryConfig`（四个边界阈值 + ToleranceHalfWidth）— 命中判定
- `ThrowConfig`（ThrowDuration、PeakHeight）
- `OnGameStateChanged` — 暂停 / 恢复处理

**本系统对外暴露（公开 API）：**
无。ThrowSystem 完全由事件驱动，不需要外部直接调用。

---

## Config

> 以下新增 `ThrowConfig` ScriptableObject，同步更新 `010_ConfigSchema`。

**ThrowConfig**（`ScriptableObjects/Configs/ThrowConfig.asset`）

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| ThrowDuration | float | 0.4 | 0.1 ~ 2.0 | 面包从桌子飞至目标的总时长（秒）。值越小投掷越快，值越大玩家等待越久 |
| PeakHeight | float | 3.0 | 0.5 ~ 10.0 | 抛物线弧顶高度（世界单位）。影响视觉高度感，不影响飞行时间或命中判定 |

另：`ThrowOrigin` 为 `ThrowSystem` 组件上的 `[SerializeField] Transform`，在 Inspector 中绑定桌子中心的空节点（不放入 SO，属于场景配置）。

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnThrowStarted | laneIndex (int) | 面包投射物实例化完成，开始飞行 |
| OnThrowCompleted | laneIndex (int), result (ThrowResult) | 面包到达目标点，命中判定完成 |

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后做什么 |
|--------|---------|------------|
| OnThrowRequested | BakingSystem | 捕获数据、生成投射物、发布 OnThrowStarted，开始动画 |
| OnGameStateChanged | GameLoop | PAUSED → 冻结 _flightTimer；PLAYING → 恢复累加 |

---

## Edge Cases

**#1 动画进行中目标怪物提前逃跑（MonsterSystem 销毁了怪物）**
处理方式：命中判定在动画到达时才执行（而非飞行中持续检测）。到达时调用 `GetMonsterInLane(laneIndex)`，若返回 null 则结果为 `EmptyLane`（怪物已逃跑，面包落空）。这是合理的游戏行为，不做额外处理。

**#2 动画进行中目标球道新生成了一只怪物**
处理方式：同 #1，命中判定在到达时读取当前状态。新怪物刚在该位置生成，面包落在同位置，可能命中。这是合理的游戏行为（面包「意外」命中刚生成的怪物）。

**#3 捕获的 capturedRatio 正好在 ToleranceHalfWidth 边界上**
处理方式：使用 `<=`（含边界）判定。`|capturedRatio - center| <= ToleranceHalfWidth` 时为 Hit，确保边界行为唯一。

**#4 MonsterSystem.GetTargetPosition 返回 Vector2.zero（越界保护触发）**
此情况意味着 laneIndex 越界（正常流程不应发生）。
处理方式：发布 OnThrowStarted，执行动画飞至 (0, 0)，到达后判定为 EmptyLane（GetMonsterInLane 越界返回 null），发布 OnThrowCompleted(EmptyLane)，输出 Warning 日志。游戏不崩溃。

**#5 ThrowDuration 极短（< 1帧）**
若 ThrowDuration < Time.deltaTime，则第一帧 t 就 >= 1，动画跳过。
处理方式：进入 InFlight 的同帧检测 t >= 1，允许零帧动画（面包瞬移），行为正确但视觉上不可见。Config 最小值约束（0.1s）可避免策划误设，但不崩溃。

**#6 游戏暂停后立即切换场景**
ThrowSystem 在 OnDestroy 中销毁 _activeProjectile，发布 OnThrowCompleted 不作为（已在新场景中）。OnDestroy 不发布事件（避免向已清空的系统广播）。

---

## Acceptance Criteria

- [ ] Given OnThrowRequested(2) 收到，Then 同帧生成面包投射物，发布 OnThrowStarted(2)，DoughSystem 开始 None 窗口
- [ ] Given 面包飞行中，When 在 Scene 视图观察，Then 面包沿抛物线运动，弧顶高度约 PeakHeight 世界单位
- [ ] Given ThrowDuration = 0.4s，Then 从发布 OnThrowStarted 到 OnThrowCompleted 约 0.4s（误差 < 0.05s）
- [ ] Given 目标球道有 Medium 怪物，capturedRatio = 1.0（Medium 中心），ToleranceHalfWidth = 0.25，Then OnThrowCompleted(result = Hit) 广播，怪物消失，爆炸特效播放
- [ ] Given 目标球道有 Medium 怪物，capturedRatio = 1.6（Softest 区间），Then OnThrowCompleted(result = WrongRatio)，怪物闪白不消失
- [ ] Given 目标球道无怪物，Then OnThrowCompleted(result = EmptyLane)，无任何怪物 API 调用
- [ ] Given 面包飞行中，When 游戏暂停，Then 面包停在当前位置；Resume 后从断点继续飞行
- [ ] Given 面包飞行中目标怪物逃跑，When 面包到达，Then 结果 = EmptyLane（不命中已不存在的怪物）
- [ ] Given capturedRatio 恰好等于 center + ToleranceHalfWidth（边界），Then 结果 = Hit（含边界）

---

## Test Plan

**Test 1 — 抛物线形状**
1. 运行游戏，触发投掷（目标 Lane_2）
2. ✓ Scene 视图中面包轨迹为曲线，中点最高，两端接近直线
3. ✓ 弧顶高度约等于 Inspector 中 PeakHeight 设置值

**Test 2 — 命中判定（Hit）**
1. 将 DoughConfig.InitialRatio = 1.0，目标球道有 MonsterData_B（TargetDoughState = Medium）
2. 不调整比例，直接烤制投掷
3. ✓ OnThrowCompleted(Hit)，怪物消失，爆炸特效出现

**Test 3 — 命中判定（WrongRatio）**
1. 将面团调至 Softest（ratio ≈ 1.5），目标球道有 MonsterData_B（Medium）
2. 投掷
3. ✓ OnThrowCompleted(WrongRatio)，怪物闪白，怪物继续存在

**Test 4 — EmptyLane**
1. 确保目标球道无怪物
2. 投掷
3. ✓ OnThrowCompleted(EmptyLane)，无爆炸无闪白

**Test 5 — 比例捕获时机验证**
1. 将面团调至 Medium（ratio = 1.0）
2. 按下空格进入烤制
3. 快速（在 ThrowDuration 内）手动调节比例至 Softest（需借助 Inspector 直接修改 CurrentRatio）
4. 等面包到达
5. ✓ 命中判定仍使用投掷触发时的 ratio（Medium），结果为 Hit（而非 WrongRatio）

**Test 6 — 暂停保持**
1. 投掷进行中，约 t=0.5 时按 Esc
2. ✓ 面包停在弧顶附近
3. Resume
4. ✓ 面包继续飞行到目标点并完成判定

---

## Future Extensions

- **投掷速度随距离变化**：当前 ThrowDuration 固定；若要近目标快、远目标慢，可改为 `ThrowDuration = distance / ThrowSpeed`，**Config 结构需小改**。
- **多投射物同时飞行**：当前单投射物（DoughState.None 保护）；若要并发投掷，需将单个状态升级为投射物列表，**需中等改动**。
- **投射物样式随烤制结果变化（不熟/熟/焦）**：ThrowSystem 在实例化时已知捕获时的 BakingState（可从事件扩展），Prefab 选择可按 BakingState 切换，**需小改**。
