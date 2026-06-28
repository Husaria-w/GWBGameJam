# 005 BakingSystem Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Draft |
| Last Updated | 2026-06-27 |
| Depends On | 004_DoughSystem, 010_ConfigSchema, 012_Architecture |
| Required By | 002_LaneSystem, 006_ThrowSystem, 009_UISystem |

---

## Goal

管理烤制计时与状态推进，在玩家松开空格（或强制超时）时决定投掷球道并触发投掷。

BakingSystem 是从「揉面」到「投掷」的桥梁：它只负责计时、状态转换、球道选定和投掷触发；投掷动画与命中判定交给 ThrowSystem。

---

## Scope

**In Scope：**
- 监听空格键按下 / 松开
- 维护烤制计时器与 BakingState（Idle / Undercooked / Cooked / Burnt）
- 根据计时器阈值推进 BakingState，广播 OnBakingStateChanged
- 在投掷时读取当前悬停球道（LaneSystem.GetHoveredLaneIndex()）
- 无悬停球道时（-1）随机选一条（0~4），包括强制投掷和主动松开两种情况
- 发布 OnThrowRequested(laneIndex) 通知 ThrowSystem 执行投掷
- 投掷后立即回到 Idle 状态

**Out of Scope：**
- 面包飞行动画与落点命中判定 → 006_ThrowSystem
- 烤制阶段球道高亮的显示 → 002_LaneSystem（订阅 OnBakingStateChanged）
- 烤制进度条 UI → 009_UISystem（订阅 OnBakingStateChanged，自行计时 UI）
- 面团比例的读取与 DoughState 校验 → ThrowSystem（在 OnThrowRequested 中读取）

---

## Gameplay Rules

- 仅当 `GameState == PLAYING` 且 `DoughState != None` 时，空格键有效。
- **按下空格**：BakingState 从 Idle 推进至 Undercooked，开始计时（timer = 0）。
- **计时推进**：
  - `timer < UndercookedDuration` → Undercooked
  - `UndercookedDuration <= timer < CookedDuration` → Cooked
  - `CookedDuration <= timer < BurntForcedThrowDuration` → Burnt
  - `timer >= BurntForcedThrowDuration` → 强制触发投掷，回到 Idle
- **松开空格**（任意 Undercooked / Cooked / Burnt 状态下）：立即触发投掷，回到 Idle。
- **投掷球道选定**（主动松开和强制投掷规则相同）：
  - 若 `LaneSystem.GetHoveredLaneIndex() >= 0`：使用该球道
  - 若 `-1`（未悬停）：随机选一条球道（Random.Range(0, 5)）
- 投掷触发后，BakingState 立即设为 Idle，不等待投掷动画完成。

---

## State Machine

```
[Idle]
  │
  └─(Space按下, PLAYING, DoughState!=None)──► [Undercooked]
                                               timer = 0
                                               广播 OnBakingStateChanged(Undercooked, Idle)
                                                     │
                      ┌──────────────────────────────┤
                      │                              │
                (timer >= UndercookedDuration)   (Space松开)
                      ▼                              │
                  [Cooked]                           │
                      │                              │
                      ├──────────────────────────────┤
                      │                              │
                (timer >= CookedDuration)        (Space松开)
                      ▼                              │
                  [Burnt]                            │
                      │                              │
                      ├──────────────────────────────┤
                      │                              │
                (timer >= BurntForcedThrowDuration)  │
                      │                              │
                      └────────────────┬─────────────┘
                                       ▼
                                  选定球道 laneIndex
                                  发布 OnThrowRequested(laneIndex)
                                  BakingState → [Idle]
                                  广播 OnBakingStateChanged(Idle, prev)
```

### 状态转换触发条件汇总

| 当前状态 | 触发条件 | 转换目标 |
|---------|---------|---------|
| Idle | Space按下 + PLAYING + DoughState!=None | Undercooked |
| Undercooked | timer >= UndercookedDuration | Cooked |
| Undercooked | Space松开 | 投掷 → Idle |
| Cooked | timer >= CookedDuration | Burnt |
| Cooked | Space松开 | 投掷 → Idle |
| Burnt | timer >= BurntForcedThrowDuration | 投掷（强制）→ Idle |
| Burnt | Space松开 | 投掷 → Idle |

