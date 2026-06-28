# 003 MonsterSystem Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Draft |
| Last Updated | 2026-06-27 |
| Depends On | 002_LaneSystem, 010_ConfigSchema, 012_Architecture |
| Required By | 006_ThrowSystem, 007_LevelSystem, 008_TableSystem, 009_UISystem |

---

## Goal

管理怪物的完整生命周期：生成、逐步移动（含平滑动画）、透视缩放、被击败、抵达桌子。

MonsterSystem 是关卡内所有威胁的来源，其他系统（ThrowSystem、TableSystem、LevelSystem）通过本系统的公开 API 和事件与怪物交互。

---

## Scope

**In Scope：**
- 怪物实例的生成与销毁
- 每步移动的计时、平滑位移动画、透视缩放插值
- 「待移动」状态的检测与暴露
- 同一球道最多一只怪物的约束
- 被击败时的销毁逻辑
- 受到错误攻击时的「闪白」视觉反馈
- 抵达桌子时触发事件并销毁

**Out of Scope：**
- 决定何时生成怪物（间隔、总数）→ 007_LevelSystem
- 命中判定与面团比例匹配 → 006_ThrowSystem
- 桌子 HP 计数 → 008_TableSystem
- 点位坐标数据 → 002_LaneSystem（GetWaypoint API）
- 爆炸特效的显示 → 006_ThrowSystem / 009_UISystem

---

## Gameplay Rules

- 怪物在球道顶端（posIndex = 0）生成，每隔固定时间向桌子方向移动一步。
- 移动过程为平滑位移动画，动画结束后停在新点位。
- 怪物的 Sprite 大小随点位序号增大，模拟透视近大远小效果。
- 同一球道同时只能存在一只怪物；该球道有怪物时，跳过该球道的生成请求。
- 怪物抵达最后一个点位（posIndex = MoveStepCount - 1）后，再次移动即碰到桌子，怪物消失，桌子受到一次攻击。
- 被正确面团命中的怪物立即消失，播放爆炸效果。
- 被错误面团命中的怪物不消失，快速闪白两次，继续移动。

---

## State Machine

### 单只怪物状态

```
[Spawned] ──(Awake初始化完成)──► [Idle] at posIndex=0
                                      │
                        ┌─────────────┤
                        │             │
               (距下次移动时间          │
               ≤ PendingMoveThreshold) │
                        ▼             │
                  [PendingMove]        │ (计时器归零)
                        │             │
                        └──────┬──────┘
                               ▼
                           [Moving]
                    (平滑位移至下一点位，
                     历时 MoveDuration 秒)
                               │
                    ┌──────────┴───────────┐
                    │ posIndex < Max-1     │ posIndex = Max-1
                    ▼                     ▼
                 [Idle]            [ReachedTable]
              (posIndex+1)         广播OnMonsterReachedTable
                                   → 销毁

任意 [Idle] 或 [PendingMove] 状态下：
    ← DefeatMonster() 调用 ──► [Defeated] → 销毁（播放爆炸）
    ← TriggerWrongHitFeedback() ──► 闪白动画，状态不变
```

### 移动计时周期

```
|◄────── MoveIntervalSeconds ──────►|
|◄── 等待 ──►|◄── MoveDuration ──►|
             ↑                     ↑
          开始移动              移动结束
          (触发动画)            (更新posIndex)

PendingMove 区间：
|◄────────────────────────────────►|
                      |◄─ PendingMoveThreshold ─►|
                      ↑                          ↑
                进入PendingMove               开始移动
```

---

## Data Model

**本系统拥有（Own）：**
- 5条球道的当前怪物实例（`Monster[5]`，null 表示空置）
- 每只怪物的：posIndex、移动计时器、当前状态、MonsterData 引用

**本系统读取（Read）：**
- `LaneSystem.GetWaypoint(laneIndex, posIndex)` — 世界坐标
- `MonsterConfig`（MoveIntervalSeconds、MoveDuration、MoveStepCount、PendingMoveThreshold、ScaleCurve）
- `OnGameStateChanged` — PAUSED 时暂停计时器与动画

**本系统对外暴露（公开 API）：**

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `SpawnMonster(int laneIndex, MonsterData data)` | bool | 在指定球道生成怪物。球道已有怪物时返回 false |
| `GetMonsterInLane(int laneIndex)` | Monster / null | 返回该球道的怪物实例，无则返回 null |
| `IsLaneOccupied(int laneIndex)` | bool | 该球道是否有怪物 |
| `DefeatMonster(int laneIndex)` | void | 立即销毁该球道怪物，广播 OnMonsterDefeated |
| `TriggerWrongHitFeedback(int laneIndex)` | void | 触发闪白动画，不影响怪物状态 |
| `GetTargetPosition(int laneIndex)` | Vector2 | 返回投掷落点：PendingMove 状态返回下一点位，否则返回当前点位（见 Edge Case #3） |

