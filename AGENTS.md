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
- 依赖 RimMind-Core 提供的 API（`RimMindAPI`、`ContextEngine`、`ContextRequest`、`SchemaRegistry`、`SettingsUIHelper`、`NpcManager`、`ScenarioIds`）
- 与 RimMind-Memory 松耦合（通过反射推送叙事记忆到 `NarratorStore`）

## 源码结构

```text
Source/
├── RimMindStorytellerMod.cs                     Mod 入口，注册 Harmony/SettingsTab/Cooldown/ContextProviders，初始化设置
├── Storyteller/
│   ├── StorytellerComp_RimMindDirector.cs       AI 驱动的事件选择器（StorytellerComp）+ 事件通知
│   ├── StorytellerCompProperties_RimMindDirector.cs  属性定义（mtbDays, maxCandidates）
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
// 核心周期方法 — 每 1000 tick 由 RimWorld 调用
override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
// 流程：检查 pending 结果 → 发起新请求（MTB 随机触发）
// 仅处理 Map_PlayerHome 目标

// 状态
bool IsActive              // 有 pending 请求或结果
int LastSuccessTick        // 上次 AI 成功选择的 tick
int LastFailTick           // 上次 AI 失败的 tick

// 估算
int GetEstimatedTicksUntilNextEvent()  // mtbDays * 60000

// 强制请求（调试用）
bool ForceRequest(IIncidentTarget target)
// 使用 RimMindAPI.RequestStructured，清除 ModCooldown

// 事件通知
bool ShouldNotifyPlayer(IncidentDef incidentDef)
// 仅 ThreatBig 和 ThreatSmall 类别触发通知
// 受 enableEventNotification 设置控制

void RegisterEventNotification(FiringIncident incident, IncidentResponse incidentResponse)
// 通过 RimMindAPI.RegisterPendingRequest 注册审批请求
// 标题：ThreatBig → "叙事者宣告"，ThreatSmall → "叙事者低语"
// 描述：优先使用 announce，其次截断 reason（20字），最后默认
// 选项："不是吧！"（shock, +0.05 张力）/"来的好！"（excited, -0.05 张力）/"了解"（accept, 0）
// 玩家反应记录到 StorytellerMemory.RecordPlayerReaction()
```

**触发机制**：非固定间隔，使用 `Rand.MTBEventOccurs(mtbDays, 60000f, 1000f)` 随机触发。`mtbDays` 默认 1.5 游戏天。

**AI 请求参数**：
```csharp
new ContextRequest {
    NpcId = NpcManager.Instance?.GetNpcForMap(map) ?? "NPC-storyteller",
    Scenario = ScenarioIds.Storyteller,
    Budget = GetStorytellerBudget(),  // 从 Core ContextSettings 读取
    MaxTokens = 200,
    Temperature = 0.8f,
}
// 通过 RimMindAPI.RequestStructured(ctxRequest, SchemaRegistry.IncidentOutput, callback)
```

### StorytellerCompProperties_RimMindDirector

```csharp
public class StorytellerCompProperties_RimMindDirector : StorytellerCompProperties
{
    public float mtbDays = 1.5f;       // MTB 天数（可被 Settings 覆盖）
    public int maxCandidates = 15;     // ⚠ 死代码 — 未被业务逻辑消费
}
```

### StorytellerComp_RimMindFallback

AI 不可用或 Director 不健康时的回退事件生成器：

```csharp
override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
// 条件检查链：
// 1. FallbackMode == None → 退出
// 2. Director.IsActive → 退出
// 3. API 已配置 + Director 健康（最近成功且未失败）→ 退出
// 4. MTB 随机触发 → 选择类别 → 选择事件
```

**Director 健康判断**：`now - LastSuccessTick < mtbDays * 60000 * 2` 且 `now - LastFailTick >= mtbDays * 60000 * 2`

**回退模式**（`FallbackMode` 枚举）：

