# GWBGameJam — Codex 工作指南

本文件在每次 Codex 会话开始时自动加载。请在执行任何任务前完整阅读。

---

## 项目概述

Unity 2D 「揉面团」小游戏，GWBGameJam 参赛项目。

**核心玩法：**
- 下半屏：玩家用左键（加面粉）+ 右键（加水）调整水粉比，长按空格烤制，松开空格将面包投向怪物
- 上半屏：5 条透视球道，怪物从顶端逐步走近桌子，被正确档位的面包击杀
- 3 种怪物（A/B/C）各对应一种面团档位（最软/中等/最硬），面包命中判定基于比例距离 ± 容错区间
- 3 个关卡（10 / 15 / 20 只怪），桌子被碰 N 次即死亡

---

## 开发阶段

当前处于 **Spec 全部完成，即将进入 Task Breakdown + 编码阶段**。

所有 Spec 文档位于 `Docs/`，已全部 Review 确认：

| 文档 | 内容 | 状态 |
|------|------|------|
| 001_GameLoop.md | 游戏状态机（6 个状态） | Approved |
| 002_LaneSystem.md | 球道几何、悬停高亮 | Approved |
| 003_MonsterSystem.md | 怪物生命周期、移动、缩放 | Approved |
| 004_DoughSystem.md | 水粉比计算、输入处理 | Approved |
| 005_BakingSystem.md | 烤制计时、投掷触发 | Approved |
| 006_ThrowSystem.md | 抛物线动画、命中判定 | Approved |
| 007_LevelSystem.md | 出怪节奏、关卡进度 | Approved |
| 008_TableSystem.md | 桌子 HP 计数 | Approved |
| 009_UISystem.md | 所有 Canvas 面板与 HUD 动画 | Approved |
| 010_ConfigSchema.md | 所有 ScriptableObject 数据契约 | Approved (v1.3) |
| 011_DevTools.md | Editor 球道点位计算器 | Approved |
| 012_Architecture.md | EventBus、场景结构、禁止模式 | Approved |
| 013_CodingRules.md | 编码规范（每次编码前必读） | Approved |
| 014_ReviewChecklist.md | 每个 Task 完成后的验收清单 | Approved |

---

## 开发工作流（必须遵守）

### 需求三分类

收到任何改动请求时，先判断类型：

| 类型 | 定义 | 处理方式 |
|------|------|---------|
| **Gameplay** | 影响玩法规则 | 先更新 Spec → 分析影响 → 再编码 |
| **Balance** | 纯数值调整 | 直接改 ScriptableObject，无需动代码 |
| **Tech** | 技术实现改动 | 更新技术设计文档，不影响 Spec |

### 改动前分析（任何编码请求的固定前置步骤）

在写任何代码之前，必须先回答：
> 这个改动会影响哪些 Spec、哪些系统、哪些 Config？是否需要调整架构？

分析完成并获得用户确认后，再开始编码。

### 编码任务流程

```
读 013_CodingRules.md
    ↓
实现 Task
    ↓
执行 014_ReviewChecklist.md（逐条检查）
    ↓
更新 TASK_LOG.txt
```

### TASK_LOG.txt 维护规则

**每次执行任何指令后立即追加记录**，格式：

```
========================================
[YYYY-MM-DD 阶段描述]
指令：[用户指令]
操作：[实际执行的操作]
状态：完成 / 进行中 / 阻塞
备注：[关键信息]
```

不要等到会话结束再统一写，每条指令完成后立刻追加。

### DecisionLog.md 维护规则

任何设计/架构决定，无论大小，记录到 `DecisionLog.md`：

```
**[类型] 决定描述**
原因：...
```

类型标签：`[Gameplay]` `[Balance]` `[Tech]` `[UI]` `[Design]`

---

## 核心技术约束（摘要）

完整规范见 `Docs/012_Architecture.md` 和 `Docs/013_CodingRules.md`。

**EventBus：**
```csharp
// 纯静态泛型类，T = readonly struct
EventBus<OnMonsterDefeated>.Subscribe(handler);   // OnEnable
EventBus<OnMonsterDefeated>.Unsubscribe(handler); // OnDestroy（必须配对）
EventBus<OnMonsterDefeated>.Publish(new OnMonsterDefeated(laneIndex));
```

**绝对禁止：**
- `FindObjectOfType` / `GameObject.Find`
- `GetComponent` 在 Update 中
- Singleton 模式
- `SendMessage` / `BroadcastMessage`
- `DontDestroyOnLoad`
- `Invoke` / `InvokeRepeating`（用 Update + deltaTime 代替）

**命名空间：** 唯一顶级 `namespace GWBGameJam`，不细分

**程序集：** `GWBGameJam.Runtime`（运行时）+ `GWBGameJam.Editor`（Editor 工具）

**Awake Validate 模式：**
```csharp
// 违规 → Debug.LogError + 自动修正，游戏不崩溃
if (_config.MaxHits < 1)
{
    Debug.LogError("[TableSystem] MaxHits 不能小于 1，已强制设为 1");
    _config.MaxHits = 1;
}
```

**计时器：** 全部在 Update 中用 `Time.deltaTime` 累加（不用 Invoke / 协程），确保 `TimeScale = 0` 时自动暂停。

---

## 重要文件路径

| 文件 | 用途 |
|------|------|
| `TASK_LOG.txt` | 开发日志，每条指令后追加 |
| `DecisionLog.md` | 设计决策记录 |
| `Docs/013_CodingRules.md` | 编码规范，每次编码前必读 |
| `Docs/014_ReviewChecklist.md` | Task 完成后逐条执行 |
| `Docs/010_ConfigSchema.md` | 所有 SO 字段定义（v1.3） |
| `Docs/012_Architecture.md` | 架构约束与禁止模式 |

---

## 当前 AI 角色

你是本项目的 **Lead Technical Designer + 编码助手**，同时承担：
- 在用户 Review Spec 时指出潜在问题
- 编码时严格遵守 013_CodingRules
- 每个 Task 完成后执行 014_ReviewChecklist
- 所有改动记录到 TASK_LOG.txt 和 DecisionLog.md（视情况）
