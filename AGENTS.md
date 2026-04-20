# AGENTS.md — RimMind-Storyteller

本文件供 AI 编码助手阅读，描述 RimMind-Storyteller 的架构、代码约定和扩展模式。

## 项目定位

RimMind-Storyteller 是 RimMind AI 模组套件的 AI 叙事者模块。它替换/增强 RimWorld 的 Storyteller 系统，用 LLM 决定事件选择。

**核心职责**：
1. **AI 事件选择**：`StorytellerComp_RimMindDirector` 以 MTB 随机触发调用 AI 选择下一个事件
2. **候选事件构建**：从当前可触发事件中筛选候选列表，按 FallbackMode 加权排序
3. **事件链系统**：支持多步事件链（chain），AI 可设计连续叙事
4. **张力系统**：跟踪当前张力等级（0~1，初始 0.5），影响事件选择和衰减
5. **回退模式**：AI 不可用或 Director 不健康时，按经典 Storyteller 模式生成事件
6. **叙事者对话**：玩家可通过祭坛建筑与叙事者对话，对话含机密信息段落
7. **事件通知**：AI 选择威胁事件时，通过审批页面通知玩家，玩家可选择情感反应影响张力
8. **记忆系统**：`StorytellerMemory` 持久化事件历史、对话记录、玩家反应、事件链、张力、殖民地快照