| 模式 | MTB 天数 | ChooseCategory 逻辑 |
|------|----------|---------------------|
| Cassandra | 4.6 | 固定 ThreatBig |
| Randy | 1.35 | 30% ThreatBig / 30% ThreatSmall / 40% Misc |
| Phoebe | 8.0 | 40% FactionArrival / 60% ThreatSmall |
| None | - | 不生成事件 |

### RimMindIncidentSelector

响应解析和 DTO 定义：

```csharp
// 解析 AI 响应（JSON 反序列化）
static (FiringIncident?, IncidentResponse?) ParseResponse(
    string aiContent, IIncidentTarget target, StorytellerComp source)
// 使用 JsonConvert.DeserializeObject<IncidentResponse>
// points_multiplier 限制在 0.3~2.0
// faction_hint 仅匹配敌对派系
// raid_strategy_hint 通过 DefDatabase 查找
// 最终 CanFireNow 二次验证
```

### IncidentResponse（JSON DTO）

```csharp
public class IncidentResponse
{
    public string defName = string.Empty;
    public string reason = string.Empty;
    public string? announce;
    public IncidentParams? @params;
    public ChainInfo? chain;
}

public class IncidentParams
{
    public float? points_multiplier;     // 0.3~2.0 范围
    public string? faction_hint;         // 仅匹配敌对派系
    public string? raid_strategy_hint;   // RaidStrategyDef.defName
}

public class ChainInfo
{
    public string chain_id = string.Empty;
    public int chain_step;
    public int chain_total;
    public string? next_hint;
}
```

### StorytellerMemory

全局单例 WorldComponent，存档持久化：

```csharp
public class StorytellerMemory : WorldComponent
{
    static StorytellerMemory? _instance;  // 构造时赋值
    public static StorytellerMemory? Instance => _instance;

    // 事件历史
    IReadOnlyList<IncidentHistoryRecord> Records;
    int MaxRecords { get; set; }    // 默认 50
    void RecordIncident(IncidentDef def, IIncidentTarget target, int tick);
    string GetRecentSummary(int count);
    bool IsOnCooldown(IncidentDef def);  // ⚠ 死代码 — 无调用点
    void ClearRecords();

    // 对话记录
    IReadOnlyList<DialogueRecord> DialogueRecords;
    int MaxDialogueRecords { get; set; }  // 默认 30
    void RecordDialogue(string role, string content, int tick);
    string GetRecentDialogueSummary(int count);
    void ClearDialogueRecords();

    // 玩家情感反应
    private IReadOnlyList<PlayerReactionRecord> PlayerReactions;  // ⚠ 死代码 — 只写不读
    void RecordPlayerReaction(string incidentDefName, string incidentLabel,
        string reaction, string reactionLabel, int tick);

    // 张力系统
    float TensionLevel { get; }     // 0~1，初始 0.5
    void UpdateTension(IncidentCategoryDef category);
    void ApplyDecayAndCleanup();    // 衰减 + 清理过期链
    void DecayTension(int ticksElapsed);  // 使用 Settings.tensionDecayPerDay
    void ApplyTensionDelta(float delta);  // 直接调整张力（玩家反应用）

    // 事件链
    int ActiveChainsCount;
    void RecordChainStep(string chainId, int chainStep, int chainTotal,
        string nextHint, string incidentDefName, int tick, float points, string factionDefName);
    void CleanupExpiredChains();    // 使用 Settings.chainExpireDays
    string GetActiveChainsSummary();

    // 自定义 Prompt
    string CustomSystemPrompt;

    // 序列化
    override void ExposeData();
}
```

**内嵌数据类**（同文件）：

```csharp
class ChainStep : IExposable { incidentDefName, triggeredTick, completed }
class EventChain : IExposable { chainId, steps, currentStep, nextHint, lastAdvancedTick, lastFactionDefName, lastPoints }
class DialogueRecord : IExposable { role, content, tick }
class PlayerReactionRecord : IExposable { incidentDefName, incidentLabel, reaction, reactionLabel, tick }
```

