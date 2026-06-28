# Spec Coding 工作流 — 启动 Prompt

将以下内容粘贴到新的 Claude 会话开头即可启动工作流。

---

## 粘贴内容（复制下方代码块全部内容）

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

---

## 使用说明

### 第一次使用（了解项目状态）
直接粘贴上方 prompt，Claude 会自动读取项目文档并汇报当前状态。

### 继续编码任务
粘贴 starter prompt 后，再说明具体任务，例如：
```
继续实现 LaneSystem，从 LaneManager.cs 开始。
```

### 修改需求
粘贴 starter prompt 后，说明想修改的内容：
```
我想把桌子 HP 从计数制改为百分比制，请先分析影响。
```
Claude 会遵守工作流，先分析影响再行动。

### 不需要解释 Spec Coding 工作流
所有规则已写入 CLAUDE.md，Claude 加载项目后自动遵守：
- 编码前先分析影响
- 每条指令后更新 TASK_LOG.txt
- 编码前读 013_CodingRules.md
- 每个 Task 后执行 014_ReviewChecklist.md

---

## 常见场景 Prompt 参考

**查看所有 Spec 文档：**
```
列出 Docs/ 目录下所有文档，并告诉我每个的当前状态。
```

**开始 Task Breakdown：**
```
所有 Spec 已确认。请把整个项目拆解成编码 Task 列表，
按照依赖顺序排列，每个 Task 粒度控制在 1-3 个脚本文件内。
```

**开始实现某个系统：**
```
开始实现 EventBus<T>，路径 Assets/Scripts/Core/EventBus.cs。
实现前先阅读 013_CodingRules.md。
```

**数值调整（Balance 类）：**
```
把第一关的生成间隔从 9 秒改为 6 秒。
```
（Claude 会直接告诉你改哪个 SO 字段，无需动代码）

**设计变更（Gameplay 类）：**
```
我想让怪物逃跑时桌子不扣 HP，只有碰到桌子才扣。
请先分析这个改动影响哪些 Spec 和系统。
```
（Claude 会先分析，等你确认后再修改 Spec 和代码）
