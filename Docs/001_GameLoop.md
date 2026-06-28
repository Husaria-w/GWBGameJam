# 001 GameLoop Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Approved |
| Last Updated | 2026-06-27 |
| Depends On | 000_ProjectVision |
| Required By | 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 012 |

---

## Goal

定义游戏的全局状态机：从启动到退出，游戏经历哪些状态、每个状态如何切换、哪些系统在哪些状态下应该激活或暂停。

所有其他系统依赖 GameLoop 的状态决定自身行为（例如：MonsterSystem 只在 PLAYING 状态下驱动移动）。

---

## Scope

**In Scope（本系统负责）：**
- 全局游戏状态的定义与切换逻辑
- 状态切换时通过事件通知其他系统
- Pause / Resume 行为（TimeScale 控制）
- 当前关卡索引追踪
- 死亡和通关的触发判定（监听事件，自身不计数）
- Dev 工具：Ctrl+Shift+P 全局加速切换

**Out of Scope（不归本系统管）：**
- 怪物生成调度与关卡内计数 → 007_LevelSystem
- 怪物移动 → 003_MonsterSystem
- 桌子 HP 计数 → 008_TableSystem
- 面团揉制 / 烤制 / 投掷逻辑 → 004 / 005 / 006
- 所有 UI 的视觉实现 → 009_UISystem
- 场景 / Prefab / 程序集组织 → 012_Architecture

---

## Gameplay Rules

- 玩家启动游戏，看到主菜单。
- 点击「开始游戏」，进入游戏场景，从第一关开始。
- 游戏中，怪物从上方球道持续向下移动，玩家在下方揉面、烤制、投掷。
- 任何时候按 Esc，游戏暂停，一切停止，显示 Pause 菜单。
- Pause 菜单可选「继续」或「回主菜单」。
- 桌子被怪物碰到 N 次后，游戏结束，显示死亡界面。
- 当前关卡的 X 只怪物全部退场（无论击败或逃跑），本关结束。
- 若非最后一关，进入关卡过渡，下一关开始。
- 若是最后一关（第三关），显示通关界面。
- 死亡界面和通关界面均按任意键回到主菜单。

---

## State Machine

### 顶层状态图

```
              ┌─────────────┐
              │  MAIN_MENU  │◄──────────────────────────────┐
              └──────┬──────┘                               │
                     │ 点击"开始游戏"                        │
                     ▼                                      │
              ┌─────────────┐   桌子HP归零                  │
         ┌───►│   PLAYING   ├──────────────► [DEATH] ───(任意键)
         │    └──────┬──────┘                               │
         │           │                      [VICTORY]──(任意键)
         │      ┌────┴───────┐                   ▲          │
         │   Esc│            │X只退场              │          │
         │      ▼            ▼                   │          │
         │  [PAUSED]  [LEVEL_TRANSITION]         │          │
         │      │            │                   │          │
         │  继续│       过渡结束                  │          │
         └──────┘            │ 若末关             │          │
                             └──────────────────┘          │
                               若非末关:CurrentLevel+1       │
                               ────────────────────────────►│
              PAUSED 点击"回主菜单" ──────────────────────────┘
```

### 状态说明

| 状态 | TimeScale | 怪物移动 | 玩家输入 | 备注 |
|------|-----------|---------|---------|------|
| MAIN_MENU | 1 | 否 | 仅菜单按钮 | — |
| PLAYING | 1（或加速） | 是 | 全部游戏输入 | 游戏主循环 |
| PAUSED | **0** | 否 | 仅 Pause 菜单 | 一切冻结 |
| LEVEL_TRANSITION | 1 | 否 | 任意键（确认继续） | 显示「第X关通过」画面，按任意键跳关 |
| DEATH | 1 | 否 | 任意键 | — |
| VICTORY | 1 | 否 | 任意键 | — |

---

## Data Model

