# Decision Log

记录所有重要的设计决定及其原因。
分类：[Gameplay] [Balance] [Tech]

---

## 2026-06-27

**[Gameplay] 怪物一击必杀**
原因：降低学习成本，简化战斗计算，玩家只需关注"打对了没有"。

---

**[Gameplay] 面团分五档，太软/太硬无法伤怪**
原因：给玩家压力——不能随便乱揉，但也保留了容错（可以无限调整）。

---

**[Gameplay] 烤焦仍然可以投掷，但不造成伤害**
原因：避免玩家卡死（总要把当前面团清掉才能揉下一个）。

---

**[Gameplay] 球道同时最多1只怪，全满时跳过生成**
原因：降低复杂度，防止玩家被同一球道连续刷怪淹没。

---

**[Gameplay] 面团—怪物对应关系放入 Config，不写死**
原因：策划可以在不改代码的情况下重新搭配"哪种面团打哪种怪"，支持后期调整。

---

**[Gameplay] 桌子HP采用计数制（N次），不采用百分比**
原因：百分比系统对玩家不直观；计数制更易理解（"还能被打几次"）。

---

**[Gameplay] 投掷方向由烤制期间鼠标所在球道决定**
原因：让瞄准成为烤制时的额外操作压力，增加游戏紧张感。

---

**[Tech] 怪物透视缩放使用 AnimationCurve（Inspector可调）**
原因：比填8个数字更直观，策划可以用可视化曲线精确控制远近感。

---

**[Tech] 球道40个点位由 Editor 内的点位计算器生成，不手填**
原因：透视球道的交点计算误差大，工具化生成更精确，且修改成本低。

---

**[Tech] 所有 Balance 数值使用 ScriptableObject**
原因：策划可直接在 Inspector 修改，不需要改代码，减少返工风险。

---

---

## 2026-06-27（续）

**[Gameplay] 面团比例采用区间档位制，不是精确值**
原因：让玩家无法一步调配精确，必须反复调整，增加操作压力和趣味性。

---

**[Gameplay] 左键单击加面粉（3/4格），右键长按加水（恒速）**
原因：两种操作手感不同（离散 vs 连续），形成节奏对比；恒速加水给玩家"慢慢调"的感觉。

---

**[Gameplay] 比例值在两端夹断，无法超出太软/太硬**
原因：防止极端值导致UI或判定逻辑异常；自然边界即为上限。

---

**[Gameplay] 怪物碰桌逃跑，计入关卡总数 X**
原因：关卡是"生存X波"模型而非"消灭X个"，怪物逃跑本身已经扣了玩家血，不需要额外惩罚。

---

**[Gameplay] 面包落点采用"锁定+预测"机制**
规则：怪物静止 → 砸当前点位；怪物移动中或距下次移动 ≤ 0.5s（待移动状态）→ 砸下一个点位。
原因：纯锁定会导致面包频繁落空（怪物刚好在移动），预测机制让投掷感更公平直观。
Config：待移动阈值 0.5s（Balance 可调）。

---

**[Gameplay] 关卡过渡时面团清空、桌子HP重置**
原因：每关作为独立单元，不应让上一关的状态影响下一关的难度体验。

---

**[UI] 桌子HP用横向条状UI显示**
原因：与面团比例UI风格统一，直观显示剩余承受次数。

---

---

## 2026-06-27（Spec 阶段）

**[Gameplay] 关卡过渡采用「按任意键继续」画面，非自动倒计时**
决定：Level 1→2、2→3 显示「第X关通过！按任意键继续」，玩家主动确认后才跳关。
原因：给玩家一个喘息点，避免上一关刚结束就被下一关的怪物突袭；与 DEATH/VICTORY 的交互方式保持一致（都是任意键），降低学习成本。

---

