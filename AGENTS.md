# AGENTS.md — RimMind-Storyteller

本文件供 AI 编码助手阅读，描述 RimMind-Storyteller 的架构、代码约定和扩展模式。

## 项目定位

RimMind-Storyteller 是 RimMind AI 模组套件的 AI 叙事者模块。它替换/增强 RimWorld 的 Storyteller 系统，用 LLM 决定事件选择。

**核心职责**：
1. **AI 事件选择**：`StorytellerComp_RimMindDirector` 以 MTB 随机触发，通过 Core 的 `ContextEngine` 构建上下文，调用 `RimMindAPI.RequestStructured` 选择事件
2. **回退模式**：AI 不可用或 Director 不健康时，按经典 Storyteller 模式生成事件
3. **张力系统**：跟踪当前张力等级（0~1，初始 0.5），影响事件选择和衰减
4. **事件链系统**：支持多步事件链（chain），AI 可设计连续叙事
5. **叙事者对话**：玩家可通过祭坛建筑与叙事者对话，走 `RimMindAPI.Chat` 路径
6. **事件通知**：AI 选择威胁事件时，通过审批页面通知玩家，玩家可选择情感反应影响张力
7. **记忆系统**：`StorytellerMemory` 持久化事件历史、对话记录、玩家反应、事件链、张力

**依赖关系**：
- 依赖 RimMind-Core 提供的 API（`RimMindAPI`、`ContextEngine`、`ContextRequest`、`SchemaRegistry`、`SettingsUIHelper`、`NpcManager`、`ScenarioIds`、`ContextKeyRegistry`、`TaskInstructionBuilder`）
- 与 RimMind-Memory 松耦合（通过反射推送叙事记忆到 `NarratorStore`）

## 源码结构

```text
Source/
├── RimMindStorytellerMod.cs                     Mod 入口，注册 Harmony/SettingsTab/Cooldown/ContextKeys
├── Storyteller/
│   ├── StorytellerComp_RimMindDirector.cs       AI 驱动的事件选择器（StorytellerComp）+ 事件通知
│   ├── StorytellerCompProperties_RimMindDirector.cs  属性定义（mtbDays）
│   ├── StorytellerComp_RimMindFallback.cs       回退事件生成器
│   ├── StorytellerCompProperties_RimMindFallback.cs  属性定义（空）
│   └── RimMindIncidentSelector.cs               响应解析 + DTO（ParseResponse, IncidentResponse, IncidentParams, ChainInfo）
├── Memory/
│   ├── StorytellerMemory.cs                     WorldComponent 全局单例 + 内嵌数据类
│   └── IncidentHistoryRecord.cs                 事件历史记录
├── Settings/
│   ├── RimMindStorytellerSettings.cs            模组设置（ModSettings）
│   └── StorytellerSettingsTab.cs                设置 UI 绘制（使用 SettingsUIHelper）
├── UI/
│   └── Window_StorytellerDialogue.cs            玩家与叙事者对话窗口
├── Comps/
│   └── CompStorytellerAltar.cs                  祭坛建筑组件 + CompProperties
├── Patch/
│   └── Patch_IncidentWorker_TryExecute.cs       事件执行后置补丁
└── Debug/
    └── StorytellerDebugActions.cs               Dev 菜单调试动作
```

**非源码关键文件**：
```text
Defs/StorytellerDefs/RimMindDirector.xml         StorytellerDef（含 Director + Fallback + Disease + CategoryMTB + Triggered comps）
Defs/ThingDefs/StorytellerAltar.xml              祭坛建筑 ThingDef
About/About.xml                                  Mod 元数据（依赖 Harmony + RimMindCore）
Languages/ChineseSimplified/Keyed/RimMind_Storyteller.xml  翻译键
Languages/English/Keyed/RimMind_Storyteller.xml  翻译键
```

## 关键类与 API

### StorytellerComp_RimMindDirector

AI 驱动的事件选择器，继承 `StorytellerComp`：