**本系统不拥有：**
- 关卡内怪物退场计数 → LevelSystem
- 桌子 HP → TableSystem
- 投掷命中判定 → ThrowSystem

---

## Config

> 以下字段新增至 `010_ConfigSchema` 中的 `MonsterConfig`（对 ConfigSchema 的追加更新）。

| 字段名 | 类型 | 默认值 | 合法范围 | 策划说明 |
|--------|------|--------|---------|---------|
| MoveDuration | float | 0.3 | 0.05 ~ MoveIntervalSeconds-0.1 | 每步移动的动画时长（秒）。不得大于 MoveIntervalSeconds，否则动画未完成就开始下一步 |
| WrongHitFlashCount | int | 2 | 1 ~ 5 | 受到错误攻击时闪白次数 |
| WrongHitFlashDuration | float | 0.1 | 0.05 ~ 0.5 | 每次闪白的持续时长（秒）|

*已在 ConfigSchema 中定义的字段（MoveIntervalSeconds、MoveStepCount、PendingMoveThreshold、ScaleCurve）此处不重复列出。*

---

## GameObject 结构

```
_World/MonsterContainer/
└── Monster_[InstanceID]（运行时动态生成）
    ├── Visual                  ← SpriteRenderer，受 Scale 控制
    └── （无 Collider，怪物不需要碰撞体）
```

**Monster Prefab 结构（来自 Prefabs/Monsters/）：**

每种怪物类型对应一个 Prefab，Prefab 上挂载 `MonsterController` 脚本并持有对应 `MonsterData` 的引用。

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnMonsterSpawned | laneIndex (int), monsterData (MonsterData) | 怪物成功生成并完成初始化 |
| OnMonsterDefeated | laneIndex (int) | 怪物被正确击败并销毁时 |
| OnMonsterReachedTable | laneIndex (int) | 怪物移动至 MoveStepCount 位置（碰桌）时 |

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后做什么 |
|--------|---------|------------|
| OnGameStateChanged | GameLoop | PAUSED → 暂停所有怪物计时器与动画；PLAYING → 恢复 |

---

## 缩放系统

- 怪物的 Visual 节点 Scale 由 `MonsterConfig.ScaleCurve` 决定
- X 轴：posIndex（0 = 最远，MoveStepCount-1 = 最近）
- Y 轴：Scale 倍率
- **静止时**：Scale = `ScaleCurve.Evaluate(posIndex)`
- **移动动画期间**：Scale 在每帧由 `ScaleCurve.Evaluate(lerp(currentPos, nextPos, t))` 插值，t = 动画进度（0→1）
- Scale 只作用于 Visual 子节点（与 LaneSystem 的 Visual/Collider 分离设计一致）

---

## Edge Cases

**#1 生成请求指向已有怪物的球道**
处理方式：`SpawnMonster()` 返回 false，不生成。LevelSystem 需在下次计时周期重试（或跳过本次）。详见 007_LevelSystem。

**#2 5条球道全满时的生成请求**
处理方式：LevelSystem 在调用 `SpawnMonster()` 前通过 `IsLaneOccupied()` 检查；若全满则跳过本次生成，等待下次计时周期，**不计入退场总数**。

**#3 怪物处于 posIndex = MoveStepCount-1 的 PendingMove 状态时被 GetTargetPosition 查询**
此时「下一位置」实际上是桌子（超出数组范围）。
处理方式：`GetTargetPosition()` 检查 `posIndex == MoveStepCount-1`，若是则返回**当前点位**（不预判）。怪物很快就会碰桌消失，面包落在当前位置仍是有效命中。

**#4 怪物正在 Moving 动画期间被 DefeatMonster() 调用**
处理方式：立即停止动画，广播 OnMonsterDefeated，销毁 GameObject。不等待动画完成。

**#5 怪物正在 Moving 动画期间被 TriggerWrongHitFeedback() 调用**
处理方式：闪白动画与移动动画并行播放（不互相阻塞）。

**#6 游戏暂停时怪物处于 Moving 动画中途**
处理方式：接收 OnGameStateChanged(PAUSED)，暂停 Unity 动画（TimeScale = 0 自动处理），计时器也随 TimeScale 暂停。Resume 后从断点继续，无需额外处理。