### Window_StorytellerDialogue

玩家与叙事者对话的窗口：

```csharp
class Window_StorytellerDialogue : Window
{
    override Vector2 InitialSize => new Vector2(520f, 560f);

    // 对话使用 RimMindAPI.Chat（走 ContextEngine 构建 Prompt）
    // MaxTokens = 300, Temperature = 0.9f
    // MaxHistoryRounds = 6（窗口内消息上限）

    // 对话记录推送到 StorytellerMemory.RecordDialogue()
    // 通过 TryPushToMemoryMod() 反射推送到 RimMindMemory 的 NarratorStore
}
```

**反射桥接细节**（`TryPushToMemoryMod`）：
- 查找 `RimMindMemory` 程序集
- 获取 `RimMind.Memory.Data.RimMindMemoryWorldComponent.Instance`
- 获取 `NarratorStore` 属性
- 读取 `RimMind.Memory.RimMindMemoryMod.Settings` 的 `enableMemory`、`narratorMaxActive`、`narratorMaxArchive`
- 创建 `MemoryEntry.Create(content, MemoryType.Event, tick, 0.3f, null)`
- 调用 `NarratorStore.AddActive(entry, narratorMaxActive, narratorMaxArchive)`

### CompStorytellerAltar

祭坛建筑组件：

```csharp
class CompStorytellerAltar : ThingComp
{
    override IEnumerable<Gizmo> CompGetGizmosExtra()
    // 返回 Command_Action，图标 UI/StorytellerAltarIcon
    // 点击打开 Window_StorytellerDialogue(parent.Map)
}
```

### Patch_IncidentWorker_TryExecute

```csharp
[HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
// 仅 __result == true 时执行：
// 1. StorytellerMemory.RecordIncident()
// 2. StorytellerMemory.UpdateTension()
// 3. RimMindAPI.NotifyIncidentExecuted()
// 4. 若 RimMindAPI.CanTriggerDialogue → 随机选殖民者 → RimMindAPI.TriggerDialogue(pawn, incidentContext)
```

### RimMindStorytellerMod

```csharp
public class RimMindStorytellerMod : Mod
{
    public static RimMindStorytellerSettings Settings;

    // 构造：
    // 1. GetSettings<RimMindStorytellerSettings>()
    // 2. new Harmony("mcocdaa.RimMindStoryteller").PatchAll()
    // 3. RimMindAPI.RegisterSettingsTab("storyteller", ..., StorytellerSettingsTab.Draw)
    // 4. RimMindAPI.RegisterModCooldown("Storyteller", () => (int)(mtbDays * 60000f))
    // 5. RegisterProviders() — 注册上下文提供者

    // 上下文提供者
    void RegisterProviders()
    // "storyteller_state" → PawnContextProvider：张力 + 近期事件 + 活跃链
    //   优先级：PromptSection.PriorityAuxiliary，ModId: "Storyteller"
    // "storyteller_dialogue" → StaticProvider：近期对话摘要
    //   优先级：PromptSection.PriorityAuxiliary，ModId: "Storyteller"
}
```

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
    ├── RimMindAPI.ShouldSkipStorytellerIncident() → yield break
    ├── MTB 随机未触发 → yield break
    │
    └── 发起 AI 请求
        ├── 构建 ContextRequest（NpcId, Scenario, Budget, MaxTokens, Temperature）
        ├── RimMindAPI.RequestStructured(request, SchemaRegistry.IncidentOutput, callback)
        │       └── Core ContextEngine 构建 Prompt + 发送请求
        │       ▼
        └── OnAIResponseReceived(response, target)
            ├── response.Success == false → 记录 _lastFailTick
            ├── RimMindIncidentSelector.ParseResponse(content, target, this)
            │   ├── JsonConvert.DeserializeObject<IncidentResponse>
            │   ├── DefDatabase<IncidentDef>.GetNamedSilentFail
            │   ├── 应用 params（points_multiplier/faction/raidStrategy）
            │   ├── CanFireNow 二次验证
            │   └── 构建 FiringIncident
            ├── 记录 _lastSuccessTick，设置 _hasPendingResult
            ├── incidentResponse.chain != null → memory.RecordChainStep()
            ├── memory.UpdateTension(incidentDef.category)
            └── ShouldNotifyPlayer → RegisterEventNotification()
                ├── ThreatBig → "叙事者宣告"
                ├── ThreatSmall → "叙事者低语"
                └── 玩家选择反应 → RecordPlayerReaction + ApplyTensionDelta