```csharp
override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
// 流程：检查 pending 结果 → 发起新请求（MTB 随机触发）
// 仅处理 Map_PlayerHome 目标

bool IsActive              // 有 pending 请求或结果
int LastSuccessTick        // 上次 AI 成功选择的 tick
int LastFailTick           // 上次 AI 失败的 tick

int GetEstimatedTicksUntilNextEvent()  // mtbDays * 60000

bool ForceRequest(IIncidentTarget target)
// 使用 RimMindAPI.RequestStructured，清除 ModCooldown
// ⚠ 仍使用 Core.Internal.AIRequestQueue，应替换为 RimMindAPI.ClearModCooldown

bool ShouldNotifyPlayer(IncidentDef incidentDef)
// 仅 ThreatBig 和 ThreatSmall 类别触发通知，受 enableEventNotification 设置控制

void RegisterEventNotification(FiringIncident incident, IncidentResponse incidentResponse)
// 通过 RimMindAPI.RegisterPendingRequest 注册审批请求
// expireTicks 读取 Settings.requestExpireTicks
// 选项："不是吧！"（shock, +0.05 张力）/"来的好！"（excited, -0.05 张力）/"了解"（accept, 0）

internal static float GetStorytellerBudget()
// 从 Core ContextSettings 读取 Budget，Window_StorytellerDialogue 也调用此方法
```

**AI 请求参数**：
```csharp
new ContextRequest {
    NpcId = NpcManager.Instance?.GetNpcForMap(map) ?? "NPC-storyteller",
    Scenario = ScenarioIds.Storyteller,
    Budget = GetStorytellerBudget(),
    CurrentQuery = "Select the most appropriate incident event...",
    MaxTokens = 400,
    Temperature = 0.8f,
    Map = map,
}
// 通过 RimMindAPI.RequestStructured(ctxRequest, SchemaRegistry.IncidentOutput, callback)
```

### StorytellerCompProperties_RimMindDirector

```csharp
public class StorytellerCompProperties_RimMindDirector : StorytellerCompProperties
{
    public float mtbDays = 1.5f;       // MTB 天数（可被 Settings 覆盖）
}
```

### StorytellerComp_RimMindFallback

AI 不可用或 Director 不健康时的回退事件生成器。

**回退模式**（`FallbackMode` 枚举）：

| 模式 | MTB 天数 | ChooseCategory 逻辑 |
|------|----------|---------------------|
| Cassandra | 4.6 | 固定 ThreatBig |
| Randy | 1.35 | 30% ThreatBig / 30% ThreatSmall / 40% Misc |
| Phoebe | 8.0 | 40% FactionArrival / 60% ThreatSmall |
| None | - | 不生成事件 |

**Director 健康判断**：`now - LastSuccessTick < mtbDays * 60000 * 2` 且 `now - LastFailTick >= mtbDays * 60000 * 2`

### RimMindIncidentSelector

```csharp
static (FiringIncident?, IncidentResponse?) ParseResponse(
    string aiContent, IIncidentTarget target, StorytellerComp source)
// JsonConvert.DeserializeObject → points_multiplier 0.3~2.0 → faction_hint 仅敌对 → CanFireNow 验证
```

### IncidentResponse（JSON DTO）

```csharp
public class IncidentResponse
{
    public string defName = string.Empty;
    public string reason = string.Empty;
    public string? announce;
    public IncidentParams? @params;     // points_multiplier, faction_hint, raid_strategy_hint
    public ChainInfo? chain;            // chain_id, chain_step, chain_total, next_hint
}
```

### StorytellerMemory

全局单例 WorldComponent，存档持久化：

```csharp
public class StorytellerMemory : WorldComponent
{
    static StorytellerMemory? _instance;
    public static StorytellerMemory? Instance => _instance;

    // 事件历史
    IReadOnlyList<IncidentHistoryRecord> Records;
    int MaxRecords { get; set; }        // 默认 50
    void RecordIncident(IncidentDef def, IIncidentTarget target, int tick);
    string GetRecentSummary(int count);
    void ClearRecords();

    // 对话记录
    IReadOnlyList<DialogueRecord> DialogueRecords;
    int MaxDialogueRecords { get; set; }  // 默认 30
    void RecordDialogue(string role, string content, int tick);
    string GetRecentDialogueSummary(int count);
    void ClearDialogueRecords();

    // 玩家情感反应（已被 storyteller_reactions provider 消费）
    internal IReadOnlyList<PlayerReactionRecord> PlayerReactions;
    void RecordPlayerReaction(...);

    // 张力系统
    float TensionLevel { get; }         // 0~1，初始 0.5
    void UpdateTension(IncidentCategoryDef category);
    void ApplyDecayAndCleanup();
    void ApplyTensionDelta(float delta);

    // 事件链
    int ActiveChainsCount;
    void RecordChainStep(...);
    void CleanupExpiredChains();
    string GetActiveChainsSummary();

    // 自定义 Prompt
    string CustomSystemPrompt;
}
```