**#7 同一球道在同一帧同时有 DefeatMonster 和 OnMonsterReachedTable**
理论上不可能（怪物状态互斥），但防御性处理：
以先到达的调用为准，后续调用对已销毁的怪物不产生效果（null check 保护）。

---

## Acceptance Criteria

- [ ] Given 球道为空，When LevelSystem 调用 SpawnMonster(2, MonsterData_A)，Then Lane_2 出现怪物，广播 OnMonsterSpawned(2, MonsterData_A)
- [ ] Given 球道已有怪物，When SpawnMonster(2, data) 再次被调用，Then 返回 false，无新怪物生成
- [ ] Given 怪物在 posIndex=0，When 等待 MoveIntervalSeconds，Then 怪物平滑移动至 posIndex=1，动画历时 MoveDuration 秒
- [ ] Given 怪物移动中，When 在 Game 视图观察，Then Sprite 大小在移动过程中连续插值变化
- [ ] Given 怪物静止于 posIndex=3，Then Visual Scale = ScaleCurve.Evaluate(3)
- [ ] Given 计时器剩余 ≤ PendingMoveThreshold，Then GetTargetPosition() 返回 posIndex+1 的坐标
- [ ] Given 计时器剩余 > PendingMoveThreshold，Then GetTargetPosition() 返回 posIndex 的坐标
- [ ] Given 怪物在 posIndex=MoveStepCount-1 且 PendingMove，Then GetTargetPosition() 返回当前点位（不越界）
- [ ] Given 怪物在任意位置，When DefeatMonster() 被调用，Then 怪物立即消失，广播 OnMonsterDefeated
- [ ] Given 怪物存活，When TriggerWrongHitFeedback() 被调用，Then 怪物闪白 WrongHitFlashCount 次，每次持续 WrongHitFlashDuration 秒，怪物不消失
- [ ] Given 怪物在 posIndex=MoveStepCount-1，When 计时器归零，Then 怪物消失，广播 OnMonsterReachedTable，不广播 OnMonsterDefeated
- [ ] Given 游戏暂停，When TimeScale=0，Then 所有怪物计时器与动画冻结；Resume 后从断点继续

---

## Test Plan

**Test 1 — 生成与单道限制**
1. 调用 SpawnMonster(2, MonsterData_A)
2. ✓ Lane_2 出现怪物 Sprite，Console 广播 OnMonsterSpawned
3. 再次调用 SpawnMonster(2, MonsterData_B)
4. ✓ 返回 false，Lane_2 怪物数量仍为 1

**Test 2 — 移动动画与缩放**
1. 生成一只怪物
2. ✓ 等待 MoveIntervalSeconds，观察怪物平滑移动
3. ✓ 移动过程中 Sprite 大小连续变化
4. ✓ 停止后 Scale 与 ScaleCurve 对应点位值一致（Inspector 验证）

**Test 3 — 待移动状态**
1. 在 Inspector 中将 PendingMoveThreshold 设为 0.8s
2. 观察 MoveIntervalSeconds 剩余 ≤ 0.8s 时 MonsterController 的状态字段
3. ✓ isPendingMove = true
4. ✓ GetTargetPosition() 返回下一点位坐标

**Test 4 — 碰桌**
1. 临时将 MoveStepCount 设为 2（快速到达桌子）
2. ✓ 怪物移动两步后消失，广播 OnMonsterReachedTable，不广播 OnMonsterDefeated

**Test 5 — 击败与闪白**
1. 调用 DefeatMonster(laneIndex)
2. ✓ 怪物立即消失，广播 OnMonsterDefeated
3. 另生成一只怪物，调用 TriggerWrongHitFeedback(laneIndex)
4. ✓ 怪物闪白两次后恢复正常，继续移动

**Test 6 — 暂停恢复**
1. 怪物移动到一半时按 Esc
2. ✓ 动画冻结在当前位置
3. Resume
4. ✓ 从断点继续完成移动动画

---

## Future Extensions

- **怪物种类增加**：新增 MonsterData_D.asset 并添加到 LevelSystem 的生成池，**MonsterSystem 无需修改**。
- **怪物移动路径变化（非直线）**：当前路径由 LaneWaypointConfig 决定，修改点位数据即可改变路径，**MonsterSystem 无需修改**。
- **移动速度随关卡提升**：当前 MoveIntervalSeconds 为全局值，若要按关卡设置不同速度，需将其移至 LevelData，并在关卡开始时注入 MonsterSystem，**需小改**。
- **怪物攻击动画（碰桌前播放）**：可在 ReachedTable 状态前插入一个 Attacking 状态，**状态机结构支持，需少量修改**。
