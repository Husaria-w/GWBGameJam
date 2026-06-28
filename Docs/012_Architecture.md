# 012 Architecture Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Approved |
| Last Updated | 2026-06-27 |
| Depends On | 001_GameLoop |
| Required By | 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 013, 014 |

---

## Goal

在任何系统开始编码之前，确立整个项目共用的技术约束：事件系统、系统间通信、场景与 Prefab 组织、命名规范、程序集划分、禁止模式。

所有后续 Spec 的实现方案必须在本文档定义的约束内进行。

---

## Scope

**In Scope（本文档决定）：**
- EventBus 设计与使用规范
- 场景结构与 GameObject 层级
- Folder / Prefab / ScriptableObject 组织
- 命名规范（Namespace、类、事件、资产）
- 程序集划分（Assembly Definition）
- 系统初始化顺序
- 数据所有权规则
- 禁止模式清单

**Out of Scope（各自 Spec 决定）：**
- 每个系统的内部逻辑 → 各 System Spec
- 具体 Config 字段 → 010_ConfigSchema
- 编码细则（命名具体格式、注释规范）→ 013_CodingRules
- 每次 Task 完成的验收流程 → 014_ReviewChecklist

---

## Architecture Principles
> 本节替代 Gameplay Rules，定义架构层的行为准则。

1. **系统不感知其他系统的存在。** 系统只通过 EventBus 广播事件，或通过公开 API 接受外部调用。任何系统都不持有另一个系统的私有引用。

2. **数据只有一个拥有者。** 每份数据只有一个系统负责读写（Owner）。其他系统只能读取，且只能通过公开属性或事件获取。

3. **引用在 Inspector 注入，不在运行时查找。** 系统间的依赖关系在编辑时通过 SerializeField 设置，禁止运行时的 Find 系列方法。

4. **EventBus 订阅必须成对出现。** 每一个 Subscribe 调用，必须在对应的生命周期方法中有对应的 Unsubscribe。

5. **Update 只做轮询必要的事。** 能用事件驱动的逻辑，不写进 Update。Update 的存在需要在 ReviewChecklist 中有意识地验证。

---

## System Boot Sequence
> 本节替代 State Machine，描述系统初始化顺序。

```
Application Start
│
├── Awake()（所有系统并行）
│   ├── 各 System 初始化内部数据（不依赖其他系统）
│   └── 各 System 缓存所需组件引用（不跨系统）
│
├── Start()（所有系统并行，Awake 全部完成后）
│   ├── 各 System 通过 SerializeField 引用已就绪
│   ├── 各 System 向 EventBus 订阅所需事件（OnEnable 也可）
│   └── GameLoop 最后执行：广播初始状态 MAIN_MENU
│
└── 游戏运行
    └── 系统只通过 EventBus 和公开 API 通信
```

**重要：** GameLoop 的初始化必须在其他所有系统的 Start 之后执行。
通过 Unity Script Execution Order 设置 GameLoop 的执行顺序最低（数字最大），确保其他系统订阅完成后再广播初始状态。

---

## Data Model：数据所有权表

| 数据 | Owner 系统 | 其他系统获取方式 |
|------|-----------|----------------|
| 当前游戏状态、关卡索引 | GameLoop | 订阅 `OnGameStateChanged` / `OnLevelStarted` |
| 40 个球道点位坐标 | LaneSystem | 调用 `LaneSystem.GetWaypoint(laneIndex, posIndex)` |
| 怪物实例列表、每道当前怪物 | MonsterSystem | 调用 `MonsterSystem.GetMonsterInLane(laneIndex)` |
| 怪物移动状态（待移动/移动中） | MonsterSystem | 订阅事件 / 调用公开属性 |
| 面团比例值、当前档位 | DoughSystem | 订阅 `OnDoughStateChanged` |
| 烤制状态、当前烤制时间 | BakingSystem | 订阅 `OnBakingStateChanged` |
| 桌子当前 HP 计数 | TableSystem | 订阅 `OnTableHit` / `OnTableDestroyed` |
| 当前关卡怪物退场计数 | LevelSystem | 订阅 `OnLevelCleared` |

