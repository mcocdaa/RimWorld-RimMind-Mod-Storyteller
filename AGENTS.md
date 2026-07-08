# AGENTS.md — RimMind-Storyteller

AI叙事者模块，替换RimWorld Storyteller系统，LLM决定事件选择。

## 项目定位

`StorytellerComp_RimMindDirector` MTB随机触发 → ContextEngine(RequestStructured, SchemaRegistry.IncidentOutput) → AI选择事件 → ParseResponse验证 → 威胁事件通知玩家审批(影响张力) → 张力系统(0~1) + 事件链(chain) + 回退模式(Cassandra/Randy/Phoebe) + 祭坛对话(RimMindAPI.Chat) + StorytellerMemory持久化。

依赖: Core(编译期)，通过反射推送/读取 Memory 模组数据。

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
├── RimMindStorytellerMod.cs                                    Mod入口 + ContextKey注册(5个) + Memory桥接委托注入
├── Agent/StorytellerAgentController.cs                         Storyteller-owned Agent 控制器
├── Storyteller/
│   ├── StorytellerComp_RimMindDirector.cs                      AI事件选择器(StorytellerComp)
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
│   ├── StorytellerMemoryBridge.cs                              Memory反射统一桥接(写入+读取 单一入口)
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
  └── 发起AI请求
      ├── ConsumeReactions(20) — 消费玩家反应
      ├── ContextRequest(NpcId, Scenario=Storyteller, Budget, MaxTokens=400, T=0.8)
      ├── RimMindAPI.RequestStructured(request, SchemaRegistry.IncidentOutput, callback)
      └── OnAIResponse → ParseResponse → RecordChainStep → RegisterEventNotification
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
| storyteller_recent_incidents | L4_History | 0.7 | Memory模组近期叙述(反射读取) |

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

## Memory 反射桥接（统一入口）

| 方法 | 方向 | 位置 |
|------|------|------|
| `StorytellerMemoryBridge.TryPushNarratorEntry` | 写入 | `Source/Extensions/StorytellerMemoryBridge.cs` |
| `StorytellerMemoryBridge.GetRecentNarrations` | 读取 | `Source/Extensions/StorytellerMemoryBridge.cs` |

调用方：
- `Window_StorytellerDialogue.TryPushToMemoryMod` → `StorytellerMemoryBridge.TryPushNarratorEntry`
- `RimMindStorytellerMod.GetRecentNarrationsFromMemory` → `StorytellerMemoryBridge.GetRecentNarrations`

⚠️ `IMemoryBridge` Core 接口仍待实施（见 `.trae/specs/clean-arch-compliance-audit/tasks.md` Task 10）。当前 `StorytellerMemoryBridge` 是 Storyteller 侧的统一封装，反射目标：`RimMind.Memory.Data.NarratorMemoryStore`（类名不可变，见 Memory mod 约束）。

## 代码约定

- StorytellerComp通过XML `StorytellerDef` 注册(非GameComponent)
- 翻译键前缀: `RimMind.Storyteller.*`
- Harmony ID: `mcocdaa.RimMindStoryteller`
- `mtbDays` 运行时以Settings为准(覆盖Def)
- 所有ContextKey注册必须包含场景守卫
- 禁止使用 `[Obsolete]` 的 `RegisterPawnContextProvider` / `RegisterStaticProvider`
- 禁止直接访问 `Core.Internal` 命名空间

## 已知问题

1. `IncidentHistoryRecord` 兼容字段 `_compat1`/`_compat2` 反序列化后未读取（存档兼容，可保留）
2. `IMemoryBridge` Core 接口未实施 — 当前由 `StorytellerMemoryBridge` 在 Storyteller 侧统一封装反射，待 Core 提供 `IMemoryBridge` 后切换（见 `.trae/specs/clean-arch-compliance-audit/tasks.md` Task 10）

## 已修复（2026-07-08）

| # | 问题 | 修复 | 提交 |
|---|------|------|------|
| 1 | 张力双重衰减 Bug | 张力衰减唯一入口收敛到 `ApplyDecayAndCleanup()`，内部委托 `TensionMath.ComputeDecay` | 09435b3 |
| 2 | 缺失翻译键（误报） | `RimMind.Storyteller.Prompt.PlayerReactions` 和 `RimMind.Storyteller.Prompt.RecentIncidents` 已在 English/ChineseSimplified XML 中定义（原 AGENTS.md 标注错误） | — |
| 3 | Memory 反射脆弱，2 条路径 | 抽取 `StorytellerMemoryBridge` 统一写入/读取入口 | 9343bad |
| 4 | JSON 修复双路径 | 统一到 `StorytellerResponseParserPure`，`RimMindIncidentSelector.ParseResponse` 仅委托 | e1494d1 |
| 5 | 死代码 `StorytellerIncidentExecutedListener` 空实现 | 删除空实现与注册 | c218cb3 |
| 6 | `budget` 未使用赋值 | 移除 3 处未使用赋值 | 5ed0fbb |
| 7 | `PawnLookup` 重复 | 提取 `PawnLookup.FindPawnById` 去重 4 处 | 61dffb7 |
| 8 | `StorytellerContextBuilder` 缺失 | 提取 6 个 helper 方法（3 个纯逻辑可单测） | fd9d054 |
| 9 | `CustomSystemPrompt` 未注入 | 接入 `storyteller_task` ContextKey | 6612b1a |

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