**[Tech] EventBus 场景切换清理：依赖 OnDestroy，不提供 ClearAll**
决定：每个系统在自身 OnDestroy/OnDisable 中执行 Unsubscribe，EventBus 不提供全局清理方法。
原因：两场景结构下 OnDestroy 必然触发，无需额外机制；ClearAll 需要维护全局频道注册表，增加复杂度且本项目规模不需要。

---

**[Tech] 事件系统采用静态泛型 EventBus，T 为 readonly struct**
决定：`EventBus<T>` 纯静态类，T 为值类型 Event Struct，无 Singleton，无 Inspector 配置。
原因：零依赖，类型安全，易审计 Subscribe/Unsubscribe 配对，适合 game jam 规模。

---

**[Tech] 场景结构：2 个场景（MainMenu + Game），Pause/Death/Victory 为 Canvas 叠加**
原因：资产集合完全不同的两个阶段用独立场景管理内存；叠加界面不值得单独开场景。

---

**[Tech] 程序集：Runtime + Editor 两个 Assembly Definition**
原因：隔离 Editor 工具代码，防止 Lane 计算器等编进正式 Build。

---

**[Tech] 顶级命名空间 GWBGameJam，不细分子命名空间**
原因：game jam 规模下子命名空间带来书写负担但收益有限，顶级命名空间足以隔离 Unity 内置类名冲突。

---

**[Tech] LaneWaypointConfig 增加 RecordedStepCount 防止数据过期**
决定：Editor 计算器写入点位时，同步写入生成时的 MoveStepCount 值。LaneSystem 启动时对比此值与当前 MonsterConfig.MoveStepCount，不一致则报 Error 并阻止进入游戏。
原因：策划修改步数后忘记重新烘焙会导致数组越界崩溃，主动检测比运行时报错更友好。

---

**[Design] 水粉比 = 水量 ÷ 面粉量，左键加粉使比值下降，右键加水使比值上升**
原因：确认数值方向，保证 DoughSystem 与 DoughStateBoundaryConfig 中的阈值方向一致。

---

**[Gameplay] 球道悬停高亮仅在烤制阶段激活**
决定：BakingState = Idle 时所有球道不响应鼠标，只有长按空格进入烤制后才启用高亮。
原因：高亮的唯一功能是指示投掷目标球道，在非烤制阶段显示高亮无实际意义，且会分散玩家注意力。
影响：LaneSystem 订阅 OnBakingStateChanged，而非始终激活。

---

---

## 2026-06-27（MonsterSystem Spec 阶段）

**[Gameplay] 怪物移动使用平滑插值动画（非瞬移）**
决定：怪物在 MoveIntervalSeconds 周期内通过 MoveDuration（默认0.3s）平滑移动到下一点位，其余时间静止等待。
原因：平滑动画让玩家能更直观地判断怪物位置，也使待移动状态的预测有明确的视觉提示。

---

**[Gameplay] 怪物生成策略：纯随机，无额外限制**
决定：球道随机（空球道中随机选一条），怪物类型独立随机（不考虑当前场上怪物分布）。
原因：game jam 规模，纯随机实现简单；生成频率与数量由 LevelSystem 控制，随机本身不需要约束即可产生足够的变化。

---

**[Tech] MonsterConfig 新增三个字段（追加更新 ConfigSchema）**
字段：MoveDuration（移动动画时长）/ WrongHitFlashCount（闪白次数）/ WrongHitFlashDuration（闪白时长）
原因：动画时长与视觉反馈参数属于 Balance 可调数值，需放入 SO 而非硬编码。

---

**[Tech] GetTargetPosition 在最后点位 PendingMove 状态返回当前点而非越界**
原因：posIndex = MoveStepCount-1 时的"下一步"是桌子（数组越界），预判无意义。怪物极短时间内就会碰桌消失，面包落在当前位置仍然有效。

---

---

## 2026-06-27（DoughSystem Spec 阶段）

