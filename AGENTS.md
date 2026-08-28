# AGENTS.md — RimMind-Storyteller

AI叙事者模块，替换RimWorld Storyteller系统，LLM决定事件选择。

## Start here

- 事件请求：`Source/Storyteller/README.md`
- 事件历史、对话、反应、张力和事件链：`Source/Memory/StorytellerMemory.cs`
- Context Provider 与模组注册：`Source/RimMindStorytellerMod.cs`
- 祭坛对话：`Source/UI/Window_StorytellerDialogue.cs`
- 调试操作：`Source/Debug/StorytellerDebugActions.cs`

## Main incident flow

`StorytellerComp_RimMindDirector` 保留 RimWorld 间隔触发门控，并把一次请求生命周期委托给 `StorytellerRequestCoordinator`。协调器复用 `StorytellerRequestState<FiringIncident>`、`RimMindIncidentSelector` 和 `IncidentSelectionPolicy`，玩家反应通知由 `StorytellerNotificationService` 构造。跨模组叙述记忆只走 `RimMindAPI.Memory`，不支持反射桥接。

## 项目定位

`StorytellerComp_RimMindDirector` MTB随机触发 → `StorytellerRequestCoordinator` → `RimMindAPI.Request.Send` → AI选择事件 → `RimMindIncidentSelector`验证 → 威胁事件通知玩家审批(影响张力) → 张力系统(0~1) + 事件链(chain) + 回退模式(Cassandra/Randy/Phoebe) + 祭坛对话(RimMindAPI.Chat) + StorytellerMemory持久化。

依赖: Core(编译期)。跨模组 Memory 访问通过 Core 公共 `RimMindAPI.Memory`。

## 构建

| 项 | 值 |
|----|-----|
| Target | net48, C#9.0, Nullable enable |
| Output | `../1.6/Assemblies/` |
| Assembly | RimMindStoryteller |
| 依赖 | RimMindCore.dll, Krafs.Rimworld.Ref, Lib.Harmony.Ref, Newtonsoft.Json |

## 源码结构

```
Source/
├── RimMindStorytellerMod.cs                                    Mod入口 + ContextKey注册(5个)
├── Agent/StorytellerAgentController.cs                         Storyteller-owned Agent 控制器
├── Storyteller/
│   ├── README.md                                                事件请求切片阅读地图
│   ├── StorytellerComp_RimMindDirector.cs                      RimWorld入口 + 间隔触发门控
│   ├── StorytellerRequestCoordinator.cs                        请求派发、回调终态与事件链记录
│   ├── StorytellerRequestState.cs                              Token、pending请求和pending结果状态
│   ├── StorytellerNotificationService.cs                       玩家反应通知与张力回调
│   ├── IncidentSelectionPolicy.cs                              事件选择与通知纯策略
│   ├── StorytellerComp_RimMindFallback.cs                      回退事件生成器
│   ├── StorytellerCompProperties_RimMindDirector.cs            Director Def属性
│   ├── StorytellerCompProperties_RimMindFallback.cs            Fallback Def属性
│   ├── RimMindIncidentSelector.cs                              响应解析入口(委托给 StorytellerResponseParserPure)
│   └── StorytellerResponseParserPure.cs                        JSON解析+修复统一入口(纯逻辑，可单测)
├── Memory/
│   ├── StorytellerMemory.cs                                    WorldComponent(事件/对话/反应/张力/链)
│   ├── IncidentHistoryRecord.cs                                事件历史记录(含存档兼容字段)
│   ├── TensionMath.cs                                          张力衰减/Clamp01 纯逻辑(可单测)
│   └── IncidentResponse.cs                                     IncidentResponse DTO
├── Extensions/
│   ├── PawnLookup.cs                                           共享Pawn查找(WorldPawns→FreeColonists)
│   ├── StorytellerContextBuilder.cs                            难度/威胁/张力文本构建(6个helper，3个纯逻辑可单测)
│   ├── StorytellerIncidentSkipCheck.cs                         ISkipCheck 实现
│   ├── StorytellerModCooldown.cs                               IModCooldown 实现
│   └── StorytellerSettingsTab.cs                               ISettingsTab Adapter
├── Settings/RimMindStorytellerSettings.cs + StorytellerSettingsTab.cs
├── UI/
│   ├── Window_StorytellerDialogue.cs                           祭坛对话窗口
│   └── Window_StorytellerAgentControl.cs                       Agent控制台窗口
├── Comps/CompStorytellerAltar.cs                               祭坛建筑组件
├── Patch/Patch_IncidentWorker_TryExecute.cs                    事件执行后置补丁
└── Debug/StorytellerDebugActions.cs
```

## 事件选择流程

```
StorytellerComp_RimMindDirector.MakeIntervalIncidents
  ├── target不是Map_PlayerHome → skip
  ├── 有pending结果 → yield return FiringIncident
  ├── 检查: API配置/enableIntervalTrigger/ShouldSkipStorytellerIncident/MTB随机触发
  └── StorytellerRequestCoordinator发起AI请求
      ├── ConsumeReactions(20) — 消费玩家反应
      ├── LlmRequestEnvelope(Scenario=Storyteller, MaxTokens=400, T=0.8)
      ├── RimMindAPI.Request.Send(envelope, callback)
      └── OnResponseReceived → ParseResponse → Publish → RecordChainStep → StorytellerNotificationService
```