**规则：** 任何系统需要其他系统的数据，只能通过上表中"其他系统获取方式"列的方法。不得直接访问他人的私有字段。

---

## EventBus 规范

### 设计

```
EventBus<T>（静态泛型类，T 为 readonly struct）
├── Subscribe(Action<T> handler)
├── Unsubscribe(Action<T> handler)
└── Publish(T eventData)
```

- `EventBus<T>` 是纯静态类，无实例，无 Singleton
- T 必须是 `readonly struct`（值类型，避免堆分配）
- Publish 时若某 handler 抛出异常，不中断其他 handler 的执行

### Event Struct 定义位置

```
Assets/Scripts/Events/
├── GameLoopEvents.cs       ← OnGameStateChanged, OnLevelStarted, OnGamePaused, OnGameResumed
├── MonsterEvents.cs        ← OnMonsterSpawned, OnMonsterDefeated, OnMonsterReachedTable
├── DoughEvents.cs          ← OnDoughStateChanged, OnDoughCleared
├── BakingEvents.cs         ← OnBakingStateChanged, OnBreadLaunched
├── ThrowEvents.cs          ← OnThrowResolved
├── LevelEvents.cs          ← OnLevelCleared
└── TableEvents.cs          ← OnTableHit, OnTableDestroyed
```

### Event Struct 命名规则

- 格式：`On[名词][动词]`（与 GameLoop Spec 已定义的一致）
- 每个 struct 携带该事件所需的最小数据
- 示例结构（概念，非代码阶段）：
  ```
  OnMonsterReachedTable { laneIndex: int }
  OnThrowResolved { laneIndex: int, result: ThrowResult }
  OnDoughStateChanged { previousState: DoughState, newState: DoughState, ratio: float }
  ```

### 订阅生命周期规则

| 订阅位置 | 取消订阅位置 |
|---------|-----------|
| `OnEnable` | `OnDisable` |
| `Awake` / `Start` | `OnDestroy` |

**禁止：** 在 Update 内动态订阅/取消订阅。

---

## 场景结构

### 两个场景

```
Assets/Scenes/
├── MainMenu.unity
└── Game.unity
```

- MainMenu：仅包含 UI，无游戏系统
- Game：包含全部游戏系统和 UI Canvas

### Game.unity GameObject 层级

```
Game.unity
├── _Bootstrap              ← GameLoop MonoBehaviour，Script Execution Order 最低
├── _Systems
│   ├── LaneSystem
│   ├── MonsterSystem
│   ├── DoughSystem
│   ├── BakingSystem
│   ├── ThrowSystem
│   ├── LevelSystem
│   └── TableSystem
├── _UI
│   ├── HUD                 ← 比例条、HP 条、常驻信息
│   ├── PauseMenu           ← 默认隐藏
│   ├── DeathScreen         ← 默认隐藏
│   ├── VictoryScreen       ← 默认隐藏
│   └── LevelTransition     ← 默认隐藏
└── _World
    ├── Lanes               ← 5 条球道静态对象
    ├── MonsterContainer    ← 怪物运行时生成在此节点下
    ├── ProjectileContainer ← 面包运行时生成在此节点下
    └── Table               ← 桌子背景 + 面团显示节点
```

**命名规则：** 以 `_` 开头的节点为纯组织节点，不挂载逻辑组件。

---

## Folder 结构

