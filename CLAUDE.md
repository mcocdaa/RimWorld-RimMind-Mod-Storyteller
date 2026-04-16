# AGENTS.md — RimMind-Storyteller

本文件供 AI 编码助手阅读，描述 RimMind-Storyteller 的架构、代码约定和扩展模式。

## 项目定位

RimMind-Storyteller 是 RimMind AI 模组套件的 AI 叙事者模块。它替换/增强 RimWorld 的 Storyteller 系统，用 LLM 决定事件选择。

**核心职责**：
1. **AI 事件选择**：`StorytellerComp_RimMindDirector` 周期性调用 AI 选择下一个事件
2. **候选事件构建**：从当前可触发事件中筛选候选列表
3. **事件链系统**：支持多步事件链（chain），AI 可设计连续叙事
4. **张力系统**：跟踪当前张力等级（0~1），影响事件选择和衰减
5. **回退模式**：AI 不可用时，按经典 Storyteller 模式（Cassandra/Randy/Phoebe）生成事件
6. **叙事者对话**：玩家可通过祭坛建筑与叙事者对话
7. **记忆系统**：`StorytellerMemory` 持久化事件历史、对话记录、事件链、张力、殖民地快照

**依赖关系**：
- 依赖 RimMind-Core 提供的 API 和上下文构建
- 与 RimMind-Memory 松耦合（通过反射推送叙事记忆）

## 源码结构

```
Source/
├── RimMindStorytellerMod.cs                Mod 入口，注册 Harmony，初始化设置
├── Storyteller/
│   ├── StorytellerComp_RimMindDirector.cs  AI 驱动的事件选择器（StorytellerComp）
│   ├── StorytellerCompProperties_RimMindDirector.cs  属性定义
│   ├── StorytellerComp_RimMindFallback.cs  回退事件生成器
│   ├── StorytellerCompProperties_RimMindFallback.cs  属性定义
│   └── RimMindIncidentSelector.cs          Prompt 构建 + 响应解析
├── Memory/
│   ├── StorytellerMemory.cs                WorldComponent，全局单例
│   └── IncidentHistoryRecord.cs            事件历史记录
├── Settings/
│   ├── RimMindStorytellerSettings.cs       模组设置
│   └── StorytellerSettingsTab.cs           设置 UI 绘制
├── UI/
│   └── Window_StorytellerDialogue.cs       玩家与叙事者对话窗口
├── Comps/
│   └── CompStorytellerAltar.cs             祭坛建筑组件（打开对话窗口）
├── Patch/
│   └── Patch_IncidentWorker_TryExecute.cs  事件执行后置补丁
└── Debug/
    └── StorytellerDebugActions.cs          Dev 菜单调试动作
```

## 关键类与 API

### StorytellerComp_RimMindDirector

AI 驱动的事件选择器，继承 `StorytellerComp`：

```csharp
// 核心周期方法
override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
// 检查 pending 结果或发起新 AI 请求

// 状态查询
bool IsActive              // 有 pending 请求或结果
int GetEstimatedTicksUntilNextEvent()

// 强制请求（调试用）
bool ForceRequest(IIncidentTarget target)
```

**请求间隔**：`requestIntervalTicks`（默认 300000 = 5 游戏天）

### StorytellerComp_RimMindFallback

AI 不可用时的回退事件生成器：

```csharp
override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
// 当 Director 不活跃时，按 FallbackMode 生成事件
```

**回退模式**（`FallbackMode` 枚举）：

| 模式 | MTB 天数 | 事件类别倾向 |
|------|----------|-------------|
| Cassandra | 4.6 | ThreatBig/Misc |
| Randy | 1.35 | 随机 |
| Phoebe | 8.0 | Misc/FactionArrival |
| None | - | 不生成事件 |

### RimMindIncidentSelector

Prompt 构建和响应解析：

```csharp
// 构建 System Prompt
static string BuildSystemPrompt(StorytellerMemory memory)
// 包含：角色设定、难度指导、自定义 prompt

// 构建 User Prompt
static string BuildUserPrompt(Map map, StorytellerMemory memory, int maxCandidates)
// 包含：候选事件列表、局势、张力、事件链、历史、对话记忆

// 构建候选事件列表
static string BuildIncidentList(Map map, StorytellerMemory memory, int maxCandidates)
// 过滤：排除 DeepDrillInfestation
// 允许类别：ThreatBig, ThreatSmall, Misc, FactionArrival
// 检查：CanFireNow + 冷却

// 解析 AI 响应
static (FiringIncident?, IncidentResponse?) ParseResponse(
    string aiContent, IIncidentTarget target, StorytellerComp source)
```