**内嵌数据类**（同文件）：`ChainStep`、`EventChain`、`DialogueRecord`、`PlayerReactionRecord`

### Window_StorytellerDialogue

```csharp
class Window_StorytellerDialogue : Window
{
    override Vector2 InitialSize => new Vector2(520f, 560f);
    // 对话使用 RimMindAPI.Chat，MaxTokens = 400, Temperature = 0.9f
    // MaxHistoryRounds = 6
    // 对话记录推送到 StorytellerMemory + TryPushToMemoryMod（反射推送 NarratorStore）
}
```

**反射桥接**（`TryPushToMemoryMod`）：查找 `RimMindMemory` 程序集 → `RimMindMemoryWorldComponent.Instance` → `NarratorStore` → `MemoryEntry.Create` → `AddActive`。`GetField("Settings")` 正确读取 Memory 设置。

### CompStorytellerAltar

祭坛建筑组件，点击打开 `Window_StorytellerDialogue(parent.Map)`。

### Patch_IncidentWorker_TryExecute

```csharp
[HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
// __result == true 时：RecordIncident → UpdateTension → NotifyIncidentExecuted → TriggerDialogue
```

### RimMindStorytellerMod

```csharp
public class RimMindStorytellerMod : Mod
{
    public static RimMindStorytellerSettings Settings;

    // 构造：GetSettings → Harmony.PatchAll → RegisterSettingsTab → RegisterModCooldown → RegisterProviders

    void RegisterProviders()
    // ⚠ 旧 API（[Obsolete]，自动桥接到 ContextKeyRegistry，无场景守卫）：
    //   "storyteller_state" → PawnContextProvider：张力 + 近期事件 + 活跃链
    //   "storyteller_dialogue" → StaticProvider：近期对话摘要
    //
    // 新 API（ContextKeyRegistry，有场景守卫）：
    //   "storyteller_task" → L0_Static, 0.95f：TaskInstructionBuilder 构建事件选择指令
    //   "storyteller_context" → L1_Baseline, 0.85f：张力 + 近期事件 + 活跃链（与 storyteller_state 重复）
    //   "storyteller_reactions" → L1_Baseline, 0.8f：玩家情感反应
}
```

**⚠ 已知问题**：`storyteller_state` 和 `storyteller_context` 提供重复数据，导致 Storyteller 场景下双重注入。应删除 `storyteller_state` 的旧 API 注册，只保留 `storyteller_context`。`storyteller_dialogue` 也应迁移到 ContextKeyRegistry。

## 数据流

### 事件选择主流程

```text
StorytellerComp_RimMindDirector.MakeIntervalIncidents(target)
    │
    ├── target 不是 Map_PlayerHome → yield break
    ├── 有 pending 结果 → yield return FiringIncident
    ├── 有 pending 请求 → yield break
    ├── API 未配置 → yield break
    ├── enableIntervalTrigger 关闭 → yield break
    ├── ShouldSkipStorytellerIncident → yield break
    ├── MTB 随机未触发 → yield break
    │
    └── 发起 AI 请求
        ├── ContextRequest（NpcId, Scenario, Budget, MaxTokens=400, Temperature=0.8f, Map）
        ├── RimMindAPI.RequestStructured(request, SchemaRegistry.IncidentOutput, callback)
        └── OnAIResponseReceived → ParseResponse → RecordChainStep → UpdateTension → RegisterEventNotification
```

### 对话流程

```text
Window_StorytellerDialogue.SendMessage()
    ├── RecordDialogue("user") + TryPushToMemoryMod("user")
    ├── ContextRequest（NpcId, Scenario, Budget, MaxTokens=400, Temperature=0.9f, Map）
    ├── RimMindAPI.Chat(request).ContinueWith(...)
    └── RecordDialogue("assistant") + TryPushToMemoryMod("assistant")
```

### 上下文注入流程（ContextEngine）