```
Assets/
├── Scenes/
│   ├── MainMenu.unity
│   └── Game.unity
├── Scripts/
│   ├── GameLoop/
│   ├── Lane/
│   ├── Monster/
│   ├── Dough/
│   ├── Baking/
│   ├── Throw/
│   ├── Level/
│   ├── Table/
│   ├── UI/
│   ├── Events/             ← 所有 Event struct 定义
│   ├── Config/             ← 所有 ScriptableObject 类定义
│   ├── Core/               ← EventBus、共用枚举、共用工具类
│   └── Editor/             ← Lane 计算器等 Editor-only 工具
├── Prefabs/
│   ├── Monsters/
│   │   ├── Monster_A.prefab
│   │   ├── Monster_B.prefab
│   │   └── Monster_C.prefab
│   ├── Projectiles/
│   │   └── Bread.prefab
│   └── UI/
│       └── （各菜单 Prefab）
├── ScriptableObjects/
│   ├── Configs/            ← XxxConfig.asset 实例
│   └── MonsterData/        ← MonsterData_A/B/C.asset
├── Art/
│   ├── Sprites/
│   │   ├── Monsters/
│   │   ├── Lanes/
│   │   ├── Bread/
│   │   ├── Table/
│   │   └── UI/
│   └── Animations/
├── Audio/
│   ├── SFX/
│   └── BGM/
└── Fonts/
```

---

## 命名规范

### Namespace
- 所有运行时代码：`namespace GWBGameJam`
- Editor 工具：`namespace GWBGameJam.Editor`
- 不使用子命名空间（减少 game jam 书写负担）

### 类与文件
| 类型 | 格式 | 示例 |
|------|------|------|
| MonoBehaviour 系统 | `[System]Manager` | `MonsterManager` |
| ScriptableObject 配置 | `[System]Config` | `MonsterConfig` |
| ScriptableObject 数据 | `[Name]Data` | `MonsterData` |
| 纯数据类/结构 | PascalCase | `WaypointData` |
| 枚举 | PascalCase | `DoughState`, `BakingState` |
| Event Struct | `On[名词][动词]` | `OnTableDestroyed` |
| Interface | `I[能力]` | `IDamageable` |
| Editor 工具 | `[Name]EditorWindow` | `LaneCalculatorEditorWindow` |

### 资产文件
| 资产类型 | 格式 | 示例 |
|---------|------|------|
| Prefab | `[Name].prefab` | `Monster_A.prefab`, `Bread.prefab` |
| Config SO | `[System]Config.asset` | `MonsterConfig.asset` |
| 数据 SO | `[Name]Data.asset` | `MonsterData_A.asset` |
| Sprite | `[Category]_[Name]` | `Monster_A_Idle`, `Bread_Cooked` |
| Scene | PascalCase | `MainMenu.unity`, `Game.unity` |

---

## 程序集（Assembly Definition）

```
Assets/Scripts/GWBGameJam.Runtime.asmdef
    └── 覆盖 Scripts/ 下所有非 Editor 代码

Assets/Scripts/Editor/GWBGameJam.Editor.asmdef
    └── 覆盖 Scripts/Editor/ 下所有 Editor 工具
    └── 引用 GWBGameJam.Runtime.asmdef
```

**目的：** 确保 Editor 工具代码不被编译进正式 Build。

---

## 禁止模式

| 禁止 | 原因 | 替代方案 |
|------|------|---------|
| `FindObjectOfType<T>()` | 运行时全场景扫描，性能差，隐式依赖 | Inspector SerializeField 注入 |
| `GameObject.Find("name")` | 依赖名称字符串，重构时容易断 | 同上 |
| Singleton Pattern | 全局隐式状态，难以测试和追踪 | 静态 EventBus + Inspector 引用 |
| `GetComponent<T>()` 在 Update | 每帧分配，GC 压力 | Awake 中缓存为私有字段 |
| `public` 字段直接暴露内部数据 | 任何系统都能随意修改，打破数据所有权 | `[SerializeField] private` + 只读公开属性 |
| Magic String | 重构时不报错，运行时才发现 | 枚举或常量类 |
| 跨系统直接修改他人私有数据 | 打破数据所有权规则 | 通过公开 API 或 EventBus |
| `SendMessage` / `BroadcastMessage` | 反射调用，无类型安全，性能差 | EventBus |
| 在 `Update` 内 Subscribe/Unsubscribe | 每帧注册/注销，逻辑混乱 | OnEnable/OnDisable |

---

## Edge Cases