### IncidentResponse（JSON DTO）

```csharp
public class IncidentResponse
{
    public string defName;            // IncidentDef 名称
    public string reason;             // AI 理由
    public IncidentParams? @params;   // 可选参数
    public ChainInfo? chain;          // 可选事件链信息
}

public class IncidentParams
{
    public float? points_multiplier;     // 点数倍率
    public string? faction_hint;         // 派系提示
    public string? raid_strategy_hint;   // 袭击策略提示
}

public class ChainInfo
{
    public string chain_id;       // 事件链 ID
    public int chain_step;        // 当前步骤
    public int chain_total;       // 总步骤数
    public string? next_hint;     // 下一步提示
}
```

### StorytellerMemory

全局单例 WorldComponent，存档持久化：

```csharp
public class StorytellerMemory : WorldComponent
{
    static StorytellerMemory? Instance;

    // 事件历史
    IReadOnlyList<IncidentHistoryRecord> Records;
    int MaxRecords { get; set; }    // 默认 50
    void RecordIncident(IncidentDef def, IIncidentTarget target, int tick);
    string GetRecentSummary(int count);
    bool IsOnCooldown(IncidentDef def);
    void ClearRecords();

    // 对话记录
    IReadOnlyList<DialogueRecord> DialogueRecords;
    int MaxDialogueRecords { get; set; }  // 默认 30
    void RecordDialogue(string role, string content, int tick);
    string GetRecentDialogueSummary(int count);
    void ClearDialogueRecords();

    // 张力系统
    float TensionLevel;             // 0~1
    void UpdateTension(IncidentCategoryDef category);
    void ApplyDecayAndCleanup();
    void DecayTension(int ticksElapsed);  // 0.03/天

    // 事件链
    int ActiveChainsCount;
    void RecordChainStep(string chainId, int chainStep, int chainTotal,
        string nextHint, string incidentDefName, int tick, float points, string factionDefName);
    void CleanupExpiredChains();    // 超过 600000 tick 的 chain
    string GetActiveChainsSummary();

    // 殖民地快照
    ColonySnapshot TakeSnapshot(Map map);
    string GetSnapshotDiff(Map map);  // 人口/财富变化

    // 自定义 Prompt
    string CustomSystemPrompt;

    // 序列化
    override void ExposeData();
}
```

**内嵌数据类**：

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
}

class ColonySnapshot : IExposable
{
    int colonistCount;
    float wealth;
    int tick;
}
```

### Window_StorytellerDialogue

玩家与叙事者对话的窗口：

```csharp
class Window_StorytellerDialogue : Window
{
    override Vector2 InitialSize => (520, 560);

    // 对话使用 RimMindAPI.RequestImmediate（非异步）
    // MaxTokens = 300, Temperature = 0.9f
    // MaxHistoryRounds = 6

    // 对话记录推送到 StorytellerMemory
    // 通过反射推送到 RimMindMemory 的 NarratorStore
}
```

### CompStorytellerAltar

祭坛建筑组件，提供打开对话窗口的 Gizmo：

```csharp
class CompStorytellerAltar : ThingComp
{
    override IEnumerable<Gizmo> CompGetGizmosExtra()
    // 返回一个 Command_Action，点击打开 Window_StorytellerDialogue
}
```

## AI Prompt 结构

### System Prompt

```
你是 RimWorld 的 AI 叙事者，负责选择下一个事件。
根据当前局势、张力等级和事件历史做出决策。
{难度指导}
{自定义 prompt}
```

### User Prompt

```
[当前局势]
{殖民地快照差异}
{张力等级}

[候选事件]
1. Raid (ThreatBig) [可触发] — 最近 5 天未发生
2. ResourcePodCrash (Misc) [可触发]
...

[活跃事件链]
chain_001: 步骤 1/3，下一步提示：...

[近期事件]
- 2天前：袭击（海盗团）
- 4天前：资源舱坠落

[对话记忆]
- 玩家：希望来点挑战
- 叙事者：好的，我会增加威胁

