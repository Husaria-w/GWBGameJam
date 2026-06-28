# 007 LevelSystem Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Draft |
| Last Updated | 2026-06-27 |
| Depends On | 003_MonsterSystem, 010_ConfigSchema, 012_Architecture |
| Required By | 001_GameLoop, 009_UISystem |

---

## Goal

按关卡配置控制怪物的生成节奏，统计本关退场数量，在所有怪物退场后广播 OnLevelCleared。

LevelSystem 是关卡进度的裁判：它不关心面团比例或投掷命中，只问「这一关该出多少怪、出了多少、走了多少」。

---

## Scope

**In Scope：**
- 订阅 OnLevelStarted，加载对应关卡数据并重置计时器与计数器
- 按 SpawnIntervalSeconds 定时，选取随机空置球道与随机怪物类型，调用 MonsterSystem.SpawnMonster()
- 统计退场数量（OnMonsterDefeated + OnMonsterReachedTable 各计一次）
- 退场数 = TotalMonsters 时发布 OnLevelCleared
- 全满跳过：当前球道全满时，跳过本次生成并重置计时器（等待下一个完整 SpawnIntervalSeconds）
- 停止条件：已生成数 ≥ TotalMonsters 时停止计时，不再尝试生成

**Out of Scope：**
- 关卡切换的状态流转 → GameLoop（订阅 OnLevelCleared）
- 面团与桌子的重置 → DoughSystem / TableSystem（各自订阅 OnLevelStarted）
- 怪物的具体移动与生成逻辑 → MonsterSystem
- UI 进度条（当前已退场 / 总数）→ 009_UISystem

---

## Gameplay Rules

- 收到 OnLevelStarted(levelIndex) 后，加载 `LevelConfig.Levels[levelIndex]`，计时器归零。
- 第一只怪物在第一个 SpawnIntervalSeconds **结束后**生成（不在 t=0 立即生成）。
- 每次计时到达 SpawnIntervalSeconds 时：
  1. 若已生成数 ≥ TotalMonsters：不生成，停止计时。
  2. 若 5 条球道全满：跳过本次，**重置计时器**（等待下一个完整间隔）。
  3. 否则：从空置球道中随机选一条，从可用怪物类型中随机选一种，调用 `MonsterSystem.SpawnMonster(laneIndex, data)`，已生成数 +1，计时器重置。
- 每当 OnMonsterDefeated 或 OnMonsterReachedTable 触发，退场数 +1。
- 退场数 == TotalMonsters 时，发布 OnLevelCleared。
- 游戏暂停（OnGameStateChanged → PAUSED）时，冻结计时器。

---

## State Machine

```
[Inactive]（非 PLAYING 状态）
    │
    └─(OnLevelStarted(levelIndex))──► 加载 LevelData[levelIndex]
                                       _spawnTimer = 0
                                       _spawnedCount = 0
                                       _exitedCount = 0
                                       → [WaitingToSpawn]

[WaitingToSpawn]（计时器累加中，等待下次生成机会）
    │
    ├─(_spawnTimer >= SpawnIntervalSeconds)
    │      │
    │      ├─(spawnedCount >= TotalMonsters)──► 停止计时 → [WaitingForAllExit]
    │      │
    │      ├─(所有球道全满)────────────────────► 重置 _spawnTimer → [WaitingToSpawn]
    │      │
    │      └─(有空置球道)─────────────────────► 随机选道/类型
    │                                            SpawnMonster()
    │                                            _spawnedCount += 1
    │                                            重置 _spawnTimer
    │                                            → [WaitingToSpawn]
    │
    ├─(OnMonsterDefeated / OnMonsterReachedTable)──► _exitedCount += 1
    │                                                 检查是否 == TotalMonsters（见下）
    │
    └─(OnGameStateChanged PAUSED)──► 冻结计时器 → [Paused]

[WaitingForAllExit]（所有怪物已生成，等待最后几只退场）
    │
    ├─(OnMonsterDefeated / OnMonsterReachedTable)──► _exitedCount += 1
    │                                                 若 == TotalMonsters
    │                                                 → 发布 OnLevelCleared → [Inactive]
    │
    └─(OnGameStateChanged PAUSED)──► 冻结计时器 → [Paused]

[Paused]
    └─(OnGameStateChanged PLAYING)──► 恢复计时器 → 回到上一状态
```

---

## Data Model

**本系统拥有（Own）：**
- `int _currentLevelIndex` — 当前关卡（0~TotalLevels-1）
- `int _spawnedCount` — 本关已生成怪物数
- `int _exitedCount` — 本关已退场怪物数（击败 + 逃跑）
- `float _spawnTimer` — 距上次生成已过去的时间（秒）

**本系统读取（Read）：**
- `LevelConfig.Levels[levelIndex]`（SpawnIntervalSeconds、TotalMonsters）
- `MonsterSystem.IsLaneOccupied(i)`（0~4，判断全满）
- `[SerializeField] MonsterData[] _availableMonsterTypes`（Inspector 配置，全局怪物类型池）

**本系统对外暴露（公开 API）：**

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `GetExitedCount()` | int | UISystem 读取已退场数 |
| `GetTotalMonsters()` | int | UISystem 读取本关总数 |
| `GetCurrentLevelIndex()` | int | UISystem 读取当前关卡号（用于显示「第 X 关」）|

**本系统不拥有：**
- 怪物实例 → MonsterSystem
- 关卡状态（PLAYING / LEVEL_TRANSITION）→ GameLoop
- 桌子 HP → TableSystem

---

## Config

使用已定义的 `LevelConfig`，无新增字段。