**本系统拥有（Own）：**
- `CurrentState` — 当前顶层状态（枚举：MAIN_MENU / PLAYING / PAUSED / LEVEL_TRANSITION / DEATH / VICTORY）
- `CurrentLevelIndex` — 当前关卡索引，0-based（0 = 第一关）
- `IsDevSpeedActive` — Ctrl+Shift+P 加速开关（bool）

**本系统读取（Read）：**
- 无。GameLoop 通过订阅事件被动响应，不主动轮询其他系统的数据。

**本系统不拥有：**
- 桌子受击次数 → 008_TableSystem 拥有
- 关卡内怪物退场计数 → 007_LevelSystem 拥有
- 任何面团 / 烤制状态 → 004 / 005 拥有

---

## Config（ScriptableObject 字段）

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| TotalLevels | int | 3 | 1 ~ 10 | 游戏总关卡数。修改后需同步配置各关卡的生成参数 |
| DevSpeedMultiplier | float | 3.0 | 1.5 ~ 10.0 | Ctrl+Shift+P 加速倍率。10 以上可能导致碰撞检测失效 |
| LevelTransitionDuration | float | 2.0 | 0.0 ~ 5.0 | 关卡过渡停留秒数。设为 0 则立即跳关（调试用） |

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnGameStateChanged | previousState, newState | 任何顶层状态切换 |
| OnLevelStarted | levelIndex (int) | 进入 PLAYING 且关卡索引已更新时 |
| OnGamePaused | — | 切换至 PAUSED |
| OnGameResumed | — | 从 PAUSED 返回 PLAYING |

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后 GameLoop 做什么 |
|--------|---------|----------------------|
| OnTableDestroyed | 008_TableSystem | 切换至 DEATH 状态 |
| OnLevelCleared | 007_LevelSystem | 若非末关 → LEVEL_TRANSITION → PLAYING（索引+1）；若末关 → VICTORY |

---

## Edge Cases

**#1 关卡过渡界面**
Level 1→2、Level 2→3 之间显示「第 X 关通过！按任意键继续」画面。
玩家按任意键后进入下一关（非自动倒计时）。
`LevelTransitionDuration` 字段保留，设为 0 时跳过等待（供调试使用）。
UI 内容详见 009_UISystem。

**#2 同一帧同时收到 OnTableDestroyed 和 OnLevelCleared**
同帧怪物碰桌且最后一只怪物退场。
处理方式：**优先 DEATH**。失败比胜利的判定优先，避免视觉上"死了还通关"的歧义。

**#3 PLAYING 中按 Esc 时正在进行投掷动画**
允许暂停。TimeScale = 0 冻结动画，Resume 后动画从冻结点继续。
不取消投掷。

**#4 从 PAUSED 点击「回主菜单」**
直接切换至 MAIN_MENU，不触发 DEATH 或 VICTORY，CurrentLevelIndex 重置为 0。

**#5 Ctrl+Shift+P 在非 PLAYING 状态**
无效。仅在 PLAYING 状态下响应此快捷键。
PAUSED 时不响应（避免加速和 TimeScale=0 产生混乱）。

**#6 玩家在 LEVEL_TRANSITION 期间按 Esc**
LEVEL_TRANSITION 期间禁用 Pause 输入，仅接受「任意键继续」。
按 Esc 不触发暂停，视为任意键，直接进入下一关。

---

## Acceptance Criteria