```text
ContextEngine.BuildSnapshot(scenario, pawn)
    │
    ├── 获取 ContextKeyRegistry.GetAll() 中所有 key
    ├── 按 Layer 分组调度（L0 → L1 → L2 → L3 → L4）
    ├── 对每个 key 调用 ValueProvider(pawn) 获取 List<ContextEntry>
    │
    ├── L0_Static:
    │   └── storyteller_task（仅 Storyteller 场景）
    │       └── TaskInstructionBuilder.Build("RimMind.Storyteller.Prompt.TaskInstruction", ...)
    │
    ├── L1_Baseline:
    │   ├── storyteller_context（仅 Storyteller 场景）
    │   │   └── 张力 + 近期事件 + 活跃链
    │   ├── storyteller_reactions（仅 Storyteller 场景）
    │   │   └── 玩家情感反应记录
    │   ├── storyteller_state（⚠ 无场景守卫，所有场景都会注入）
    │   │   └── 张力 + 近期事件 + 活跃链（与 storyteller_context 重复）
    │   └── storyteller_dialogue（⚠ 无场景守卫，所有场景都会注入）
    │       └── 近期对话摘要
    │
    └── 合并去重 → Budget 裁剪 → 返回最终上下文
```

## 张力系统

```csharp
_tensionLevel = 0.5f  // 初始值

// 事件类别影响
ThreatBig → +0.25    ThreatSmall → +0.12
Misc → -0.05         FactionArrival → -0.08

// 玩家反应影响
shock → +0.05    excited → -0.05    accept → 0

// 衰减：Settings.tensionDecayPerDay（默认 0.03/天）
```

## 事件链系统

1. AI 响应包含 `chain` 字段 → `RecordChainStep()` 记录
2. `GetActiveChainsSummary()` 通过 `storyteller_context` provider 注入 Prompt
3. AI 根据 `next_hint` 选择下一个事件继续链
4. 超过 `Settings.chainExpireDays`（默认 10 天）未推进自动过期

## 设置项

| 设置 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| enableIntervalTrigger | bool | true | 启用 MTB 随机触发 |
| fallbackMode | FallbackMode | Cassandra | 回退模式 |
| mtbDays | float | 1.5 | 事件平均间隔天数 |
| debugLogging | bool | false | 调试日志 |
| requestExpireTicks | int | 30000 | 请求过期 tick |
| maxEventRecords | int | 50 | 最大事件记录数 |
| maxDialogueRecords | int | 30 | 最大对话记录数 |
| enableEventNotification | bool | true | 启用事件通知 |
| maxPlayerReactions | int | 20 | 玩家反应记录上限 |
| chainExpireDays | float | 10.0 | 事件链过期天数 |
| tensionDecayPerDay | float | 0.03 | 张力衰减率 |

## 代码约定

### 命名空间

- `RimMind.Storyteller` — 核心逻辑
- `RimMind.Storyteller.Memory` — 记忆系统
- `RimMind.Storyteller.Settings` — 设置
- `RimMind.Storyteller.UI` — 界面
- `RimMind.Storyteller.Comps` — 建筑组件
- `RimMind.Storyteller.Patch` — Harmony 补丁
- `RimMind.Storyteller.Debug` — 调试动作

### 上下文注册（ContextKeyRegistry）

**推荐方式**（新 API，有场景守卫）：
```csharp
ContextKeyRegistry.Register("key", ContextLayer.L1_Baseline, priority,
    pawn => {
        if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller)
            return new List<ContextEntry>();
        // ... 构建上下文
        return new List<ContextEntry> { new ContextEntry(content) };
    }, "RimMind.Storyteller");
```

**⚠ 避免使用**（旧 API，`[Obsolete]`，无场景守卫）：
```csharp
RimMindAPI.RegisterPawnContextProvider("key", provider, priority, modId);
RimMindAPI.RegisterStaticProvider("key", provider, priority, modId);
```

旧 API 内部会自动桥接到 `ContextKeyRegistry`，但桥接后**没有场景守卫**，会在所有场景下注入数据。

### StorytellerComp 注册

通过 `StorytellerDef` XML 注册：

```xml
<StorytellerDef ParentName="BaseStoryteller">
    <defName>RimMindDirector</defName>
    <comps>
        <li Class="RimMind.Storyteller.StorytellerCompProperties_RimMindDirector" />
        <li Class="RimMind.Storyteller.StorytellerCompProperties_RimMindFallback" />
        <li Class="StorytellerCompProperties_Disease">...</li>
        <li Class="StorytellerCompProperties_CategoryMTB">...</li>
        <li Class="StorytellerCompProperties_Triggered">...</li>
    </comps>
</StorytellerDef>
```

### 翻译键

前缀为 `RimMind.Storyteller.*`：
- `RimMind.Storyteller.UI.*` — UI 文本
- `RimMind.Storyteller.Prompt.*` — Prompt 上下文标签
- `RimMind.Storyteller.Dialogue.*` — 对话 UI 文本 + 对话 Prompt
- `RimMind.Storyteller.Altar.*` — 祭坛交互
- `RimMind.Storyteller.Context.*` — 事件上下文

