# 揉面团小游戏 — 开发交接 Prompt

将以下内容粘贴到新的 Claude 会话开头即可启动工作流。

---

## 粘贴内容（复制下方全部，路径改成你自己的）

```
你好，我需要你帮我在一个 Unity 2D 游戏项目中进行开发。

项目路径：[你的项目路径，例如 E:\Program Files\GWBGameJam]

首先请执行以下操作（按顺序）：
1. 阅读 [路径]\CLAUDE.md
2. 阅读 [路径]\TASK_LOG.txt
3. 阅读 [路径]\DecisionLog.md
4. 阅读 [路径]\Docs\013_CodingRules.md

读完后告诉我：
- 项目当前处于什么阶段
- 下一步应该做什么

不要开始编码，等我确认后再行动。
```

---

## 当前开发状态（2026-06-28）

### 编码进度

| 阶段 | 内容 | 状态 |
|------|------|------|
| T01–T18 | 所有系统代码 | ✅ 完成 |
| T19 | Prefab 制作（Monster_A/B/C + Bread）| ✅ 完成 |
| T20 | Inspector 连线 | 🔧 部分完成（见下方 Bug 表）|
| T21 | 球道点位烘焙 | ⚠️ 有问题（坐标范围不对）|
| T22 | 端到端集成测试 | 🔧 进行中 |

### 已知 Bug / 待办

| 问题 | 说明 | 优先级 |
|------|------|--------|
| Main Camera Tag | 未设为 MainCamera，LaneManager NRE | **高** |
| Monster 不显示 | MonsterData 的 IdleSprite/HitSprite 未赋值（无美术资源）| **高** |
| 球道点位错误 | LaneCalculator 算出 Y=200-500，与实际场景不匹配 | **高** |
| UI 参考线 + Indicator | RatioBar 的参考线和指示器 GameObject 未在 Inspector 连线 | **中** |
| 中文字体缺失 | LiberationSans 不支持中文，过关文本已暂改英文 | **低** |

### 已完成的集成修复（T20 期间）
- GameLoop.Start() 改为直接调用 StartGame()
- GoToMainMenu() 改为 SceneManager.LoadScene("MainMenu")
- SceneLoader.cs 已创建，MainMenu Start 按钮已连线
- PauseMenu_Canvas 继续/返回主菜单按钮已连线
- Build Settings 已添加 MainMenu + Game 场景

---

## 待讨论的设计变更

以下内容在 StarterPrompt 中被提出，**与当前 Spec 不一致，尚未实现**。
接手后如需改动，请先让 Claude 分析影响，不要直接改代码。

| 变更 | 原 Spec | 新想法 | 影响范围 |
|------|--------|-------|---------|
| 左键随机面粉量 | 固定 -0.75 | 随机 ½-1 格 | DoughSystem + DoughConfig |
| 右键随机水速 | 固定 WaterFillRate | 随机 1-3 倍速 | DoughSystem + DoughConfig |
| 加水/加粉互相推动 | 两端独立调整 | 加一方推回另一方 | DoughSystem（需先确认是代码改动还是纯视觉）|
| 比例条参考线数量 | 3 条（有效档位）| 5 条（含太软/太硬边界）| RatioBar |
| 关卡独立场景 | 单个 Game.unity | Level1/2/3 分场景 | 架构级，影响 GameLoop/LevelSystem/UISystem |

---

## 常见 Prompt 参考

**修 Bug（举例）：**
```
Play 时报 NPE：LaneManager 某行，帮我定位原因并修复。
```

**Inspector 连线问题：**
```
T20 中 RatioBar 的参考线字段应该填什么 GameObject？
```

**先讨论再改动：**
```
我想把左键加面粉改为随机 ½-1 格，并实现加水时推动面粉条退回。
先分析这个改动影响哪些 Spec 和代码，不要直接改。
```

**调数值（Balance 类，不需要改代码）：**
```
把烤制时间从 0.5/1.5/2.5 改为 0.3/1.0/2.0。
```

**加调试功能：**
```
在 HUD 上加一个 Debug 按钮，点击直接通关当前关卡。
```

**查手动操作步骤：**
```
打开 Docs/ManualInstructions.md，告诉我球道点位烘焙那步怎么修坐标问题。
```
