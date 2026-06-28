# 011 DevTools Spec

| 字段 | 内容 |
|------|------|
| Version | 1.0 |
| Status | Draft |
| Last Updated | 2026-06-27 |
| Depends On | 002_LaneSystem, 010_ConfigSchema, 012_Architecture |
| Required By | — |

---

## Goal

提供一个 Editor 工具窗口，从场景中读取 5 条球道的 `PolygonCollider2D` 顶点，按配置的 Y 坐标列表计算水平线与球道边缘的交点中点，将 40 个点位写入 `LaneWaypointConfig`。

此工具是 LaneSystem 的数据准备工具，不参与任何运行时逻辑。

---

## Scope

**In Scope：**
- Editor 菜单入口（`GWBGameJam / Lane Waypoint Calculator`）
- 从场景中的 `Lane_0~4` 读取 `PolygonCollider2D` 顶点
- 维护 `LaneCalculatorData`（Editor-only SO）存储 `MoveStepCount` 个 Y 坐标
- 「Auto Distribute」按钮：在所有球道 Collider 的 Y 包围盒内均匀生成 Y 坐标列表
- 「Bake」按钮：执行交点计算，写入 `LaneWaypointConfig.Lanes` 与 `RecordedStepCount`
- 「Preview in Scene」：在 Scene 视图绘制 Gizmo 球体，预览 40 个点位

**Out of Scope：**
- 运行时点位读取 → LaneSystem（通过 `LaneWaypointConfig` 数据）
- 球道视觉形状的调整 → 场景内直接编辑 Sprite / Collider
- 生产 Build 不包含此工具（位于 GWBGameJam.Editor 程序集）

---

## Editor 数据结构

### LaneCalculatorData（Editor-only ScriptableObject）

路径：`Assets/Editor/LaneCalculatorData.asset`（不在 Resources，仅 Editor 使用）

| 字段名 | 类型 | 说明 |
|--------|------|------|
| WaypointYPositions | float[] | 长度 = MonsterConfig.MoveStepCount。index 0 = 最远离桌子（Y 最大），末位 = 最靠近桌子（Y 最小）|
| LaneRootNames | string[] | 默认 ["Lane_0","Lane_1","Lane_2","Lane_3","Lane_4"]，工具据此在场景中查找节点 |

---

## 计算算法

**输入：**
- 5 条球道的 `PolygonCollider2D`（4 个顶点，透视四边形）
- `MoveStepCount` 个 Y 坐标（来自 `LaneCalculatorData.WaypointYPositions`）

**对每条球道（laneIndex = 0~4）：**
1. 从 4 个顶点中识别左边缘（较小 X 的两点）和右边缘（较大 X 的两点）
2. 对每个 `y_i = WaypointYPositions[i]`：
   - 求水平线 `Y = y_i` 与左边缘线段的交点 `xLeft`
   - 求水平线 `Y = y_i` 与右边缘线段的交点 `xRight`
   - 中点 = `((xLeft + xRight) / 2, y_i)`
3. 将 `MoveStepCount` 个中点写入 `LaneWaypointConfig.Lanes[laneIndex].Positions`

**线段与水平线交点公式（t = 参数化位置）：**
```
t = (y_i - y1) / (y2 - y1)
x = x1 + t * (x2 - x1)
```
若 `t < 0 || t > 1`，说明 Y 超出边缘范围，输出 Warning 并跳过该点位（不写入）。

**写入后：**
- `LaneWaypointConfig.RecordedStepCount = MonsterConfig.MoveStepCount`
- 调用 `EditorUtility.SetDirty(LaneWaypointConfig)` 保存修改

---

## Editor 窗口布局

```
┌─────────────────────────────────────┐
│  GWBGameJam - Lane Waypoint Calc   │
├─────────────────────────────────────┤
│  MonsterConfig: [____Object Field_] │
│  LaneWaypointConfig: [___________] │
│  LaneCalculatorData: [___________] │
├─────────────────────────────────────┤
│  Y 坐标列表 (MoveStepCount 个)       │
│  [Auto Distribute]                  │
│  [0] y = ___  [1] y = ___          │
│  [2] y = ___  [3] y = ___          │
│  ... （共 MoveStepCount 行）         │
├─────────────────────────────────────┤
│  [Bake]            [Preview Scene] │
│                                     │
│  状态栏：上次 Bake 时间 / 错误信息   │
└─────────────────────────────────────┘
```

