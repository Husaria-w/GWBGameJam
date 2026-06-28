# 008 TableSystem Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Draft |
| Last Updated | 2026-06-27 |
| Depends On | 003_MonsterSystem, 010_ConfigSchema, 012_Architecture |
| Required By | 001_GameLoop, 009_UISystem |

---

## Goal

追踪桌子被怪物碰触的次数，在次数达到上限时广播 OnTableDestroyed，触发死亡流程。

TableSystem 是纯计数系统：它不关心怪物类型、面团比例或烤制结果，只记录「被碰了几次」。

---

## Scope

**In Scope：**
- 订阅 OnMonsterReachedTable，每次到达时扣减 HP
- HP 归零时发布 OnTableDestroyed
- 订阅 OnLevelStarted，重置 HP 为 MaxHits
- 对外暴露当前 HP 和最大 HP 供 UISystem 查询

**Out of Scope：**
- 桌子 HP 的 UI 显示 → 009_UISystem
- 怪物是否真的「碰到」桌子的物理判定 → MonsterSystem（已通过事件通知）
- 死亡后的状态流转 → GameLoop（订阅 OnTableDestroyed）

---

## Gameplay Rules

- 桌子有最大承受次数 MaxHits（TableConfig 配置）。
- 每当一只怪物抵达桌子（OnMonsterReachedTable），HP 减 1。
- HP = 0 时，立即发布 OnTableDestroyed，本关结束（死亡）。
- 新关卡开始（OnLevelStarted）时，HP 重置为 MaxHits。

---

## State Machine

```
[Alive]（CurrentHP > 0）
    │
    └─(OnMonsterReachedTable)──► CurrentHP -= 1
                                      │
                          ┌───────────┴───────────┐
                          │ CurrentHP > 0         │ CurrentHP = 0
                          ▼                       ▼
                       [Alive]             发布 OnTableDestroyed
                                           → [Destroyed]

[Destroyed]（本关剩余期间不再响应 OnMonsterReachedTable）

任意状态：
    (OnLevelStarted) ──► CurrentHP = MaxHits → [Alive]
```

---

## Data Model

**本系统拥有（Own）：**
- `int _currentHP` — 当前剩余承受次数

**本系统读取（Read）：**
- `TableConfig.MaxHits`
- `OnMonsterReachedTable` — 来自 MonsterSystem
- `OnLevelStarted` — 来自 GameLoop

**本系统对外暴露（公开 API）：**

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `GetCurrentHP()` | int | UISystem 读取以更新 HP 条 |
| `GetMaxHP()` | int | UISystem 读取以计算 HP 条比例 |

---

## Config

使用已定义的 `TableConfig`，无新增字段。

| 字段名 | 默认值 | 说明 |
|--------|--------|------|
| MaxHits | 5 | 桌子能承受的最大碰触次数 |

---

## Events

### 本系统发布（Publish）

| 事件名 | 携带数据 | 触发时机 |
|--------|---------|---------|
| OnTableDestroyed | —（无） | CurrentHP 降至 0 时 |

### 本系统订阅（Subscribe）

| 事件名 | 来源系统 | 触发后做什么 |
|--------|---------|------------|
| OnMonsterReachedTable | MonsterSystem | CurrentHP -= 1；若归零则发布 OnTableDestroyed |
| OnLevelStarted | GameLoop | CurrentHP = MaxHits |

---

## Edge Cases

**#1 同一帧多只怪物同时抵达桌子（OnMonsterReachedTable 连续触发）**
处理方式：每次事件独立处理，HP 依次扣减。若第一次扣减触发 OnTableDestroyed，后续同帧事件仍会到达，但此时 `_currentHP <= 0`，加入 guard check：`if (_currentHP <= 0) return;`，防止重复发布 OnTableDestroyed。

**#2 OnTableDestroyed 发出后，GameLoop 切换至 DEATH 状态，此时 MonsterSystem 停止移动，不再有新的 OnMonsterReachedTable**
验证：DEATH 状态下怪物计时器已冻结（MonsterSystem 订阅 OnGameStateChanged），不存在新碰桌事件。Edge Case #1 的 guard 仅作额外防御。

**#3 MaxHits 设为 0（策划误配）**
处理方式：TableSystem 在 Awake 中 Validate，若 MaxHits < 1，输出 Error 并强制设为 1。游戏不崩溃。

---

## Acceptance Criteria

- [ ] Given 游戏开始，Then CurrentHP = MaxHits
- [ ] Given CurrentHP = 3，When OnMonsterReachedTable 触发，Then CurrentHP = 2，UISystem HP 条更新
- [ ] Given CurrentHP = 1，When OnMonsterReachedTable 触发，Then CurrentHP = 0，发布 OnTableDestroyed
- [ ] Given OnTableDestroyed 已发布，When 同帧再次收到 OnMonsterReachedTable，Then 不重复发布 OnTableDestroyed
- [ ] Given OnLevelStarted 触发，Then CurrentHP = MaxHits（无论上一关结束时 HP 是多少）
- [ ] Given TableConfig.MaxHits = 0，When 运行游戏，Then Console 输出 Error，MaxHits 强制为 1

---

## Test Plan

**Test 1 — HP 扣减**
1. 将 MaxHits = 3
2. 手动调用 MonsterSystem.OnMonsterReachedTable 三次（或 MonsterSystem 触发）
3. ✓ 第 1 次：CurrentHP = 2；第 2 次：CurrentHP = 1；第 3 次：OnTableDestroyed 广播，GameLoop → DEATH

**Test 2 — 关卡重置**
1. 第一关中怪物碰桌 2 次，CurrentHP = MaxHits - 2
2. 第一关通关（非死亡），进入第二关
3. ✓ OnLevelStarted 触发，CurrentHP = MaxHits（满值）

**Test 3 — 同帧多次碰桌**
1. 将 MaxHits = 2
2. 构造同帧两次 OnMonsterReachedTable（如两只怪物同步移动）
3. ✓ OnTableDestroyed 仅广播一次，不重复

---

## Future Extensions

- **分段 HP（不同怪物类型造成不同伤害）**：当前每次碰桌固定 -1；若要差异化伤害，需在 OnMonsterReachedTable 中携带伤害值，**ConfigSchema 和 MonsterData 需小改**。
- **桌子受伤动画**：UISystem 监听 OnMonsterReachedTable 即可触发，**TableSystem 无需修改**。