**#1 两个系统在同一 Awake 中互相依赖**
若 SystemA.Awake() 需要 SystemB 已初始化：
处理方式：A 只在 Start() 中读取 B 的数据，Awake 只做自身初始化。如仍有顺序需求，通过 Script Execution Order 解决，**不通过代码内的延迟初始化（如 Invoke）解决**。

**#2 场景切换时 EventBus 订阅清理**
场景卸载时，Unity 自动触发场景内所有 GameObject 的 `OnDestroy`。
处理方式：每个系统**仅负责自身的 Unsubscribe**，在 `OnDestroy`（或 `OnDisable`）中完成。EventBus 不提供全局 ClearAll，不依赖外部统一清理。
前提：本项目为两场景结构（非 Additive 异步加载），OnDestroy 必然触发，此方案成立。

**#3 Prefab 实例化后引用丢失**
动态生成的 Prefab（怪物、面包）不在 Inspector 中预先引用：
处理方式：Prefab 自身持有所需引用（通过 Prefab 内部 Inspector 配置），或由生成它的 Manager 在 Instantiate 后通过公开 Initialize 方法注入必要数据。禁止 Prefab 实例在 Awake 中 Find 场景中的系统。

---

## Acceptance Criteria

- [ ] 项目中无任何 `FindObjectOfType` 调用（Grep 验证）
- [ ] 项目中无任何 `GameObject.Find` 调用（Grep 验证）
- [ ] 项目中无任何 Singleton 实现（Grep `Instance` 验证）
- [ ] 所有 EventBus Subscribe 调用，在同类中有对应 Unsubscribe（ReviewChecklist 验证）
- [ ] 所有 ScriptableObject 类位于 `Scripts/Config/` 目录
- [ ] 所有 Event Struct 位于 `Scripts/Events/` 目录
- [ ] Game.unity 中 GameObject 层级与本文档结构一致
- [ ] `GWBGameJam.Runtime.asmdef` 和 `GWBGameJam.Editor.asmdef` 存在且配置正确
- [ ] 所有运行时类声明 `namespace GWBGameJam`
- [ ] GameLoop 的 Script Execution Order 为场景中最低（最晚执行）

---

## Test Plan

**Test 1 — 禁止模式静态检查**
1. 在 Visual Studio / Rider 中全局搜索 `FindObjectOfType`、`GameObject.Find`、`SendMessage`
2. ✓ 结果为零

**Test 2 — 事件订阅配对检查**
1. 对每个含有 `EventBus<T>.Subscribe` 的类，确认存在对应 `Unsubscribe`
2. ✓ 所有订阅均有对应取消订阅

**Test 3 — 场景层级结构**
1. 打开 Game.unity
2. ✓ Hierarchy 中存在 `_Bootstrap`、`_Systems`、`_UI`、`_World` 节点
3. ✓ `_Systems` 下每个系统节点存在且名称与本文档一致

**Test 4 — 程序集编译隔离**
1. 将项目切换为 Release Build
2. ✓ `LaneCalculatorEditorWindow` 不出现在 Build 产物中

**Test 5 — Namespace 检查**
1. 随机抽查 5 个脚本文件
2. ✓ 每个文件顶部均声明 `namespace GWBGameJam`

---

## Future Extensions

- **多场景 Additive Loading**：当前两场景结构可扩展为 Additive 加载（例如 HUD 作为独立场景），EventBus 的静态特性天然支持跨场景事件，**支持扩展，无需改动 EventBus**。
- **Unit Test 支持**：系统间通过 EventBus 解耦，单个系统可在测试场景中单独运行，**支持**，但需额外建立 Test Assembly。
- **存档系统**：当前架构无持久化层。若要加，建议增加独立 SaveSystem 监听 GameLoop 事件，**对现有架构无侵入**。
- **平台移植（移动端）**：输入系统目前假设鼠标+键盘。若需触屏，需在各 System 的输入处理部分引入 InputAdapter 层，**需要局部改动，但 EventBus 层和系统逻辑层无需修改**。
