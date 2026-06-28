# GWBGameJam — 揉面团

GWB Game Jam 参赛项目。Unity 2D 揉面团小游戏。

---

## 游戏玩法

- **下半屏**：揉面桌。左键点击加面粉，右键长按加水，调整水粉比至目标档位。长按空格烤制，松开投掷。
- **上半屏**：5 条透视球道，怪物从远处逐步走近桌子。被正确档位的面包一击击杀，否则闪白继续前进。
- **目标**：3 关内击败所有怪物，不让桌子被碰坏。

---

## 环境要求

| 工具 | 版本 |
|------|------|
| Unity | 2022.3 LTS（建议）|
| IDE | Rider / VS Code |
| Claude Code | 最新版 |

---

## 快速开始

```bash
git clone <repo-url>
```

用 Unity Hub 打开 `D:\Unity Workplace\GWBGameJam`，等待包导入完成即可。

---

## 项目结构

```
GWBGameJam/
├── Assets/
│   ├── Scripts/         # 运行时代码（GWBGameJam.Runtime 程序集）
│   │   ├── Core/        # GameLoop, EventBus, GameEnums
│   │   ├── Events/      # 所有 Event Struct
│   │   ├── Systems/     # 各系统（LaneSystem, MonsterSystem 等）
│   │   ├── Config/      # ScriptableObject 类定义
│   │   └── UI/          # UISystem
│   ├── Editor/          # Editor 工具（GWBGameJam.Editor 程序集）
│   ├── Prefabs/         # Monster, Projectile 等 Prefab
│   └── ScriptableObjects/
│       ├── Configs/     # 所有 Balance 配置（改数值只改这里）
│       └── MonsterData/ # MonsterData_A/B/C.asset
├── Docs/                # 所有设计文档（Spec）
├── CLAUDE.md            # Claude AI 工作指南（自动加载）
├── DecisionLog.md       # 设计决策记录
└── TASK_LOG.txt         # 开发日志
```

---

## 使用 Claude AI 协同开发

本项目使用 **Spec Coding 工作流**：所有设计写成 Spec 文档，Claude 读 Spec 后再编码，确保实现与设计一致。

### 启动方法

1. 用 **Claude Code** 打开本项目目录
2. 复制以下 prompt 粘贴到输入框：

```
你好，我需要你帮我在一个 Unity 2D 游戏项目中进行开发。

项目路径：D:\Unity Workplace\GWBGameJam

首先请执行以下操作（按顺序）：
1. 阅读 D:\Unity Workplace\GWBGameJam\CLAUDE.md（项目工作指南）
2. 阅读 D:\Unity Workplace\GWBGameJam\TASK_LOG.txt（了解当前开发进度）
3. 阅读 D:\Unity Workplace\GWBGameJam\DecisionLog.md（了解已有的设计决策）

读完后，用 2-3 句话告诉我：
- 项目当前处于什么阶段
- 下一步应该做什么

不要开始任何编码或 Spec 修改，等我确认后再行动。
```

3. Claude 汇报状态后，告知你要做的任务

更多使用场景见 `Docs/StarterPrompt.md`。

### 修改数值（Balance 调整）

直接在 Unity Inspector 中修改 `ScriptableObjects/Configs/` 下对应的 SO 文件，**无需改代码、无需问 Claude**。

| 想改的内容 | 对应 SO |
|-----------|---------|
| 出怪速度 / 每关总数 | LevelConfig |
| 烤制时间阈值 | BakingConfig |
| 怪物移动速度 | MonsterConfig |
| 桌子承受次数 | TableConfig |
| 面粉/水的操作速度 | DoughConfig |
| 命中容错区间 | DoughStateBoundaryConfig |

### 修改玩法规则

先告诉 Claude「我想改 XX，请先分析影响」，等分析完再确认，Claude 会自动更新 Spec 和代码。**不要直接改代码。**

---

## 关键文档

| 文档 | 说明 |
|------|------|
| `Docs/012_Architecture.md` | 架构规范、禁止模式 |
| `Docs/010_ConfigSchema.md` | 所有 SO 字段定义 |
| `Docs/013_CodingRules.md` | 编码规范 |
| `Docs/014_ReviewChecklist.md` | Task 验收清单 |
| `DecisionLog.md` | 历史设计决策（看这里了解「为什么这样做」）|
| `TASK_LOG.txt` | 开发进度日志 |

---

## 联系

有问题找秦钰奇（qinyuqicharles@gmail.com）。