- [ ] Given MAIN_MENU，When 点击「开始游戏」，Then 状态 = PLAYING，CurrentLevelIndex = 0，发布 OnLevelStarted(0)
- [ ] Given PLAYING，When 按 Esc，Then TimeScale = 0，状态 = PAUSED，发布 OnGamePaused
- [ ] Given PAUSED，When 点击「继续」，Then TimeScale = 1，状态 = PLAYING，发布 OnGameResumed
- [ ] Given PAUSED，When 点击「主菜单」，Then 状态 = MAIN_MENU，CurrentLevelIndex = 0
- [ ] Given PLAYING，When 收到 OnTableDestroyed，Then 状态 = DEATH
- [ ] Given DEATH，When 按任意键，Then 状态 = MAIN_MENU
- [ ] Given PLAYING（LevelIndex=0，TotalLevels=3），When 收到 OnLevelCleared，Then 状态 = LEVEL_TRANSITION，显示「第1关通过」画面
- [ ] Given LEVEL_TRANSITION，When 按任意键，Then 状态 = PLAYING，CurrentLevelIndex = 1，发布 OnLevelStarted(1)
- [ ] Given PLAYING（LevelIndex=2，TotalLevels=3），When 收到 OnLevelCleared，Then 状态 = VICTORY
- [ ] Given VICTORY，When 按任意键，Then 状态 = MAIN_MENU
- [ ] Given PLAYING，When Ctrl+Shift+P，Then TimeScale = DevSpeedMultiplier，IsDevSpeedActive = true
- [ ] Given PLAYING + IsDevSpeedActive=true，When 再次 Ctrl+Shift+P，Then TimeScale = 1，IsDevSpeedActive = false
- [ ] Given PAUSED，When Ctrl+Shift+P，Then 无响应（TimeScale 保持 0）
- [ ] Given PLAYING，同帧收到 OnTableDestroyed 和 OnLevelCleared，Then 状态 = DEATH（非 VICTORY）

---

## Test Plan

**前置条件：** GameManager 挂载于场景，所有 Config 已在 Inspector 中赋值。

**Test 1 — 主菜单进入游戏**
1. 运行游戏，确认显示主菜单
2. 点击「开始游戏」
3. ✓ Inspector：CurrentState = PLAYING，CurrentLevelIndex = 0
4. ✓ Console：OnLevelStarted(0) 被广播

**Test 2 — 暂停与恢复**
1. 游戏进行中按 Esc
2. ✓ Inspector：TimeScale = 0，CurrentState = PAUSED
3. 点击「继续」
4. ✓ Inspector：TimeScale = 1，CurrentState = PLAYING

**Test 3 — 死亡流程**
1. 临时设置 TableConfig.MaxHits = 1
2. 放一只怪物到达桌子
3. ✓ 显示 DEATH 界面
4. 按任意键
5. ✓ 回到主菜单，CurrentLevelIndex = 0

**Test 4 — 关卡推进**
1. 临时设置 LevelConfig[0].TotalMonsters = 1，LevelTransitionDuration = 0
2. 让一只怪物退场（击败或逃跑均可）
3. ✓ CurrentLevelIndex = 1，OnLevelStarted(1) 被广播

**Test 5 — 通关**
1. 临时设置 TotalLevels = 1，LevelConfig[0].TotalMonsters = 1
2. 清空第一关
3. ✓ 显示 VICTORY 界面，按任意键回主菜单

**Test 6 — 加速模式**
1. PLAYING 中按 Ctrl+Shift+P
2. ✓ Inspector：TimeScale = DevSpeedMultiplier
3. 再按一次
4. ✓ Inspector：TimeScale = 1

**Test 7 — PAUSED 中加速无效**
1. 先暂停，再按 Ctrl+Shift+P
2. ✓ TimeScale 保持 0

---

## Future Extensions

- **关卡选择 / 解锁**：当前无关卡解锁状态，若要加需在 GameLoop 引入存档系统和新状态。当前设计**不支持**，需改动。
- **无限生存模式**：TotalLevels 可配置，但无「无限」逻辑，需在 LevelSystem 扩展，GameLoop 层改动**极小**。
- **成绩 / 分数系统**：GameLoop 发布的事件（OnLevelCleared、OnTableDestroyed）足够让独立 ScoreSystem 监听，**支持扩展**，GameLoop 层无需修改。
- **过场动画**：LEVEL_TRANSITION 状态已预留 Duration 配置，插入动画**只需修改该状态的 UI 内容**，状态机结构不变。
- **多人模式**：当前所有状态为单例，多人需要完全重构，**不支持**。