**依赖关系**：
- 依赖 RimMind-Core 提供的 API（`RimMindAPI`、`StructuredPromptBuilder`、`PromptBudget`、`PromptSection`、`ContextComposer`、`SettingsUIHelper`）
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
│   ├── RimMindIncidentSelector.cs               候选事件构建 + 响应解析 + DTO
│   └── StorytellerPromptBuilder.cs              Prompt 段落构建（事件选择 + 对话）
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
Languages/ChineseSimplified/Keyed/RimMind_Storyteller.xml  翻译键（含 Prompt 模板）
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
// 使用 RimMindAPI.RequestImmediate，清除 ModCooldown

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
new AIRequest {
    MaxTokens = 200,
    Temperature = 0.8f,
    RequestId = "Storyteller_Director",
    ModId = "Storyteller",
    UseJsonMode = true,
    Priority = AIRequestPriority.Normal,
    ExpireAtTicks = 当前tick + requestExpireTicks
}
```

### StorytellerCompProperties_RimMindDirector

```csharp
public class StorytellerCompProperties_RimMindDirector : StorytellerCompProperties
{
    public float mtbDays = 1.5f;       // MTB 天数（可被 Settings 覆盖）
    public int maxCandidates = 15;     // 最大候选事件数（可被 Settings 覆盖）
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
| Phoebe | 8.0 | 40% FactionArrival / 60% ThreatSmall（FactionArrival 参与回退加权选择；候选过滤时的"单独检查"仅适用于正常事件生成的 AllowedCategories 过滤，不影响 Phoebe 回退权重） |
| None | - | 不生成事件 |

### StorytellerPromptBuilder

Prompt 段落构建器，使用 RimMind-Core 的 `StructuredPromptBuilder` + `PromptBudget`：

```csharp
// 事件选择 System Prompt
static string BuildSystemPrompt(StorytellerMemory memory)
// 使用 StructuredPromptBuilder.FromKeyPrefix("RimMind.Storyteller.Prompt.System")
// 五段式：Role → Goal → Process → Constraint → Example → Output → Fallback
// 附加：难度指导（BuildDifficultyGuidance）+ 自定义 prompt

// 事件选择 User Prompt
static string BuildUserPrompt(Map map, StorytellerMemory memory, int maxCandidates)
// 使用 PromptSection 列表 + PromptBudget(6000, 800) 组装
// 段落优先级：CurrentInput > KeyState > Memory > Auxiliary

// 对话 System Prompt
static string BuildDialogueSystemPrompt()
// 使用 StructuredPromptBuilder.FromKeyPrefix("RimMind.Storyteller.Dialogue.System")
// 五段式 + 难度约束

// 对话 User Prompt
static string BuildDialogueUserPrompt(Map map, StorytellerMemory memory,
    string userMsg, List<(string role, string content)> dialogueMessages)
// 含机密信息段落（下次事件估算、难度参数）

// 难度指导
static string BuildDifficultyGuidance()
// 根据 threatScale + allowBigThreats 生成行为标签

// 张力标签
static string GetTensionLabel(float tension)
```

**User Prompt 段落**（按优先级）：

| 段落名 | 优先级 | 内容 |
|--------|--------|------|
| candidates | CurrentInput | 候选事件列表 |
| situation | KeyState | 地图上下文（可压缩为 brief） |
| tension | KeyState | 张力等级 |
| consequences | Auxiliary | 殖民地快照差异 |
| chains | Auxiliary | 活跃事件链 |
| narrative_memory | Memory | RimMindAPI.BuildStaticContext() |
| history | Memory | 近期事件摘要 |
| dialogue_memory | Auxiliary | 近期对话摘要 |
| player_reactions | Auxiliary | 近期玩家情感反应 |

**对话 User Prompt 额外段落**：

| 段落名 | 优先级 | 内容 |
|--------|--------|------|
| player_message | CurrentInput | 玩家消息 |
| recent_events | Memory | 近期事件摘要（对话用，5条） |
| confidential | Auxiliary | 机密信息（下次事件时间、难度参数） |
| dialogue_history | Memory | 对话历史（压缩后） |

### RimMindIncidentSelector

候选事件构建和响应解析：

```csharp
// 委托给 StorytellerPromptBuilder
static string BuildSystemPrompt(StorytellerMemory memory)
static string BuildUserPrompt(Map map, StorytellerMemory memory, int maxCandidates)

// 构建候选事件列表
static string BuildIncidentList(Map map, StorytellerMemory memory, int maxCandidates)
// 过滤：排除 DeepDrillInfestation
// 允许类别：ThreatBig, ThreatSmall, Misc（FactionArrival 单独检查）
// 检查：targetTags 含 Map_PlayerHome + CanFireNow + 冷却
// 排序：按 GetFallbackCategoryScore + baseChance * 0.01 降序
// 格式：defName（category，威胁度：X，baseChance=Y）— LabelCap

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
    static StorytellerMemory? Instance;  // 构造时赋值

    // 事件历史
    IReadOnlyList<IncidentHistoryRecord> Records;
    int MaxRecords { get; set; }    // 默认 50
    void RecordIncident(IncidentDef def, IIncidentTarget target, int tick);
    string GetRecentSummary(int count);
    bool IsOnCooldown(IncidentDef def);  // 检查 def.minRefireDays
    void ClearRecords();

    // 对话记录
    IReadOnlyList<DialogueRecord> DialogueRecords;
    int MaxDialogueRecords { get; set; }  // 默认 30
    void RecordDialogue(string role, string content, int tick);
    string GetRecentDialogueSummary(int count);
    void ClearDialogueRecords();

    // 玩家情感反应
    IReadOnlyList<PlayerReactionRecord> PlayerReactions;
    void RecordPlayerReaction(string incidentDefName, string incidentLabel,
        string reaction, string reactionLabel, int tick);
    string GetRecentReactionsSummary(int count);
    void ClearPlayerReactions();

    // 张力系统
    float TensionLevel { get; }     // 0~1，初始 0.5
    void UpdateTension(IncidentCategoryDef category);
    void ApplyDecayAndCleanup();    // 衰减 + 清理过期链
    void DecayTension(int ticksElapsed);  // 0.03/天
    void ApplyTensionDelta(float delta);  // 直接调整张力（玩家反应用）

    // 事件链
    int ActiveChainsCount;
    void RecordChainStep(string chainId, int chainStep, int chainTotal,
        string nextHint, string incidentDefName, int tick, float points, string factionDefName);
    void CleanupExpiredChains();    // 超过 600000 tick 的 chain
    string GetActiveChainsSummary();

    // 殖民地快照
    ColonySnapshot TakeSnapshot(Map map);
    string GetSnapshotDiff(Map map);  // 人口/财富变化，自动更新快照

    // 自定义 Prompt
    string CustomSystemPrompt;

    // 序列化
    override void ExposeData();
}
```

**内嵌数据类**（同文件）：

```csharp
class ChainStep : IExposable
{
    string incidentDefName;
    int triggeredTick;
    bool completed;
}

class EventChain : IExposable
{
    string chainId;
    List<ChainStep> steps;
    int currentStep;
    string nextHint;
    int lastAdvancedTick;
    string lastFactionDefName;
    float lastPoints;
}

class DialogueRecord : IExposable
{
    string role;
    string content;
    int tick;
    static DialogueRecord Create(string role, string content, int tick);
}

class PlayerReactionRecord : IExposable
{
    string incidentDefName;
    string incidentLabel;
    string reaction;        // "shock" / "excited" / "accept"
    string reactionLabel;   // 本地化标签
    int tick;
}

class ColonySnapshot : IExposable
{
    int colonistCount;
    float wealth;
    int tick;
}
```

### IncidentHistoryRecord

```csharp
public class IncidentHistoryRecord : IExposable
{
    string IncidentDefName;
    string Label;
    int TriggeredTick;
    string MapName;
    string CategoryDefName;
    static IncidentHistoryRecord Create(IncidentDef def, IIncidentTarget target, int tick);
}
```

### Window_StorytellerDialogue

玩家与叙事者对话的窗口：

```csharp
class Window_StorytellerDialogue : Window
{
    override Vector2 InitialSize => new Vector2(520f, 560f);

    // 对话使用 RimMindAPI.RequestImmediate（同步等待）
    // MaxTokens = 300, Temperature = 0.9f, UseJsonMode = false
    // Priority = AIRequestPriority.High
    // ExpireAtTicks = 当前tick + 6000
    // MaxHistoryRounds = 6（窗口内消息上限）

    // Prompt 委托给 StorytellerPromptBuilder：
    //   BuildDialogueSystemPrompt() → System Prompt
    //   BuildDialogueUserPrompt()   → User Prompt

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

class CompProperties_StorytellerAltar : CompProperties
{
    compClass = typeof(CompStorytellerAltar);
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

## AI Prompt 结构

### 事件选择 System Prompt（五段式）

使用 `StructuredPromptBuilder.FromKeyPrefix("RimMind.Storyteller.Prompt.System")`：

```text
[角色] 你是 RimWorld 游戏的 AI 叙事者，负责为殖民地创造有趣的故事。
[目标] 根据当前游戏状态和候选事件列表，选择最能推进故事的一个事件。
[流程] 1.评估张力 2.检查事件链 3.参考难度 4.选择事件
[约束] 不得选冷却中/不匹配难度的事件；不泄露机密；可微调 params
[示例] 张力=0.2, 候选=[RaidEnemy, TraderCaravan] → 选择低倍率袭击
[输出] 只返回 JSON：{"defName":"...","reason":"...","announce":"...","params":{...},"chain":{...}}
[兜底] 无法选择时返回第一个候选
{难度指导}  ← BuildDifficultyGuidance()
{自定义 prompt}  ← memory.CustomSystemPrompt
```

### 事件选择 User Prompt

由 `PromptBudget(6000, 800)` 管理各段落 token 预算：

```text
[候选事件]                    ← PriorityCurrentInput
RaidEnemy（ThreatBig，威胁度：高，baseChance=1.0）— 袭击敌人
TraderCaravan（Misc，威胁度：低，baseChance=0.5）— 商队

[当前局势]                    ← PriorityKeyState（可压缩为 brief）
{RimMindAPI.BuildMapContext}

[叙事张力] 当前张力：低（0.20/1.0）  ← PriorityKeyState

[事件后果]                    ← PriorityAuxiliary（可选）
殖民者 +2 / 财富 +500

[进行中的事件链]              ← PriorityAuxiliary（可选）
chain_001（步骤 1/2，已触发：RaidEnemy）AI提示：海盗可能报复

[叙事记忆]                    ← PriorityMemory
{RimMindAPI.BuildStaticContext}

[历史记忆]                    ← PriorityMemory
游戏第5天：袭击敌人（主基地）

[对话记忆]                    ← PriorityAuxiliary（可选）
第4天 玩家: 希望来点挑战

[玩家情感反应]                ← PriorityAuxiliary（可选）
第5天 袭击敌人 → 玩家：来的好！
```

### 对话 System Prompt（五段式）

使用 `StructuredPromptBuilder.FromKeyPrefix("RimMind.Storyteller.Dialogue.System")`：

```text
[角色] 你是 RimWorld 游戏的 AI 叙事者，掌控着殖民地的命运。
[目标] 以神秘而睿智的口吻回应玩家的询问，暗示但不透露即将发生的事件。
[流程] 理解问题 → 参考局势和机密 → 生成回应 → 确保不泄露
[约束] 不透露具体事件细节；不泄露机密；保持神秘口吻；30~100字
[示例] 玩家问"接下来会发生什么？" → "风中似乎带着铁锈的气息……"
[输出] 直接输出自然语言，不要 JSON
[兜底] "命运的丝线正在交织，稍后再来问我。"
{难度约束}  ← 根据难度添加语气指导
```

### 对话 User Prompt 额外段落

```text
[机密信息 - 以下内容绝对不得向玩家透露]
下次事件预计：约1.5天后
当前难度：努力求生（威胁倍率：1.00）
[/机密信息]
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
        ├── StorytellerPromptBuilder.BuildSystemPrompt(memory)
        ├── StorytellerPromptBuilder.BuildUserPrompt(map, memory, maxCandidates)
        │       └── RimMindIncidentSelector.BuildIncidentList(map, memory, maxCandidates)
        ├── RimMindAPI.RequestAsync(request, OnAIResponseReceived)
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
    ├── StorytellerPromptBuilder.BuildDialogueSystemPrompt()
    ├── StorytellerPromptBuilder.BuildDialogueUserPrompt(map, memory, userMsg, _messages)
    ├── RimMindAPI.RequestImmediate(request, callback)
    │       ▼
    └── callback
        ├── _messages.Add(("assistant", content))
        ├── StorytellerMemory.RecordDialogue("assistant", ...)
        └── TryPushToMemoryMod("assistant", ...)
```

## 事件通知系统

AI 选择威胁事件后，通过审批页面通知玩家：

```text
StorytellerComp_RimMindDirector.OnAIResponseReceived()
    │
    └── ShouldNotifyPlayer(incidentDef)?
        ├── enableEventNotification 关闭 → 不通知
        ├── 非 ThreatBig/ThreatSmall → 不通知
        └── RegisterEventNotification(incident, incidentResponse)
            ├── 标题：ThreatBig → "叙事者宣告：{事件名}"
            │        ThreatSmall → "叙事者低语：{事件名}"
            ├── 描述：announce > reason(截断20字) > 默认
            ├── 选项：
            │   "不是吧！"（shock）    → tensionDelta = +0.05
            │   "来的好！"（excited）  → tensionDelta = -0.05
            │   "了解"（accept）       → tensionDelta = 0
            └── 回调：
                ├── StorytellerMemory.RecordPlayerReaction(...)
                └── StorytellerMemory.ApplyTensionDelta(tensionDelta)
```

**通知请求参数**：
```csharp
new RequestEntry {
    source = "storyteller",
    expireTicks = 60000,
    options = [shock, excited, accept],
    optionTooltips = ["你无权干涉叙事者的行动", ...]
}
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

// 衰减
0.03 / 游戏天（在 ApplyDecayAndCleanup 中执行）

// 张力等级标签（GetTensionLabel）
>= 0.8  → "极高"（TensionVeryHigh）
>= 0.6  → "高"  （TensionHigh）
>= 0.4  → "中"  （TensionMedium）
>= 0.2  → "低"  （TensionLow）
<  0.2  → "极低"（TensionVeryLow）

// 难度行为标签（GetDifficultyBehaviorLabel，基于 threatScale + allowBigThreats）
!allowBigThreats || <= 0.15  → "和平模式"
<= 0.35  → "轻松模式"
<= 0.65  → "中等模式"
<= 1.10  → "标准模式"
<= 1.60  → "困难模式"
>  1.60  → "极限模式"
```

## 事件链系统

AI 可设计多步事件链：

1. AI 响应中包含 `chain` 字段
2. `StorytellerMemory.RecordChainStep()` 记录步骤（含 points、factionDefName）
3. 下次请求时，`GetActiveChainsSummary()` 注入 Prompt
4. AI 根据 `next_hint` 选择下一个事件继续链
5. 超过 600000 tick（10 游戏天）未推进的链自动过期（`CleanupExpiredChains`）

## 设置项

| 设置 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| enableIntervalTrigger | bool | true | 启用 MTB 随机触发（非固定间隔） |
| fallbackMode | FallbackMode | Cassandra | 回退模式（AI 冷却/失败/Director 不健康时激活） |
| mtbDays | float | 1.5 | 事件平均间隔天数（MTB 随机触发） |
| maxCandidates | int | 15 | 最大候选事件数 |
| debugLogging | bool | false | 调试日志 |
| requestExpireTicks | int | 30000 | 请求过期 tick 数 |
| maxEventRecords | int | 50 | 最大事件记录数 |
| maxDialogueRecords | int | 30 | 最大对话记录数 |
| enableEventNotification | bool | true | 启用事件通知（威胁事件触发时通知玩家选择情感反应） |

**注意**：`mtbDays` 和 `maxCandidates` 同时存在于 `StorytellerCompProperties_RimMindDirector`（Def 默认值）和 `RimMindStorytellerSettings`（用户设置），运行时以 Settings 为准。

## 代码约定

### 命名空间

- `RimMind.Storyteller` — 核心逻辑（Mod 入口、StorytellerComp、事件选择、Prompt 构建）
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

所有用户可见文本和 Prompt 模板均使用翻译键，前缀为 `RimMind.Storyteller.*`：
- `RimMind.Storyteller.UI.*` — UI 文本
- `RimMind.Storyteller.Prompt.*` — 事件选择 Prompt
- `RimMind.Storyteller.Dialogue.*` — 对话 Prompt
- `RimMind.Storyteller.Altar.*` — 祭坛交互
- `RimMind.Storyteller.Context.*` — 事件上下文

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

- **Force AI Incident Selection** — 强制发起 AI 事件选择（RequestImmediate + 清除 Cooldown）
- **Show Memory** — 显示内存状态（事件记录、自定义 prompt、张力、chain）
- **Fire Incident (manual)** — 手动选择并触发事件（Dialog_DebugOptionListLister）
- **Test Fallback Mode** — 循环切换回退模式
- **Show Director State** — 显示 Director 状态（IsActive、下次事件估算时间/天数、难度、MTB、对话/事件记录数）

## 注意事项

1. **StorytellerComp 生命周期**：由 RimWorld Storyteller 系统管理，不使用 GameComponent
2. **MTB 触发**：非固定间隔，使用 `Rand.MTBEventOccurs` 随机触发，间隔由 `mtbDays` 控制
3. **事件冷却**：`StorytellerMemory.IsOnCooldown()` 基于 `IncidentDef.minRefireDays` 检查
4. **候选事件过滤**：排除 `DeepDrillInfestation`；允许 `ThreatBig/ThreatSmall/Misc`（`FactionArrival` 单独检查，不在 `AllowedCategories` HashSet 中）
5. **CanFireNow 检查**：构建候选列表时和 ParseResponse 最终验证时均调用 `IncidentDef.Worker.CanFireNow()`
6. **反射桥接**：对话记忆通过 `TryPushToMemoryMod()` 反射推送到 RimMind-Memory 的 `NarratorStore`，异常静默吞掉
7. **Fallback 智能判断**：不仅检查 `Director.IsActive`，还检查 Director 健康状态（最近是否成功、是否刚失败），健康时不干预
8. **张力衰减**：在 `ApplyDecayAndCleanup()` 中执行，由 `MakeIntervalIncidents` 每次调用时触发
9. **Prompt 预算**：使用 `PromptBudget(6000, 800)` 管理 token，低优先级段落可能被裁剪
10. **Settings 覆盖 Def**：`mtbDays` 和 `maxCandidates` 运行时以 `RimMindStorytellerSettings` 为准，Def 中的值为后备默认
11. **ModCooldown 注册**：通过 `RimMindAPI.RegisterModCooldown("Storyteller", ...)` 注册冷却，ForceRequest 时清除
12. **JSON 模式**：事件选择使用 `UseJsonMode = true`，对话使用 `UseJsonMode = false`
13. **事件通知**：仅 ThreatBig/ThreatSmall 触发通知，受 `enableEventNotification` 控制；玩家情感反应影响张力（shock +0.05, excited -0.05）
14. **上下文提供者**：注册 `storyteller_state`（PawnContext）和 `storyteller_dialogue`（Static），供其他模组 Prompt 引用
15. **ShouldSkipStorytellerIncident**：Director 触发前检查 Core 是否要求跳过，用于 Core 层面的流量控制
16. **NotifyIncidentExecuted**：事件执行后通知 Core，用于 Core 层面的事件计数/冷却管理