---

## Auto Distribute 逻辑

1. 遍历 5 条球道的所有 `PolygonCollider2D` 顶点
2. 求所有顶点的 `yMin` 和 `yMax`（考虑 `transform.position` 偏移）
3. 留出边距（默认上下各 5%），在 `[yMax - margin, yMin + margin]` 内均匀分配 `MoveStepCount` 个 Y 值
4. index 0 = yMax（最远），末位 = yMin（最近）
5. 写入 `LaneCalculatorData.WaypointYPositions`，刷新窗口列表

---

## Preview in Scene

在 `OnSceneGUI` 或工具窗口的 `OnEnable` + `SceneView.duringSceneGui` 中绘制：
- 对 40 个已计算的点位，使用 `Handles.SphereHandleCap` 绘制小球（半径 0.1 世界单位）
- 颜色按球道区分（5 种颜色，Inspector 可配置，默认用色谱等分）
- 仅在工具窗口打开且 `LaneWaypointConfig` 有有效数据时绘制

---

## Edge Cases

**#1 场景中找不到 `Lane_X` 节点**
处理方式：Bake 前检查 `LaneRootNames` 中的每个名称是否存在于场景，若缺失则输出 Error 并中止 Bake，不写入任何数据。

**#2 某条 Y 值超出球道 Collider 的 Y 范围**
处理方式：交点参数 `t < 0 || t > 1`，该点位以 `Vector2.zero` 填充并输出 Warning（含球道索引和 Y 值）。使用者需调整 Y 坐标后重新 Bake。

**#3 顶点顺序不规则（PolygonCollider2D 顶点非矩形顺序）**
处理方式：通过比较顶点 X 值中位数来区分左右边缘，而非依赖顶点下标顺序。具体：对 4 个顶点按 X 升序排列，取前 2 个作为左边缘，后 2 个作为右边缘，各自按 Y 升序确定线段方向。

**#4 Bake 后 MonsterConfig.MoveStepCount 被修改**
处理方式：LaneSystem 运行时的过期检测（RecordedStepCount ≠ MoveStepCount）会报 Error。工具本身不监听 SO 修改，需手动重新 Bake。

---

## Acceptance Criteria

- [ ] Given 场景中有 5 条球道且已设置 PolygonCollider2D，When 点击「Auto Distribute」，Then WaypointYPositions 被均匀填充为 MoveStepCount 个值
- [ ] Given Y 坐标列表已填写，When 点击「Bake」，Then LaneWaypointConfig.Lanes[0~4].Positions 各含 MoveStepCount 个非零 Vector2
- [ ] Given Bake 完成，Then RecordedStepCount == MonsterConfig.MoveStepCount
- [ ] Given Preview 开启，When 在 Scene 视图中观察，Then 40 个球体分布在 5 条球道内，位置符合透视远近关系
- [ ] Given Y 坐标超出某球道范围，When Bake，Then Console 输出 Warning，该点位为 Vector2.zero，其余点位正常写入
- [ ] Given 场景中 Lane_2 不存在，When Bake，Then Console 输出 Error，LaneWaypointConfig 不被修改

---

## Test Plan

**Test 1 — 完整 Bake 流程**
1. 打开 `GWBGameJam / Lane Waypoint Calculator`
2. 配置三个 Object Field 引用
3. 点击「Auto Distribute」，✓ Y 列表自动填充 8 个值
4. 点击「Bake」，✓ LaneWaypointConfig Inspector 显示 5×8 = 40 个非零坐标
5. ✓ RecordedStepCount = 8

**Test 2 — Scene Preview**
1. Bake 后点击「Preview Scene」
2. ✓ Scene 视图出现 40 个彩色球体，分布在 5 条球道内

**Test 3 — 数据过期检测联动**
1. Bake 完成后，将 MonsterConfig.MoveStepCount 从 8 改为 10
2. 运行游戏
3. ✓ Console 输出过期 Error，游戏不进入 Playing