```

### 事件执行后处理

```text
Patch_IncidentWorker_TryExecute.Postfix(__result == true)
    │
    ├── StorytellerMemory.RecordIncident(def, target, tick)
    ├── StorytellerMemory.UpdateTension(category)
    ├── RimMindAPI.NotifyIncidentExecuted()
    └── RimMindAPI.CanTriggerDialogue?
        └── 随机选殖民者 → RimMindAPI.TriggerDialogue(pawn, incidentContext)
```

### 对话流程

```text
Window_StorytellerDialogue.SendMessage()
    │
    ├── 记录到 _messages + StorytellerMemory.RecordDialogue("user", ...)
    ├── TryPushToMemoryMod("user", ...) → 反射推送 NarratorStore
    ├── 构建 ContextRequest（NpcId, Scenario, Budget, CurrentQuery, MaxTokens, Temperature）
    ├── RimMindAPI.Chat(request).ContinueWith(...)
    │       └── Core ContextEngine 构建 Prompt + 发送请求
    │       ▼
    └── callback
        ├── _messages.Add(("assistant", content))
        ├── StorytellerMemory.RecordDialogue("assistant", ...)
        └── TryPushToMemoryMod("assistant", ...)
```

## 张力系统

```csharp
// 初始值
_tensionLevel = 0.5f

// 事件类别对张力的影响
ThreatBig      → +0.25
ThreatSmall    → +0.12
Misc           → -0.05
FactionArrival → -0.08
其他           → 0

// 玩家情感反应对张力的影响
shock          → +0.05
excited        → -0.05
accept         → 0