---

## Data Model

**本系统拥有（Own）：**
- `float _bakingTimer` — 当前烤制时长（秒），Idle 时为 0
- `BakingState _currentBakingState` — 当前烤制状态

**本系统读取（Read）：**
- `BakingConfig`（UndercookedDuration、CookedDuration、BurntForcedThrowDuration）
- `LaneSystem.GetHoveredLaneIndex()` — 触发投掷时读取一次
- `DoughSystem.GetCurrentDoughState()` — 空格按下时检查是否为 None
- `OnGameStateChanged` — PAUSED 时冻结计时器

**本系统对外暴露（公开 API）：**

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `GetBakingState()` | BakingState | UISystem 可轮询（也可订阅事件） |
| `GetBakingTimer()` | float | UISystem 读取以显示烤制进度条 |

**本系统不拥有：**
- 投掷动画 → ThrowSystem
- 球道悬停状态 → LaneSystem
- 面团比例与档位 → DoughSystem
- 随机数种子 → 使用 Unity 内置 Random（无需独立管理）

---

## Config

BakingSystem 使用 `BakingConfig`，无新增字段。已有字段：

| 字段名 | 默认值 | 说明（见 010_ConfigSchema）|
|--------|--------|--------------------------|
| UndercookedDuration | 0.5s | Undercooked → Cooked 的时间阈值 |
| CookedDuration | 1.5s | Cooked → Burnt 的时间阈值 |
| BurntForcedThrowDuration | 2.5s | Burnt → 强制投掷的时间阈值 |

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnBakingStateChanged | newState (BakingState), previousState (BakingState) | BakingState 发生任何变化时（含回到 Idle）|
| OnThrowRequested | laneIndex (int) | 投掷球道已选定，通知 ThrowSystem 执行投掷 |

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后做什么 |
|--------|---------|------------|
| OnGameStateChanged | GameLoop | PAUSED → 冻结计时器（停止 Update 中的 timer 累加）；PLAYING → 恢复 |

---

## Edge Cases

**#1 空格按下后同帧松开（极短点按）**
处理方式：按下时进入 Undercooked（timer = 0），松开时触发投掷。BakingState 为 Undercooked（不熟），投掷结果由 ThrowSystem 根据 DoughState 判定（面包扔出但命中率取决于面团档位）。这是合法的操作，不做特殊处理。

**#2 DoughState = None 时按下空格**
处理方式：在 Update 中检查 `DoughSystem.GetCurrentDoughState() != DoughState.None`，为 None 时 Space 输入静默忽略，不进入 Undercooked。

**#3 游戏暂停时处于 Undercooked / Cooked / Burnt 状态中**
处理方式：收到 OnGameStateChanged(PAUSED) 后，停止 timer 累加（_isPaused 标志位）。空格松开事件在 PAUSED 时忽略。Resume 后从当前 timer 值继续计时，BakingState 不变。

**#4 强制投掷发生时没有悬停球道（GetHoveredLaneIndex() == -1）**
处理方式：`Random.Range(0, 5)` 选定球道，行为与主动松开时的无悬停处理一致。日志输出「BakingSystem: ForcedThrow → RandomLane[x]」供调试。

**#5 OnThrowRequested 发出后，ThrowSystem 尚未完成上一次投掷动画**
理论上不可能：投掷完成前 DoughState = None，BakingSystem 拒绝新的空格输入（Edge Case #2 保护）。防御性处理：若 ThrowSystem 有「忙碌」标志，BakingSystem 发出 Warning 日志，ThrowSystem 自行决定是否队列处理。

**#6 DevSpeedMultiplier 加速时（Ctrl+Shift+P，Time.timeScale > 1）**
BakingSystem 使用 `Time.deltaTime` 累加 timer，TimeScale 提升后 deltaTime 自然增大，烤制时间等比缩短。无需特殊处理，行为符合预期（快进模式）。

---

## Acceptance Criteria