## IncidentResponse DTO

```json
{"defName":"", "reason":"", "announce?":"", "params?":{"points_multiplier":0.3~2.0, "faction_hint":"", "raid_strategy_hint":""}, "chain?":{"chain_id":"", "chain_step":1, "chain_total":3, "next_hint":""}}
```

## 张力系统

初始0.5，事件影响: ThreatBig+0.25 / ThreatSmall+0.12 / Misc-0.05 / FactionArrival-0.08。玩家反应: shock+0.05 / excited-0.05。衰减: `tensionDecayPerDay`(默认0.03/天)。衰减唯一入口为 `ApplyDecayAndCleanup()`：`WorldComponentTick` 每 60000 tick 调用，`MakeIntervalIncidents` 亦调用，内部通过 `(now - _lastTensionDecayTick)` 计算经过 tick 数后交 `TensionMath.ComputeDecay` 执行。

## 回退模式

| 模式 | MTB天 | 策略 |
|------|-------|------|
| Cassandra | 4.6 | 固定ThreatBig |
| Randy | 1.35 | 30%Big/30%Small/40%Misc |
| Phoebe | 8.0 | 40%FactionArrival/60%Small |

## ContextKey 注册（全部使用新 API + 场景守卫）

| Key | Layer | Priority | 内容 |
|-----|-------|----------|------|
| storyteller_task | L0_Static | 0.95 | TaskInstruction(12段事件选择指令) |
| storyteller_context | L1_Baseline | 0.85 | 难度+威胁+张力+近期事件+活跃链 |
| storyteller_reactions | L1_Baseline | 0.8 | 玩家情感反应(ConsumedReactionsText) |
| storyteller_dialogue | L3_State | 0.5 | 近期对话摘要 |
| storyteller_recent_incidents | L4_History | 0.7 | Memory模组近期叙述(`RimMindAPI.Memory`) |

所有注册均包含 `if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();` 守卫。

## 上下文注入流程

```
ContextEngine.BuildContext(Scenario=Storyteller)
  ├── L0_Static: storyteller_task (TaskInstruction 12段指令)
  ├── L1_Baseline: storyteller_context (难度+威胁+张力+事件+链)
  ├── L1_Baseline: storyteller_reactions (玩家反应)
  ├── L2_Environment: Core自动注入 (地图状态、殖民者状态等)
  ├── L3_State: storyteller_dialogue (对话摘要)
  └── L4_History: storyteller_recent_incidents (Memory叙述) + Core自动注入 (NarrativeMemory)
```

## 跨模组 Memory 路径

| 方向 | 公共入口 | 调用方 |
|------|----------|--------|
| 写入叙述记忆 | `RimMindAPI.Memory.AddNarratorMemory` | `Window_StorytellerDialogue` |
| 读取近期叙述 | `RimMindAPI.Memory.GetRecentNarrations` | `RimMindStorytellerMod` Context Provider |

Storyteller 不访问 Memory 的具体 Store、WorldComponent 或设置单例。

## Invariants

- StorytellerComp通过XML `StorytellerDef` 注册(非GameComponent)
- 翻译键前缀: `RimMind.Storyteller.*`
- Harmony ID: `mcocdaa.RimMindStoryteller`
- `mtbDays` 运行时以Settings为准(覆盖Def)
- 间隔检查顺序保持为 target → maintenance → pending result → pending request → API → setting → skip check → MTB
- 过期请求Token必须忽略；成功/失败Tick只由 `StorytellerRequestState` 更新
- `StorytellerMemory` 持有事件历史、对话、反应、张力和事件链的持久状态
- 所有ContextKey注册必须包含场景守卫
- 禁止使用 `[Obsolete]` 的 `RegisterPawnContextProvider` / `RegisterStaticProvider`
- 禁止直接访问 `Core.Internal` 命名空间

## 已知问题

1. `IncidentHistoryRecord` 兼容字段 `_compat1`/`_compat2` 反序列化后未读取（存档兼容，可保留）

## 操作边界

### ✅ 必须做
- 修改事件选择逻辑后更新 `ParseResponse` 验证步骤
- 修改张力计算后验证0~1范围
- 新ContextKey注册必须包含场景守卫

### ⚠️ 先询问
- 修改 `mtbDays`(1.5) / `TensionLevel`初始值(0.5)
- 修改Fallback模式回退逻辑

### 🚫 绝对禁止
- 通过 `Core.Internal.AIRequestQueue.Instance` 直接清除冷却
- 使用 `[Obsolete]` 的 `RegisterPawnContextProvider` / `RegisterStaticProvider`
- ContextKey注册缺少场景守卫
- 后台线程调用 `Find.Storyteller` 或 `IncidentDef.Worker.CanFireNow`