// 衰减（使用 Settings.tensionDecayPerDay，默认 0.03/天）
// 在 ApplyDecayAndCleanup 中执行
```

## 事件链系统

AI 可设计多步事件链：

1. AI 响应中包含 `chain` 字段
2. `StorytellerMemory.RecordChainStep()` 记录步骤（含 points、factionDefName）
3. 下次请求时，`GetActiveChainsSummary()` 通过 ContextProvider 注入 Prompt
4. AI 根据 `next_hint` 选择下一个事件继续链
5. 超过 `Settings.chainExpireDays`（默认 10 天）未推进的链自动过期

## 设置项

| 设置 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| enableIntervalTrigger | bool | true | 启用 MTB 随机触发 |
| fallbackMode | FallbackMode | Cassandra | 回退模式 |
| mtbDays | float | 1.5 | 事件平均间隔天数 |
| maxCandidates | int | 15 | ⚠ 死代码 — 未被业务逻辑消费 |
| debugLogging | bool | false | 调试日志 |
| requestExpireTicks | int | 30000 | ⚠ 设置无效 — RegisterEventNotification 硬编码 60000 |
| maxEventRecords | int | 50 | 最大事件记录数 |
| maxDialogueRecords | int | 30 | 最大对话记录数 |
| enableEventNotification | bool | true | 启用事件通知 |
| maxPlayerReactions | int | 20 | 玩家反应记录上限 |
| chainExpireDays | float | 10.0 | 事件链过期天数 |
| tensionDecayPerDay | float | 0.03 | 张力衰减率 |

## 代码约定

### 命名空间

- `RimMind.Storyteller` — 核心逻辑（Mod 入口、StorytellerComp、事件选择）
- `RimMind.Storyteller.Memory` — 记忆系统
- `RimMind.Storyteller.Settings` — 设置
- `RimMind.Storyteller.UI` — 界面
- `RimMind.Storyteller.Comps` — 建筑组件
- `RimMind.Storyteller.Patch` — Harmony 补丁
- `RimMind.Storyteller.Debug` — 调试动作

### StorytellerComp 注册

通过 `StorytellerDef` XML 注册，不是自动发现：

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

所有用户可见文本使用翻译键，前缀为 `RimMind.Storyteller.*`：
- `RimMind.Storyteller.UI.*` — UI 文本
- `RimMind.Storyteller.Prompt.*` — Prompt 上下文标签（DaySummary, RolePlayer, ChainProgress 等）
- `RimMind.Storyteller.Dialogue.*` — 对话 UI 文本
- `RimMind.Storyteller.Altar.*` — 祭坛交互
- `RimMind.Storyteller.Context.*` — 事件上下文

**注意**：XML 中存在 66 个孤儿翻译键（旧版 Prompt 构建遗留），详见 `docs/06-problem/RimMind-Storyteller.md`。

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

- **Force AI Incident Selection** — 强制发起 AI 事件选择（RequestStructured + 清除 Cooldown）
- **Show Memory** — 显示内存状态（事件记录、自定义 prompt、张力、chain）
- **Fire Incident (manual)** — 手动选择并触发事件（Dialog_DebugOptionListLister）
- **Test Fallback Mode** — 循环切换回退模式
- **Show Director State** — 显示 Director 状态（IsActive、下次事件估算时间/天数、难度、MTB、对话/事件记录数）

## 注意事项

1. **StorytellerComp 生命周期**：由 RimWorld Storyteller 系统管理，不使用 GameComponent
2. **MTB 触发**：非固定间隔，使用 `Rand.MTBEventOccurs` 随机触发，间隔由 `mtbDays` 控制
3. **事件冷却**：`StorytellerMemory.IsOnCooldown()` 基于 `IncidentDef.minRefireDays` 检查（⚠ 死代码，未被调用）
4. **CanFireNow 检查**：ParseResponse 最终验证时调用 `IncidentDef.Worker.CanFireNow()`
5. **反射桥接**：对话记忆通过 `TryPushToMemoryMod()` 反射推送到 RimMind-Memory 的 `NarratorStore`，异常静默吞掉
6. **Fallback 智能判断**：不仅检查 `Director.IsActive`，还检查 Director 健康状态
7. **张力衰减**：在 `ApplyDecayAndCleanup()` 中执行，由 `MakeIntervalIncidents` 每次调用时触发
8. **Settings 覆盖 Def**：`mtbDays` 运行时以 `RimMindStorytellerSettings` 为准，Def 中的值为后备默认
9. **ModCooldown 注册**：通过 `RimMindAPI.RegisterModCooldown("Storyteller", ...)` 注册冷却，ForceRequest 时清除
10. **Structured Output**：事件选择使用 `RimMindAPI.RequestStructured` + `SchemaRegistry.IncidentOutput`
11. **Chat 路径**：祭坛对话使用 `RimMindAPI.Chat`，两者都由 Core 的 ContextEngine 统一构建 Prompt
12. **事件通知**：仅 ThreatBig/ThreatSmall 触发通知，受 `enableEventNotification` 控制
13. **上下文提供者**：注册 `storyteller_state`（PawnContext）和 `storyteller_dialogue`（Static），供 Core ContextEngine 引用
14. **ShouldSkipStorytellerIncident**：Director 触发前检查 Core 是否要求跳过
15. **NotifyIncidentExecuted**：事件执行后通知 Core，用于 Core 层面的事件计数/冷却管理