**[Gameplay] 水粉比 UI 采用弹性跟随动画，比例值本身即时更新**
决定：DoughSystem 的 CurrentRatio 在每次玩家输入后立即更新；UISystem 读取目标值并用弹性插值跟上，不共享同一个变量。
原因：即时数据更新保证命中判定和状态推导无延迟；UI 弹性动画是纯视觉效果，不影响游戏逻辑。

---

**[Gameplay] 右键加水为固定速率，无加速**
决定：右键长按每帧 `ratio += WaterFillRate * deltaTime`，匀速线性，无积分加速。
原因：恒速给玩家可预测的操作手感，避免长按过久导致难以控制精度。

---

**[Gameplay] 面团比例 InitialRatio 可配置，默认 1.0（Medium）**
决定：每次关卡开始和投掷后，比例重置为 DoughConfig.InitialRatio（默认1.0）。
原因：Medium 是三个有效档位的中间点，玩家从此出发调整任意方向所需操作量均等，体验公平。

---

**[Tech] 面团「无面团窗口」通过订阅 ThrowSystem 事件实现**
决定：DoughSystem 订阅 OnThrowStarted（设 None）和 OnThrowCompleted（重置 ratio），而非检查 BakingState。
原因：ThrowSystem 最清楚投掷动画的开始和结束时机；BakingState 返回 Idle 的时机不保证与动画完成同步。

---

---

## 2026-06-27（BakingSystem Spec 阶段）

**[Gameplay] 松开空格立即投掷，无确认窗口**
原因：即时响应保持操作节奏感；「确认窗口」增加设计复杂度但收益有限，game jam 规模不值得。

---

**[Gameplay] 无悬停球道时（-1）投向随机球道，主动和强制投掷规则一致**
原因：随机球道比「投空」更有张力（玩家付出了烤制成本，面包必然落地）；统一规则降低玩家学习负担。

---

**[Tech] BakingSystem 投掷后立即回 Idle，不等待 ThrowSystem 动画完成**
原因：BakingSystem 职责是计时与触发，动画期的状态由 DoughSystem 的 None 窗口保护（防二次触发），两系统解耦。

---

---

## 2026-06-27（ThrowSystem Spec 阶段）

**[Gameplay] 投掷动画使用固定时长抛物线**
决定：`pos(t) = Lerp(start, end, t) + up * PeakHeight * 4t(1-t)`，ThrowDuration 固定（不随距离变化）。
原因：固定时长保证每次投掷的等待节奏一致，玩家不因距离不同而产生时机混乱；抛物线比直线更有手感。

---

**[Tech] 面团比例在 OnThrowRequested 同帧捕获，命中判定用捕获值而非到达时实时值**
原因：飞行期间 DoughSystem 已重置比例（None→InitialRatio），若在到达时读取比例，命中判定永远是 InitialRatio 对应的结果，与玩家的烤制操作脱节。捕获时机是正确性保证。

---

**[Tech] 命中判定基于比例距离（|ratio - center| ≤ ToleranceHalfWidth），而非 DoughState 枚举比较**
原因：DoughState 枚举比较只知道「是否在同一档位」，无法利用 ToleranceHalfWidth 实现精度分级；比例距离判定统一了「是否在正确档位的有效区间」，也与 UI 参考线的显示逻辑一致。

---

**[Gameplay] 命中到达后立即判定，不等待特效播放**
原因：特效是视觉反馈，不应阻塞游戏逻辑推进（怪物消失、LevelSystem 计数更新、DoughSystem 重置均不需要等特效）；等待特效会引入时序耦合。

---

---

## 2026-06-27（LevelSystem Spec 阶段）

**[Gameplay] 第一只怪物在第一个 SpawnIntervalSeconds 结束后生成（非 t=0）**
原因：给玩家一个喘息时间观察球道布局，避免关卡开始瞬间即面对威胁；与烤制阶段的「先准备」节奏一致。

---