**注意**：XML 中存在约 52 个孤儿翻译键（旧版 Prompt 构建遗留），详见 `docs/06-problem/RimMind-Storyteller.md`。`TaskInstruction.*` 的 12 个子键由 `TaskInstructionBuilder.Build()` 在运行时消费，非孤儿。`Prompt.TensionLevel` 和 `Prompt.ReactionRecordLine` 被 ContextKeyRegistry provider 消费，非孤儿。

### Harmony

- Harmony ID：`mcocdaa.RimMindStoryteller`
- Patch_IncidentWorker_TryExecute：`IncidentWorker.TryExecute` 后置补丁

### 构建

- 目标框架：`net48`
- C# 语言版本：9.0
- Nullable：enable
- RimWorld 版本：1.6
- 输出路径：`../1.6/Assemblies/`
- 程序集名：`RimMindStoryteller`
- NuGet 依赖：`Krafs.Rimworld.Ref`、`Lib.Harmony.Ref`、`Newtonsoft.Json`
- 项目引用：`RimMindCore.dll`（从 `../../RimMind-Core/$(GameVersion)/Assemblies/`）

## 调试

Dev 菜单（需开启开发模式）→ RimMind Storyteller：

- **Force AI Incident Selection** — 强制发起 AI 事件选择
- **Show Memory** — 显示内存状态
- **Fire Incident (manual)** — 手动选择并触发事件
- **Test Fallback Mode** — 循环切换回退模式
- **Show Director State** — 显示 Director 状态
- **Show Tension History** — 显示张力历史和玩家反应
- **Clear Storyteller Memory** — 清空记忆
- **Reset Tension Level** — 重置张力到 0.5
- **Show Event Chains Detail** — 显示事件链详情

## 注意事项

1. **StorytellerComp 生命周期**：由 RimWorld Storyteller 系统管理，不使用 GameComponent
2. **MTB 触发**：非固定间隔，使用 `Rand.MTBEventOccurs` 随机触发
3. **CanFireNow 检查**：ParseResponse 最终验证时调用 `IncidentDef.Worker.CanFireNow()`
4. **反射桥接**：对话记忆通过 `TryPushToMemoryMod()` 反射推送到 Memory 的 `NarratorStore`，`GetField("Settings")` 正确读取 Memory 设置
5. **Fallback 智能判断**：不仅检查 `Director.IsActive`，还检查 Director 健康状态
6. **张力衰减**：在 `ApplyDecayAndCleanup()` 中执行，由 `MakeIntervalIncidents` 每次调用时触发
7. **Settings 覆盖 Def**：`mtbDays` 运行时以 `RimMindStorytellerSettings` 为准
8. **ModCooldown 注册**：通过 `RimMindAPI.RegisterModCooldown("Storyteller", ...)` 注册冷却
9. **Structured Output**：事件选择使用 `RimMindAPI.RequestStructured` + `SchemaRegistry.IncidentOutput`
10. **Chat 路径**：祭坛对话使用 `RimMindAPI.Chat`
11. **⚠ Core.Internal 访问**：`ForceRequest` 使用 `Core.Internal.AIRequestQueue.Instance?.ClearCooldown("Storyteller")`，应替换为 `RimMindAPI.ClearModCooldown("Storyteller")`
12. **⚠ 重复上下文注入**：`storyteller_state`（旧 API）和 `storyteller_context`（新 API）提供相同数据，导致 Storyteller 场景下双重注入。应删除 `storyteller_state` 旧 API 注册
13. **⚠ 旧 API 已 Obsolete**：`RegisterPawnContextProvider` / `RegisterStaticProvider` 已标记 `[Obsolete]`，应迁移到 `ContextKeyRegistry.Register`
14. **ContextKeyRegistry**：注册 `storyteller_task`（L0_Static, 0.95f）、`storyteller_context`（L1_Baseline, 0.85f）、`storyteller_reactions`（L1_Baseline, 0.8f）
15. **TaskInstructionBuilder**：`Build("RimMind.Storyteller.Prompt.TaskInstruction", ...)` 构建 12 段事件选择指令
16. **跨模组依赖**：仅依赖 Core；Bridge-RimChat/RimTalk 通过 Core API 或 Memory 数据间接交互
