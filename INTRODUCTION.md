# RimMind - Storyteller

AI 叙事导演，根据殖民地历史和当前局势编织连贯的故事线。

## 核心能力

**历史感知事件选择** - AI 了解殖民地过去发生了什么，据此选择有叙事连贯性的下一个事件，而非纯随机。

**事件链系统** - AI 可以创建多步连锁事件（通过 chain_id / chain_step），编织有起承转合的故事弧线。超过 10 游戏天未推进的链自动过期。

**张力系统** - 追踪殖民地紧张程度（0~1，初始 0.5），事件触发时增减（ThreatBig +0.25, ThreatSmall +0.12, Misc -0.05, FactionArrival -0.08），自然衰减 0.03/天，实现张弛有度的叙事节奏。

**事件记忆系统** - 记录已发生事件的历史，包括时间、类型、地图，为 AI 决策提供时间线上下文。

**智能候选生成** - 向 AI 展示当前可触发事件列表（按 FallbackMode 加权排序），让 AI 从中选择最符合叙事逻辑的一个。

**难度感知** - AI 根据游戏难度设置（threatScale、allowBigThreats 等）调整事件选择强度，从和平到极限六档行为指导。

**Fallback 机制** - AI 不可用或 Director 不健康时自动切换到经典叙事者（Cassandra / Randy / Phoebe / None），游戏不会中断。

**叙事者对话** - 通过祭坛建筑与叙事者对话，AI 以神秘睿智的口吻回应，可暗示但不透露即将发生的事件。对话含机密信息段落（下次事件时间、难度参数），AI 严格遵守不泄露约束。

**殖民地快照** - 追踪人口和财富变化，事件后果差异注入 Prompt 供 AI 参考。

**自定义叙事风格** - 通过 Prompt 自定义 AI 的叙事偏好，从冷酷到温和任你选择。

## 叙事特色

- 悲剧之后可能迎来希望
- 长期平静后酝酿风暴
- 根据殖民地特点定制事件（农业殖民地遭遇作物病害）
- 避免重复同类事件造成疲劳
- 连锁事件创造有记忆点的故事线
- 难度自适应：和平模式温和友善，极限模式冷酷无情

## 设置项

| 设置 | 默认值 | 说明 |
|------|--------|------|
| 定时触发 | 开启 | 按 MTB 随机间隔触发 AI 事件选择 |
| Fallback 模式 | Cassandra | AI 冷却/失败/Director 不健康时的备用行为 |
| 事件平均间隔 | 1.5 游戏天 | AI 评估频率（MTB 随机触发） |
| 候选事件数上限 | 15 | 每次评估的候选事件数量 |
| 请求过期 | 0.5 游戏天 | AI 请求超时自动取消 |
| 事件记录上限 | 50 | 保留的最近事件记录数量 |
| 对话记录上限 | 30 | 保留的最近对话记录数量 |
| 叙事者风格 Prompt | - | 追加到系统 Prompt 的自定义指令 |
| 详细日志 | 关闭 | 输出 AI 选择过程到 Player.log |

## 建议配图

1. 叙事者选择界面（展示 RimMind Director）
2. AI 选择事件的调试日志截图
3. 事件历史记录界面
4. 叙事者祭坛建筑截图
5. 与叙事者对话窗口截图

---

# RimMind - Storyteller (English)

An AI narrative director weaving coherent storylines based on colony history and current situation.

## Key Features

**History-Aware Event Selection** - AI understands what has happened in the colony and chooses the next event with narrative coherence rather than pure randomness.

**Event Chain System** - AI can create multi-step chained events (via chain_id / chain_step), weaving story arcs with beginnings, developments, and climaxes. Chains expire after 10 game days of inactivity.

**Tension System** - Tracks colony tension level (0~1, initial 0.5), adjusting with events (ThreatBig +0.25, ThreatSmall +0.12, Misc -0.05, FactionArrival -0.08) and naturally decaying at 0.03/day, creating dramatic pacing with ebbs and flows.

**Event Memory System** - Records history of occurred events, including time, type, and map, providing timeline context for AI decisions.

**Smart Candidate Generation** - Shows AI a list of currently triggerable events (weighted by FallbackMode), letting AI choose the one that best fits narrative logic.

**Difficulty Awareness** - AI adjusts event selection intensity based on game difficulty settings (threatScale, allowBigThreats, etc.), with six tiers of behavioral guidance from Peaceful to Extreme.

**Fallback Mechanism** - Automatically switches to classic storyteller (Cassandra / Randy / Phoebe / None) when AI is unavailable or Director is unhealthy. Game never interrupts.

**Storyteller Dialogue** - Chat with the storyteller via the Altar building. AI responds in a mysterious and wise tone, hinting at but not revealing upcoming events. Dialogue includes confidential sections (next event time, difficulty parameters) that AI strictly follows not to leak.

**Colony Snapshot** - Tracks population and wealth changes, injecting event consequence diffs into the prompt for AI reference.

**Custom Narrative Style** - Define AI's narrative preferences through prompts, from ruthless to gentle.

## Narrative Features

- Hope may follow tragedy
- Storms brew after long calm
- Events tailored to colony characteristics (agricultural colony faces crop blight)
- Avoids repetition fatigue from similar events
- Chained events create memorable storylines
- Difficulty-adaptive: gentle and friendly in Peaceful, ruthless in Extreme

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Interval Trigger | On | Trigger AI event selection at MTB-based random intervals |
| Fallback Mode | Cassandra | Backup behavior when AI cooling down / failed / Director unhealthy |
| Avg. Event Interval | 1.5 game days | AI evaluation frequency (MTB random trigger) |
| Max Candidates | 15 | Number of candidate events per AI evaluation |
| Request Expiry | 0.5 game days | Auto-cancel AI requests after timeout |
| Max Event Records | 50 | Number of recent event records kept |
| Max Dialogue Records | 30 | Number of recent dialogue records kept |
| Style Prompt | - | Custom instruction appended to system prompt |
| Verbose Logging | Off | Output AI selection details to Player.log |

## Suggested Screenshots

1. Storyteller selection screen (showing RimMind Director)
2. Debug log of AI selecting events
3. Event history record interface
4. Storyteller Altar building
5. Storyteller dialogue window