**[Gameplay] 全满跳过时计时器完全重置（等待下一个完整间隔）**
原因：「连续生成补偿」（计时器不重置）会在全满解除后立即连续出怪，造成突发压力且难以预测；完整间隔重置行为更直观，玩家能感知到「堵满了就缓一缓」。

---

---

## 2026-06-27（UISystem Spec 阶段）

**[UI] 比例条方向：左=水（软），右=面粉（硬）**
原因：用户确认「反过来」，即高比例（水多=软）在左侧，低比例（粉多=硬）在右侧。

---

**[UI] 不显示关卡退场进度（X/TotalMonsters）**
原因：用户决定不展示；桌子 HP 条已提供足够的威胁感知。进度数据通过 LevelSystem 公开 API 保留，后期可随时加回。

---

**[Tech] LevelTransition 面板使用 OnLevelCleared 时缓存的关卡号，而非 OnGameStateChanged 时的实时值**
原因：OnGameStateChanged(LEVEL_TRANSITION) 触发时 LevelSystem 可能已加载下一关，导致显示的关卡号偏移。缓存方案确保显示「刚通过的关卡」。需在 GameLoop Review 中最终确认事件顺序。

---

**[UI] MainMenu_Canvas 不放入 Game.unity**
返回主菜单通过 SceneManager.LoadScene("MainMenu") 实现，Game.unity 的 UISystem 不需要 MainMenu_Canvas。GoToMainMenu() 触发场景切换，Game.unity 只管游戏内的 Canvas 状态（HUD / Pause / LevelTransition / Death / Victory）。

---

## 2026-06-28（集成调试阶段）

**[Tech] 怪物占位缩放采用 MonsterData.DisplayScale（每种怪独立），而非生成占位 sprite 或全局倍率**
决定：在 MonsterData 增加 `DisplayScale`（float，默认 1，≥0.01），在 MonsterController.ApplyScale 中乘在 ScaleCurve 之上。sprite 由策划手动赋值到各 MonsterData。
原因：sprite 与缩放放同一个 SO，策划「拖图 + 调大小」一站式完成；每种怪图原始尺寸不同，独立缩放最灵活；乘法叠加保留 ScaleCurve 的透视比例。

**[Tech] 分辨率独立：锁定 16:9，用相机黑边（letterbox）而非真实像素锁定**
决定：通过 (1) 各 Canvas 的 CanvasScaler→Scale With Screen Size 1920×1080；(2) 新增 `AspectRatioEnforcer` 脚本挂主相机，强制相机视口为 16:9，非 16:9 屏幕补黑边；(3) Player Settings 默认分辨率 1920×1080，实现「任何设备画面构图一致」。
原因：物理上无法在任意设备强制 1920×1080 像素；锁定 16:9 + 等比缩放 + 黑边是标准的分辨率无关方案，保证评审在任何屏幕看到相同构图。
注：脚本仅改 `Camera.rect`，不依赖 EventBus；Update 中只比较 Screen 尺寸（无 GetComponent），尺寸变化时才重算，符合 CodingRules。

**[Tech] 排查确认 Main Camera 无 bug，TASK_LOG 旧记录作废**
结论：Game.unity 的 Main Camera 已 Tag = MainCamera，`Camera.main`（LaneManager）能正常取到；且该引用仅在 Update 的 `_needsHoverCheck && IsHoverActive` 双守卫下使用，正常进入游戏不会解引用。TASK_LOG 中「Camera Tag 未设 → LaneManager NRE」为过期/误记。

---

## 开发工作流规则

**需求三分类：**
- **Gameplay**（玩法改动）→ 先更新 Spec，再分析影响，最后编码
- **Balance**（数值调整）→ 直接改 ScriptableObject，无需动代码
- **Tech**（技术改动）→ 更新技术设计文档，不影响 Spec

**固定分析流程（在任何改动前必须先问）：**
> 我想增加（或修改）这个功能，请先分析它会影响哪些 Spec、哪些系统、哪些配置，以及是否需要调整架构。不要写代码。

---