选择一个事件，返回 JSON：
<Incident>
{"defName": "Raid", "reason": "...", "params": {"points_multiplier": 1.2}, "chain": {"chain_id": "chain_001", "chain_step": 1, "chain_total": 3, "next_hint": "..."}}
</Incident>
```

## 数据流

```
StorytellerComp_RimMindDirector.MakeIntervalIncidents()
    │
    ├── 检查是否有 pending 结果
    │   ├── 有 → 返回 FiringIncident
    │   └── 无 → 发起新请求
    │       ▼
    ├── RimMindIncidentSelector.BuildSystemPrompt()
    ├── RimMindIncidentSelector.BuildUserPrompt()
    │       ▼
    ├── RimMindAPI.RequestAsync()
    │       ▼
    ├── OnAIResponseReceived()
    │       ▼
    ├── RimMindIncidentSelector.ParseResponse()
    │       ├── 解析 <Incident> JSON
    │       ├── 查找 IncidentDef
    │       ├── 构建 FiringIncident
    │       └── 记录 chain/tension
    │       ▼
    └── 返回 FiringIncident → RimWorld 事件系统执行
```

### 事件执行后处理

```
Patch_IncidentWorker_TryExecute.Postfix()
    │
    ├── StorytellerMemory.RecordIncident()
    ├── StorytellerMemory.UpdateTension()
    └── 可能触发 AI 对话（RimMindAPI.CanTriggerDialogue）
```

## 张力系统

```csharp
// 事件类别对张力的影响
ThreatBig    → +0.3
ThreatSmall  → +0.1
Misc         → -0.05
FactionArrival → -0.1

// 衰减
0.03 / 游戏天

// 张力等级标签
0.0~0.2  → "平静"
0.2~0.4  → "轻微紧张"
0.4~0.6  → "紧张"
0.6~0.8  → "高度紧张"
0.8~1.0  → "极限"
```

## 事件链系统

AI 可设计多步事件链：

1. AI 响应中包含 `chain` 字段
2. `StorytellerMemory.RecordChainStep()` 记录步骤
3. 下次请求时，`GetActiveChainsSummary()` 注入 Prompt
4. AI 根据 `next_hint` 选择下一个事件继续链
5. 超过 600000 tick（10 游戏天）未推进的链自动过期

## 设置项

| 设置 | 默认值 | 说明 |
|------|--------|------|
| enableIntervalTrigger | true | 启用周期触发 |
| fallbackMode | Cassandra | 回退模式 |
| requestIntervalTicks | 300000 | AI 请求间隔（5 游戏天） |
| maxCandidates | 15 | 最大候选事件数 |
| debugLogging | false | 调试日志 |
| requestExpireTicks | 30000 | 请求过期 |
| maxEventRecords | 50 | 最大事件记录数 |
| maxDialogueRecords | 30 | 最大对话记录数 |

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

通过 XML Def 注册，不是自动发现：

```xml
<StorytellerCompDef>
    <compClass>RimMind.Storyteller.StorytellerComp_RimMindDirector</compClass>
    <requestIntervalTicks>300000</requestIntervalTicks>
    <maxCandidates>15</maxCandidates>
</StorytellerCompDef>
```

### Harmony

- Harmony ID：`mcocdaa.RimMindStoryteller`
- Patch_IncidentWorker_TryExecute：事件执行后记录

### 构建

- 目标框架：`net48`
- C# 语言版本：9.0
- RimWorld 版本：1.6
- 输出路径：`../1.6/Assemblies/`

## 调试

Dev 菜单（需开启开发模式）→ RimMind Storyteller：

- **Force AI Incident Selection** — 强制发起 AI 事件选择
- **Show Memory** — 显示内存状态（事件记录、自定义 prompt、张力、chain）
- **Fire Incident (manual)** — 手动选择并触发事件
- **Test Fallback Mode** — 切换回退模式
- **Show Director State** — 显示 Director 组件状态（IsActive、下次事件估算、难度、间隔等）

## 注意事项

1. **StorytellerComp 生命周期**：由 RimWorld Storyteller 系统管理，不使用 GameComponent
2. **事件冷却**：`StorytellerMemory.IsOnCooldown()` 检查事件是否在冷却中
3. **候选事件过滤**：排除 `DeepDrillInfestation`，只允许 `ThreatBig/ThreatSmall/Misc/FactionArrival`
4. **CanFireNow 检查**：构建候选列表时调用 `IncidentDef.Worker.CanFireNow()`
5. **反射桥接**：对话记忆通过反射推送到 RimMind-Memory 的 NarratorStore
6. **Fallback 优先级**：当 Director.IsActive 为 true 时，Fallback 不生成事件
7. **张力衰减**：在 `ApplyDecayAndCleanup()` 中执行，应在合适的时机调用
