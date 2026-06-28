# 014 ReviewChecklist

> 每个 Task 完成后，AI 必须逐条执行此清单。发现问题立即修复，不推迟到下一个 Task。

---

## 执行方式

完成每个编码 Task 后，按以下顺序逐节检查，在每一条后标注 `[✓]` 或 `[✗ 已修复]`。若某条不适用于本 Task，标注 `[N/A]`。

---

## 1. 禁止模式检查

- [ ] 无 `FindObjectOfType` / `FindObjectsOfType`
- [ ] 无 `GameObject.Find`
- [ ] 无 `GetComponent` 在 `Update` / `FixedUpdate` / `LateUpdate` 中调用
- [ ] 无 Singleton（无 `public static XXX Instance`）
- [ ] 无 `SendMessage` / `BroadcastMessage`
- [ ] 无 `DontDestroyOnLoad`
- [ ] 无 `Invoke` / `InvokeRepeating`（计时使用 Update 累加）
- [ ] 无 `Resources.Load`
- [ ] 无硬编码字符串用于逻辑判断（Tag、Layer、动画参数等）

---

## 2. EventBus 配对检查

- [ ] 每个 `EventBus<T>.Subscribe` 都在 `OnDestroy` 或 `OnDisable` 中有对应的 `Unsubscribe`
- [ ] 没有在 `OnDestroy` 中发布新事件
- [ ] 所有 Event Struct 均为 `readonly struct`，位于 `Scripts/Events/` 目录
- [ ] Event 命名遵循 `On[Noun][Verb]` 格式

---

## 3. Awake 验证检查

- [ ] 所有 Config 字段约束（见 010_ConfigSchema）在 `Awake` 中执行 Validate
- [ ] 违规时输出 `Debug.LogError` 并自动修正，不 `throw` 也不 `Debug.Break`
- [ ] Inspector 必填引用（SO、Transform 等）在 `Awake` 中检查是否为 null

---

## 4. 命名空间与程序集检查

- [ ] 所有文件顶部使用 `namespace GWBGameJam`
- [ ] Editor 代码位于 `Assets/Editor/` 且属于 `GWBGameJam.Editor` 程序集
- [ ] 运行时代码无 `using UnityEditor;`

---

## 5. ScriptableObject 规范检查

- [ ] SO 类仅含字段定义，无 `Start` / `Update` / 协程
- [ ] 新增 SO 已添加 `[CreateAssetMenu]` Attribute
- [ ] 新增 SO 字段已同步更新至 `010_ConfigSchema.md`

---

## 6. 数据所有权检查（参照 012_Architecture）

- [ ] 本 Task 编写的代码只**写**本系统拥有的数据
- [ ] 读取其他系统的数据时使用公开 API 或 EventBus，未直接访问内部字段
- [ ] 未引入新的跨系统直接字段访问

---

## 7. Spec 符合性检查

- [ ] 实现与对应 Spec 的「Acceptance Criteria」逐条对照
- [ ] 新增字段或行为变更已更新对应 Spec 或在 DecisionLog 中记录原因
- [ ] 未引入 Spec 未定义的副作用

---

## 8. Unity 最佳实践检查

- [ ] `GetComponent` 调用仅在 `Awake` 中（不在其他生命周期方法里）
- [ ] 协程（若使用）在 `OnDestroy` 中调用 `StopAllCoroutines`
- [ ] `Time.deltaTime` 用于所有帧率无关的计时（非 `Time.fixedDeltaTime`，除非在 FixedUpdate 中）
- [ ] 无内存泄漏风险：动态实例化的 GameObject 在不需要时有对应的 `Destroy`

---

## 9. 日志与调试代码检查

- [ ] 无遗留的 `Debug.Log`（纯调试用途，非 Error/Warning）
- [ ] 生产代码中保留的 `Debug.LogWarning` / `Debug.LogError` 均有明确含义和触发条件
- [ ] 无注释掉的死代码块

---

## 10. TASK_LOG 更新

- [ ] 已在 `TASK_LOG.txt` 中追加本次 Task 的记录（时间、指令、操作、状态、备注）

---

## 快速修复指引

| 发现问题 | 修复动作 |
|---------|---------|
| Subscribe 无对应 Unsubscribe | 在 OnDestroy 中补全 |
| GetComponent 在 Update 中 | 提取到 Awake 缓存字段 |
| Spec AC 未通过 | 修复代码，不修改 AC |
| 新行为未在 Spec 中 | 先问用户是否需要更新 Spec |
| SO 字段未在 ConfigSchema | 追加更新 010_ConfigSchema.md |