| 字段名 | 默认值（Lv1/Lv2/Lv3） | 说明 |
|--------|----------------------|------|
| SpawnIntervalSeconds | 9.0 / 7.0 / 5.0 | 相邻两次生成的间隔（秒）|
| TotalMonsters | 10 / 15 / 20 | 本关总出怪数（退场满此数后通关）|

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnLevelCleared | — | 本关退场数达到 TotalMonsters |

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后做什么 |
|--------|---------|------------|
| OnLevelStarted | GameLoop | 加载关卡数据，重置计数器与计时器 |
| OnMonsterDefeated | MonsterSystem | _exitedCount += 1，检查通关条件 |
| OnMonsterReachedTable | MonsterSystem | _exitedCount += 1，检查通关条件 |
| OnGameStateChanged | GameLoop | PAUSED → 冻结计时器；PLAYING → 恢复 |

---

## Edge Cases

**#1 所有球道全满时计时器重置（Q2 用户确认方案 B）**
`_spawnTimer = 0`，等待下一个完整 SpawnIntervalSeconds。
效果：全满时生成节奏「暂停」，不会因全满而积累「欠账」生成。

**#2 OnMonsterDefeated 和 OnMonsterReachedTable 同帧同球道触发**
理论上不可能（怪物状态互斥，MonsterSystem 保证不重复广播）。若防御性检测，_exitedCount 仍是整数加法，不影响结果，只要不超过 TotalMonsters 即可。加 guard：`if (_exitedCount >= TotalMonsters) return;`。

**#3 最后一只怪物退场，同帧 OnTableDestroyed 也触发（GameLoop 001 Edge Case）**
GameLoop 以 OnTableDestroyed 优先（→ DEATH），忽略同帧的 OnLevelCleared。LevelSystem 本身仍发布 OnLevelCleared，但 GameLoop 不响应（已在 DEATH 流程中）。LevelSystem 无需感知此情况。

**#4 LevelConfig.Levels 数组长度不足（TotalLevels = 3 但只有 2 个元素）**
由 GameLoop 初始化检查覆盖（008_TableSystem Edge Case 对应逻辑在 LevelSystem 中）：
处理方式：OnLevelStarted 中检查 levelIndex < Levels.Length，越界则 Warning 并使用最后一个元素，游戏不崩溃。

**#5 _availableMonsterTypes 数组为空（Inspector 未配置）**
处理方式：Awake 中检查数组长度 > 0，若为空输出 Error 并阻止进入 PLAYING 状态。

**#6 SpawnMonster() 在有空置球道时仍返回 false（MonsterSystem 内部异常）**
处理方式：输出 Warning 日志，视同「本次生成失败」，_spawnedCount 不递增，计时器重置（等待下次）。不崩溃，不阻塞关卡进度。

---

## Acceptance Criteria

- [ ] Given OnLevelStarted(0)，Then _spawnedCount = 0，_exitedCount = 0，_spawnTimer = 0
- [ ] Given SpawnIntervalSeconds = 9.0，When 等待 9s，Then 第一只怪物生成（不在 t=0 生成）
- [ ] Given 5条球道全满，When 计时到达，Then 不生成怪物，_spawnTimer 重置为 0（等待下一个完整间隔）
- [ ] Given spawnedCount = TotalMonsters，When 计时到达，Then 不生成怪物，计时器停止
- [ ] Given OnMonsterDefeated 触发，Then _exitedCount += 1
- [ ] Given OnMonsterReachedTable 触发，Then _exitedCount += 1
- [ ] Given _exitedCount 达到 TotalMonsters，Then 发布 OnLevelCleared
- [ ] Given 游戏暂停，Then _spawnTimer 不再累加；Resume 后从当前计时值继续
- [ ] Given LevelData[0].TotalMonsters = 10，When 10只怪物全部退场（击败+逃跑混合），Then OnLevelCleared 发布一次

---

## Test Plan

**Test 1 — 第一只怪物的生成时机**
1. 运行游戏，进入第一关（SpawnIntervalSeconds = 9.0）
2. ✓ t=0：无怪物生成
3. ✓ t≈9.0s：Lane 上出现第一只怪物，Console 广播 OnMonsterSpawned

**Test 2 — 全满跳过（方案 B）**
1. 手动填满 5 条球道（调低 SpawnIntervalSeconds）
2. 等待计时到达
3. ✓ 无怪物生成，_spawnTimer = 0，等待下一个完整间隔
4. 手动销毁一只怪物
5. ✓ 下一个间隔后正常生成

**Test 3 — 通关计数**
1. TotalMonsters = 5（测试用）
2. 让 3 只被击败，2 只逃跑
3. ✓ 第 5 只退场时发布 OnLevelCleared，Console 可见

**Test 4 — 暂停冻结**
1. 生成计时器进行到 4s（SpawnIntervalSeconds = 9.0）
2. 暂停
3. 等待 5s
4. ✓ Resume 后计时器从 4s 继续，不是 9s

**Test 5 — 关卡重置**
1. 第一关进行中，_spawnedCount = 5，_exitedCount = 3
2. GameLoop → OnLevelStarted(1)（进入第二关）
3. ✓ _spawnedCount = 0，_exitedCount = 0，SpawnIntervalSeconds 更新为 7.0

---

## Future Extensions

- **关卡内出怪类型组合控制（如第 3 关只出 B/C 类怪物）**：将 `_availableMonsterTypes` 移入 LevelData，按关卡配置不同的怪物池，**LevelSystem 结构支持，ConfigSchema 需小改**。
- **波次系统（第 1 波出 5 只，第 2 波等第 1 波全清后再出）**：当前单一连续生成节奏；增加波次需在 LevelSystem 引入 WaveData 层，**需中等改动**。
- **加速生成（关卡内随时间缩短间隔）**：当前 SpawnIntervalSeconds 全关固定；若要线性缩短，需在 Update 中动态计算，**LevelSystem 需小改**。