- [ ] Given PLAYING 且 DoughState != None，When 按下空格，Then BakingState = Undercooked，广播 OnBakingStateChanged(Undercooked, Idle)
- [ ] Given DoughState = None，When 按下空格，Then BakingState 保持 Idle，无广播
- [ ] Given Undercooked，When 计时达到 UndercookedDuration（默认0.5s），Then BakingState = Cooked，广播 OnBakingStateChanged(Cooked, Undercooked)
- [ ] Given Cooked，When 计时达到 CookedDuration（默认1.5s），Then BakingState = Burnt，广播 OnBakingStateChanged(Burnt, Cooked)
- [ ] Given Burnt，When 计时达到 BurntForcedThrowDuration（默认2.5s），Then 发布 OnThrowRequested，BakingState = Idle，广播 OnBakingStateChanged(Idle, Burnt)
- [ ] Given Cooked，When 松开空格，Then 立即发布 OnThrowRequested(laneIndex)，BakingState = Idle
- [ ] Given 松开空格时 GetHoveredLaneIndex() = 2，Then OnThrowRequested(2) 发布
- [ ] Given 松开空格时 GetHoveredLaneIndex() = -1，Then OnThrowRequested(0~4 随机值) 发布
- [ ] Given PAUSED 状态，When 持续等待，Then BakingState 不因计时推进而变化
- [ ] Given PAUSED 状态，When 松开空格，Then 无 OnThrowRequested 发布，BakingState 不变
- [ ] Given DevSpeedMultiplier = 3.0 开启，Then 实际烤制时间缩短为 1/3（Undercooked 约0.17s可达到 Cooked）

---

## Test Plan

**Test 1 — 状态时序**
1. PLAYING，DoughState = Medium（默认 InitialRatio = 1.0）
2. 按下空格，观察 Console
3. ✓ t=0: OnBakingStateChanged(Undercooked, Idle)
4. ✓ t≈0.5s: OnBakingStateChanged(Cooked, Undercooked)
5. ✓ t≈1.5s: OnBakingStateChanged(Burnt, Cooked)
6. 不松开空格，继续等待
7. ✓ t≈2.5s: OnThrowRequested 发布，OnBakingStateChanged(Idle, Burnt)

**Test 2 — 松开时机**
1. 按下空格，等到 Cooked 状态（约0.5~1.5s）
2. 松开空格
3. ✓ 立即发布 OnThrowRequested，BakingState = Idle
4. ✓ LaneSystem 的悬停高亮在收到 OnBakingStateChanged(Idle) 后重置

**Test 3 — 无悬停投掷**
1. 将鼠标移到屏幕下半部分（无球道悬停）
2. 按下并松开空格
3. ✓ OnThrowRequested 发布，laneIndex 为 0~4 内某随机值
4. 重复 10 次，✓ 球道分布有随机性，不固定在某一条

**Test 4 — DoughState None 保护**
1. 触发投掷，DoughState 进入 None 窗口
2. 在 None 窗口内按空格
3. ✓ BakingState 保持 Idle，无 OnBakingStateChanged 广播

**Test 5 — 暂停冻结**
1. 按下空格进入 Undercooked
2. 等约 0.3s（仍在 Undercooked）
3. 按 Esc 暂停
4. 等待 2s
5. ✓ BakingState 仍为 Undercooked，Console 无新状态变更广播
6. Resume
7. ✓ 继续计时，约 0.2s 后过渡到 Cooked

**Test 6 — DevSpeed**
1. 开启 Ctrl+Shift+P（DevSpeedMultiplier = 3.0）
2. 按下空格
3. ✓ Cooked 约 0.17s 后到达（0.5/3），Burnt 约 0.5s，强制投掷约 0.83s

---

## Future Extensions

- **连击机制（快速投掷加分）**：BakingSystem 可在 OnThrowRequested 中携带 BakingState 数据，上层系统根据 Cooked 状态给出加分，**BakingSystem 接口已支持（BakingState 可通过事件数据传递）**。
- **多档位烤制（过熟→焦→炭化）**：当前只有3个计时阶段，增加阶段只需在 BakingConfig 中增加字段并在 Update 中增加判断，**结构支持，需小改**。
- **按住空格期间面包缓慢上浮动画**：UISystem 读取 GetBakingTimer() 即可驱动，**BakingSystem 无需修改**。
