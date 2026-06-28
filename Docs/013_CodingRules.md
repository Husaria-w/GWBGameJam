# 013 CodingRules

> AI 每次开始编码任务前必须完整读取本文件。

---

## 命名空间

- **唯一顶级命名空间**：`GWBGameJam`
- 所有运行时脚本均使用此命名空间，不细分子命名空间
- Editor 脚本同样使用 `GWBGameJam`（已通过 Assembly Definition 隔离）

```csharp
// ✓ 正确
namespace GWBGameJam { ... }

// ✗ 禁止
namespace GWBGameJam.Systems { }
namespace GWBGameJam.UI { }
```

---

## 程序集划分

| 程序集 | asmdef 文件 | 包含内容 |
|--------|------------|---------|
| GWBGameJam.Runtime | Assets/Scripts/GWBGameJam.Runtime.asmdef | 所有运行时代码 |
| GWBGameJam.Editor | Assets/Editor/GWBGameJam.Editor.asmdef | 仅 Editor 工具代码（LaneCalculator 等）|

- Editor asmdef 需设置 `Editor Only = true`，`References: GWBGameJam.Runtime`
- 运行时代码**不得**引用任何 `UnityEditor.*` 命名空间

---

## EventBus 使用规范

### 定义事件

```csharp
// ✓ 正确：readonly struct，On[Noun][Verb] 命名，存于 Scripts/Events/
public readonly struct OnMonsterDefeated
{
    public readonly int LaneIndex;
    public OnMonsterDefeated(int laneIndex) { LaneIndex = laneIndex; }
}

// ✗ 禁止：class、非 readonly struct
public class OnMonsterDefeated { }
```

### 订阅 / 取消订阅

```csharp
// ✓ 在 OnEnable 中订阅
private void OnEnable()
{
    EventBus<OnMonsterDefeated>.Subscribe(HandleMonsterDefeated);
}

// ✓ 在 OnDisable / OnDestroy 中取消（必须配对）
private void OnDestroy()
{
    EventBus<OnMonsterDefeated>.Unsubscribe(HandleMonsterDefeated);
}
```

- **Subscribe 和 Unsubscribe 必须配对**，无一遗漏
- EventBus 无 `ClearAll` 方法，每个系统自行清理
- 不在 OnDestroy 中发布新事件（避免向已销毁的系统广播）

### 发布事件

```csharp
// ✓ 正确
EventBus<OnMonsterDefeated>.Publish(new OnMonsterDefeated(laneIndex));

// ✗ 禁止：在循环内每帧发布同一事件（除非状态真的每帧变化）
```

---

## 禁止模式

| 禁止用法 | 替代方案 |
|---------|---------|
| `FindObjectOfType<T>()` | Inspector `[SerializeField]` 直接引用 |
| `GameObject.Find("name")` | Inspector 引用或 EventBus |
| `GetComponent<T>()` 在 `Update` 中调用 | Awake 缓存至字段 |
| Singleton 模式（`Instance` 静态字段） | EventBus + Inspector 引用 |
| Magic String（硬编码字符串做 key）| 常量或 ScriptableObject |
| `SendMessage` / `BroadcastMessage` | EventBus |
| `Resources.Load` | 直接 Inspector 引用或 Addressables |

---

## ScriptableObject 规范

```csharp
// ✓ 正确：SO 只存数据，不含逻辑
[CreateAssetMenu(fileName = "MonsterConfig", menuName = "GWBGameJam/MonsterConfig")]
public class MonsterConfig : ScriptableObject
{
    public float MoveIntervalSeconds = 1.0f;
    // ...
}

// ✗ 禁止：SO 中含 Start/Update/协程
```

- 所有 Balance 数值存于 SO，不硬编码于脚本
- SO Asset 路径：`ScriptableObjects/Configs/` 或 `ScriptableObjects/MonsterData/`

---

## Awake 验证规范（Validate 模式）

所有系统组件在 `Awake` 中验证 Config 约束：

```csharp
private void Awake()
{
    ValidateConfig();
    // 之后再初始化
}

private void ValidateConfig()
{
    if (_config.MaxHits < 1)
    {
        Debug.LogError("[TableSystem] MaxHits 不能小于 1，已强制设为 1");
        _config.MaxHits = 1;  // 自动修正，游戏不崩溃
    }
}
```

