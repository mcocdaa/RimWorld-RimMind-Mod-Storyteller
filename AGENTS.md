# AGENTS.md — RimMind-Storyteller

AI叙事者模块，替换RimWorld Storyteller系统，LLM决定事件选择。

## 项目定位

`StorytellerComp_RimMindDirector` MTB随机触发 → ContextEngine(RequestStructured, SchemaRegistry.IncidentOutput) → AI选择事件 → ParseResponse验证 → 威胁事件通知玩家审批(影响张力) → 张力系统(0~1) + 事件链(chain) + 回退模式(Cassandra/Randy/Phoebe) + 祭坛对话(RimMindAPI.Chat) + StorytellerMemory持久化。

依赖: Core(编译期)，通过反射推送记忆到Memory的NarratorStore。

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
├── RimMindStorytellerMod.cs                                    Mod入口
├── Storyteller/
│   ├── StorytellerComp_RimMindDirector.cs                      AI事件选择器(StorytellerComp)
│   ├── StorytellerComp_RimMindFallback.cs                      回退事件生成器
│   └── RimMindIncidentSelector.cs                              响应解析(ParseResponse + IncidentResponse DTO)
├── Memory/StorytellerMemory.cs                                 WorldComponent(事件/对话/反应/张力/链)
├── Settings/RimMindStorytellerSettings.cs + StorytellerSettingsTab.cs
├── UI/Window_StorytellerDialogue.cs                            祭坛对话窗口
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
  └── 发起AI请求
      ├── ContextRequest(NpcId, Scenario=Storyteller, Budget, MaxTokens=400, T=0.8)
      ├── RimMindAPI.RequestStructured(request, SchemaRegistry.IncidentOutput, callback)
      └── OnAIResponse → ParseResponse → RecordChainStep → UpdateTension → RegisterEventNotification
```

## IncidentResponse DTO

```json
{"defName":"", "reason":"", "announce?":"", "params?":{"points_multiplier":0.3~2.0, "faction_hint":"", "raid_strategy_hint":""}, "chain?":{"chain_id":"", "chain_step":1, "chain_total":3, "next_hint":""}}
```

## 张力系统

初始0.5，事件影响: ThreatBig+0.25 / ThreatSmall+0.12 / Misc-0.05 / FactionArrival-0.08。玩家反应: shock+0.05 / excited-0.05。衰减: `tensionDecayPerDay`(默认0.03/天)。

## 回退模式

| 模式 | MTB天 | 策略 |
|------|-------|------|
| Cassandra | 4.6 | 固定ThreatBig |
| Randy | 1.35 | 30%Big/30%Small/40%Misc |
| Phoebe | 8.0 | 40%FactionArrival/60%Small |

## ContextKey 注册

| Key | Layer | Priority | 内容 |
|-----|-------|----------|------|
| storyteller_task | L0_Static | 0.95 | TaskInstruction(12段事件选择指令) |
| storyteller_context | L1_Baseline | 0.85 | 张力+近期事件+活跃链 |
| storyteller_reactions | L1_Baseline | 0.8 | 玩家情感反应 |

## 代码约定

- StorytellerComp通过XML `StorytellerDef` 注册(非GameComponent)
- 翻译键前缀: `RimMind.Storyteller.*`
- Harmony ID: `mcocdaa.RimMindStoryteller`
- `mtbDays` 运行时以Settings为准(覆盖Def)

## 已知问题

1. `storyteller_state`(旧API)和 `storyteller_context`(新API)提供重复数据，导致双重注入
2. `ForceRequest` 使用 `Core.Internal.AIRequestQueue.Instance`，应替换为 `RimMindAPI.ClearModCooldown`
3. 旧API `RegisterPawnContextProvider`/`RegisterStaticProvider` 已Obsolete，应迁移

## 操作边界

### ✅ 必须做
- 修改事件选择逻辑后更新 `ParseResponse` 验证步骤
- 修改张力计算后验证0~1范围
- 新ContextKey注册后删除对应旧API注册

### ⚠️ 先询问
- 修改 `mtbDays`(1.5) / `TensionLevel`初始值(0.5)
- 修改Fallback模式回退逻辑

### 🚫 绝对禁止
- 通过 `Core.Internal.AIRequestQueue.Instance` 直接清除冷却
- `storyteller_state` 和 `storyteller_context` 同时注册
- 后台线程调用 `Find.Storyteller` 或 `IncidentDef.Worker.CanFireNow`