规则：
- **输出 `Debug.LogError`**（不是 Warning，Error 在 Console 会明确标红）
- **自动修正至合法值**，游戏继续运行（不 `throw`，不 `Debug.Break`）
- 修正逻辑与 Spec 中约束表格描述完全一致

---

## Script Execution Order

`GameLoop` 脚本的 Execution Order 设置为最低（最晚执行），在 ProjectSettings 中手动配置。

其余系统无特殊 Execution Order 要求（依赖 EventBus 解耦，不依赖 Update 顺序）。

---

## 协程 vs Update

- **计时器（BakingTimer、SpawnTimer）**：在 Update 中用 `Time.deltaTime` 累加，不用协程
  - 原因：协程在 `Time.timeScale = 0` 时无法暂停（除非用 `WaitForSecondsRealtime`），而 Update 天然随 TimeScale 暂停
- **抛物线动画**：在 Update 中用 `_flightTimer += Time.deltaTime` 驱动，不用协程
- **闪白动画（MonsterSystem）**：可以用协程（短暂视觉效果，不影响游戏逻辑），但需在 OnDestroy 中 StopAllCoroutines

---

## 组件引用缓存

```csharp
// ✓ 正确：Awake 缓存
private SpriteRenderer _spriteRenderer;
private void Awake() { _spriteRenderer = GetComponent<SpriteRenderer>(); }

// ✗ 禁止：Update 中每帧 GetComponent
private void Update() { GetComponent<SpriteRenderer>().color = ...; }
```

---

## 代码注释规范

- **默认不写注释**
- 仅在以下情况写单行注释：隐性约束、非直觉行为、已知 Unity Bug 的规避
- **禁止**：多行 `///` 文档注释，描述「做了什么」的注释，含任务/Issue 编号的注释

```csharp
// ✓ 合理注释：说明 WHY
// TimeScale=0 时 yield WaitForSeconds 不生效，改用 Update 累加
_bakingTimer += Time.deltaTime;

// ✗ 禁止注释
// 增加烤制时间（这是在 Sprint 2 中添加的功能）
// 调用 BakingSystem 的方法来更新状态
```

---

## 文件与文件夹结构

```
Assets/
├── Scripts/
│   ├── Core/           ← GameLoop.cs, GameEnums.cs, EventBus.cs
│   ├── Events/         ← 所有 Event Struct（每个 struct 一个文件）
│   ├── Systems/        ← LaneSystem, MonsterSystem, DoughSystem 等
│   ├── Config/         ← 所有 SO 定义类
│   └── UI/             ← UISystem.cs 及 UI 相关脚本
├── Editor/             ← LaneCalculator 等 Editor 工具（GWBGameJam.Editor 程序集）
├── Prefabs/
│   ├── Monsters/       ← 每种怪物一个 Prefab
│   └── Projectile/     ← 面包投射物 Prefab
└── ScriptableObjects/
    ├── Configs/        ← GameLoopConfig, BakingConfig 等
    └── MonsterData/    ← MonsterData_A/B/C.asset
```

---

## 场景层级结构（Game.unity）

```
_Bootstrap           ← GameLoop 组件挂载于此（Execution Order 最低）
_Systems/
├── LaneSystem       ← LaneManager 组件
├── MonsterSystem
├── DoughSystem
├── BakingSystem
├── ThrowSystem
├── LevelSystem
└── TableSystem
_UI/                 ← 所有 Canvas（见 009_UISystem）
_World/
├── Lanes/           ← Lane_0~4（各含 Visual + Collider 子节点）
├── MonsterContainer ← 运行时动态生成的怪物挂载此处
├── ProjectileContainer ← 面包投射物
└── Table            ← 桌子 Sprite
```

---

## 禁止使用的 Unity API

| API | 原因 |
|-----|------|
| `Object.FindObjectOfType` / `FindObjectsOfType` | 性能差，强耦合 |
| `GameObject.Find` | 依赖对象名字符串，脆弱 |
| `SendMessage` / `BroadcastMessage` | 无类型安全，难以追踪 |
| `DontDestroyOnLoad` | 两场景结构不需要跨场景持久化 |
| `PlayerPrefs` | 本项目无存档需求 |
| `Invoke` / `InvokeRepeating` | 用 Update 累加计时代替 |
