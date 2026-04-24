# RimMind - Storyteller

AI 驱动的叙事者，基于殖民地历史和当前局势，通过 LLM 选择最具戏剧性的随机事件，创造连贯的叙事体验。

## RimMind 是什么

RimMind 是一套 AI 驱动的 RimWorld 模组套件，通过接入大语言模型（LLM），让殖民者拥有人格、记忆、对话和自主决策能力。

## 子模组列表与依赖关系

| 模组 | 职责 | 依赖 | GitHub |
|------|------|------|--------|
| RimMind-Core | API 客户端、请求调度、上下文打包 | Harmony | [RimMind-Core 仓库](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Core) |
| RimMind-Actions | AI 控制小人的动作执行库 | Core | [RimMind-Actions 仓库](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Actions) |
| RimMind-Advisor | AI 扮演小人做出工作决策 | Core, Actions | [RimMind-Advisor 仓库](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Advisor) |
| RimMind-Dialogue | AI 驱动的对话系统 | Core | [RimMind-Dialogue 仓库](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Dialogue) |
| RimMind-Memory | 记忆采集与上下文注入 | Core | [RimMind-Memory 仓库](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Memory) |
| RimMind-Personality | AI 生成人格与想法 | Core | [RimMind-Personality 仓库](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Personality) |
| **RimMind-Storyteller** | **AI 叙事者，智能选择事件** | Core | [RimMind-Storyteller 仓库](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Storyteller) |

```
Core ── Actions ── Advisor
  ├── Dialogue
  ├── Memory
  ├── Personality
  └── Storyteller
```

## 安装步骤

### 从源码安装

**Linux/macOS:**
```bash
git clone git@github.com:RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Storyteller.git
cd RimWorld-RimMind-Mod-Storyteller
./script/deploy-single.sh <your RimWorld path>
```

**Windows:**
```powershell
git clone git@github.com:RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Storyteller.git
cd RimWorld-RimMind-Mod-Storyteller
./script/deploy-single.ps1 <your RimWorld path>
```

### 从 Steam 安装

1. 安装 [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) 前置模组
2. 安装 RimMind-Core
3. 安装 RimMind-Storyteller
4. 在模组管理器中确保加载顺序：Harmony → Core → Storyteller

## 快速开始

### 填写 API Key

1. 启动游戏，进入主菜单
2. 点击 **选项 → 模组设置 → RimMind-Core**
3. 填写你的 **API Key** 和 **API 端点**
4. 填写 **模型名称**（如 `gpt-4o-mini`）
5. 点击 **测试连接**，确认显示"连接成功"

### 选择 AI 叙事者

1. 新建游戏或载入存档
2. 在叙事者选择界面选择 **"RimMind Director"**
3. AI 将根据殖民地历史和当前局势智能选择事件

## 核心功能

### AI 事件选择

RimMind Director 是一个全新的叙事者，取代传统的 Cassandra/Randy/Phoebe：

- **MTB 随机触发**：按平均间隔随机向 AI 发送当前局势
- **智能选择**：AI 根据剧情连贯性、挑战平衡、戏剧性选择最佳事件
- **历史记忆**：记录已触发事件，避免重复和冷却期冲突
- **难度感知**：根据 threatScale、allowBigThreats 等六档难度指导 AI 行为
- **事件通知**：AI 选择威胁事件时通知玩家，玩家可选择情感反应影响叙事张力

### 事件链系统

AI 可以创建多步连锁事件，通过 chain_id 和 chain_step 控制。例如：先来一波小袭击试探，再来一波大袭击收尾。超过 10 游戏天未推进的链自动过期。

### 张力系统

追踪殖民地当前的紧张程度（0~1，初始 0.5），事件触发时增减（ThreatBig +0.25, ThreatSmall +0.12, Misc -0.05, FactionArrival -0.08），自然衰减 0.03/天。AI 根据张力水平调整事件强度，实现张弛有度的叙事节奏。

### Fallback 机制

AI 不可用或 Director 不健康（近期无成功或刚失败）时，自动切换到经典叙事者模式：

| Fallback 模式 | MTB 天数 | 特点 |
|-------------|----------|------|
| Cassandra | 4.6 | 渐进式难度，固定 ThreatBig |
| Randy | 1.35 | 完全随机，30% ThreatBig / 30% ThreatSmall / 40% Misc |
| Phoebe | 8.0 | 友好型，40% FactionArrival / 60% ThreatSmall |
| None | - | 禁用 Fallback，纯 AI 驱动 |

### 叙事者对话

通过祭坛建筑与叙事者对话，AI 以神秘睿智的口吻回应。对话含机密信息段落（下次事件时间、难度参数），AI 严格遵守不泄露约束。对话记录自动推送到 StorytellerMemory 和 RimMind-Memory（如已安装）。

### 事件通知

AI 选择威胁事件时，通过审批页面通知玩家。ThreatBig 显示"叙事者宣告"，ThreatSmall 显示"叙事者低语"。玩家可选择情感反应：

- "不是吧！"（shock）— 增加叙事张力（+0.05）
- "来的好！"（excited）— 降低叙事张力（-0.05）
- "了解"（accept）— 不影响张力

反应将记录到叙事者记忆，影响未来事件选择。可在设置中关闭此功能。

### 自定义叙事风格

通过设置页的"叙事者风格 Prompt"自定义 AI 行为，例如"你是一个冷酷的叙事者，喜欢制造极端困境"。

## 设置项

| 设置 | 默认值 | 说明 |
|------|--------|------|
| 定时触发 | 开启 | 按 MTB 随机间隔触发 AI 事件选择，关闭后仍可手动触发 |
| Fallback 模式 | Cassandra | AI 冷却/失败/Director 不健康时的备用行为 |
| 事件平均间隔 | 1.5 游戏天 | AI 评估频率（MTB 随机触发，0.5~10 天可调） |
| 候选事件数上限 | 15 | 每次评估的候选事件数量（5~25 可调） |
| 请求过期 | 0.5 游戏天 | AI 请求超时自动取消（0.06~2 天可调） |
| 事件记录上限 | 50 | 保留的最近事件记录数量（10~100 可调） |
| 对话记录上限 | 30 | 保留的最近对话记录数量（5~60 可调） |
| 玩家反应记录上限 | 20 | 保留的玩家情感反应记录数量（5~50 可调） |
| 事件链过期 | 10 天 | 超过此时间未推进的事件链自动移除（3~30 天可调） |
| 张力衰减 | 0.03/天 | 每游戏日衰减的叙事张力值（0.01~0.10 可调） |
| 叙事者风格 Prompt | - | 追加到系统 Prompt 的自定义指令 |
| 事件通知 | 开启 | 威胁事件触发时通知玩家选择情感反应 |
| 详细日志 | 关闭 | 输出 AI 选择过程到 Player.log |

## 常见问题

**Q: 必须新建游戏才能用吗？**
A: 不需要。载入已有存档后，在叙事者选择界面切换为 RimMind Director 即可。

**Q: AI 不可用时会怎样？**
A: 自动切换到 Fallback 模式（默认 Cassandra），游戏不会中断。配置好 API 后 AI 自动接管。

**Q: AI 会选择过于极端的事件吗？**
A: AI 会根据殖民地当前状态和难度设置调整事件强度。张力系统确保不会连续触发灾难性事件。你也可以通过自定义 Prompt 限制 AI 行为。

**Q: 可以和原版叙事者共存吗？**
A: RimMind Director 是独立的叙事者，选择它后替代原版叙事者。切换回原版叙事者即可恢复。

**Q: 配合 Memory 模组效果更好吗？**
A: 是的。Memory 提供殖民地历史记忆，Storyteller 参考这些记忆做出更有叙事连贯性的事件选择。对话记录也会自动推送到 Memory 模组。

**Q: 触发间隔是固定的吗？**
A: 不是。使用 MTB（Mean Time Between）随机触发机制，类似原版叙事者。1.5 天是平均间隔，实际触发时间有随机波动。

## 致谢

本项目开发过程中参考了以下优秀的 RimWorld 模组：

- [RimTalk](https://github.com/jlibrary/RimTalk.git) - 对话系统参考
- [RimTalk-ExpandActions](https://github.com/sanguodxj-byte/RimTalk-ExpandActions.git) - 动作扩展参考
- [NewRatkin](https://github.com/solaris0115/NewRatkin.git) - 种族模组架构参考
- [VanillaExpandedFramework](https://github.com/Vanilla-Expanded/VanillaExpandedFramework.git) - 框架设计参考

## 贡献

欢迎提交 Issue 和 Pull Request！如果你有任何建议或发现 Bug，请通过 GitHub Issues 反馈。

---

# RimMind - Storyteller (English)

An AI-driven storyteller that selects the most dramatic random events based on colony history and current situation via LLM, creating a coherent narrative experience.

## What is RimMind

RimMind is an AI-driven RimWorld mod suite that connects to Large Language Models (LLMs), giving colonists personality, memory, dialogue, and autonomous decision-making.

## Sub-Modules & Dependencies

| Module | Role | Depends On | GitHub |
|--------|------|------------|--------|
| RimMind-Core | API client, request dispatch, context packaging | Harmony | [RimMind-Core repo](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Core) |
| RimMind-Actions | AI-controlled pawn action execution | Core | [RimMind-Actions repo](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Actions) |
| RimMind-Advisor | AI role-plays colonists for work decisions | Core, Actions | [RimMind-Advisor repo](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Advisor) |
| RimMind-Dialogue | AI-driven dialogue system | Core | [RimMind-Dialogue repo](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Dialogue) |
| RimMind-Memory | Memory collection & context injection | Core | [RimMind-Memory repo](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Memory) |
| RimMind-Personality | AI-generated personality & thoughts | Core | [RimMind-Personality repo](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Personality) |
| **RimMind-Storyteller** | **AI storyteller, smart event selection** | Core | [RimMind-Storyteller repo](https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Storyteller) |

## Installation

### Install from Source

**Linux/macOS:**
```bash
git clone git@github.com:RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Storyteller.git
cd RimWorld-RimMind-Mod-Storyteller
./script/deploy-single.sh <your RimWorld path>
```

**Windows:**
```powershell
git clone git@github.com:RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Storyteller.git
cd RimWorld-RimMind-Mod-Storyteller
./script/deploy-single.ps1 <your RimWorld path>
```

### Install from Steam

1. Install [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
2. Install RimMind-Core
3. Install RimMind-Storyteller
4. Ensure load order: Harmony → Core → Storyteller

## Quick Start

### API Key Setup

1. Launch the game, go to main menu
2. Click **Options → Mod Settings → RimMind-Core**
3. Enter your **API Key** and **API Endpoint**
4. Enter your **Model Name** (e.g., `gpt-4o-mini`)
5. Click **Test Connection** to confirm

### Select AI Storyteller

1. Start a new game or load a save
2. Select **"RimMind Director"** in the storyteller selection screen
3. AI will intelligently select events based on colony history and current situation

## Key Features

- **AI Event Selection**: RimMind Director replaces traditional storytellers with LLM-powered event selection using MTB random trigger
- **Event Chain System**: AI can create multi-step chained events for narrative arcs (chains expire after 10 game days)
- **Tension System**: Tracks colony tension (0~1, initial 0.5), adjusting event intensity for dramatic pacing (ThreatBig +0.25, ThreatSmall +0.12, Misc -0.05, FactionArrival -0.08, decay 0.03/day)
- **Difficulty Awareness**: Six tiers of behavioral guidance from Peaceful to Extreme based on threatScale and allowBigThreats
- **Fallback Mechanism**: Automatically switches to classic storyteller mode when AI is unavailable or Director is unhealthy
- **Storyteller Dialogue**: Chat with the storyteller via the Altar building, with confidential sections AI strictly won't leak
- **Event Notification**: When AI selects a threat event, notify the player to choose an emotional reaction that affects narrative tension
- **Custom Narrative Style**: Define AI behavior through custom prompts

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Interval Trigger | On | Trigger AI event selection at MTB-based random intervals; manual trigger still works when off |
| Fallback Mode | Cassandra | Backup behavior when AI cooling down / failed / Director unhealthy |
| Avg. Event Interval | 1.5 game days | AI evaluation frequency (MTB random trigger, 0.5~10 days) |
| Max Candidates | 15 | Number of candidate events per AI evaluation (5~25) |
| Request Expiry | 0.5 game days | Auto-cancel AI requests after timeout (0.06~2 days) |
| Max Event Records | 50 | Number of recent event records kept (10~100) |
| Max Dialogue Records | 30 | Number of recent dialogue records kept (5~60) |
| Max Player Reactions | 20 | Number of player emotional reaction records kept (5~50) |
| Chain Expiry | 10 days | Event chains not advanced within this time are removed (3~30 days) |
| Tension Decay | 0.03/day | Amount of narrative tension that decays per game day (0.01~0.10) |
| Style Prompt | - | Custom instruction appended to system prompt |
| Event Notification | On | Notify player to choose emotional reaction when threat events are selected |
| Verbose Logging | Off | Output AI selection details to Player.log |

## FAQ

**Q: Do I need a new game?**
A: No. Load an existing save and switch to RimMind Director in the storyteller selection screen.

**Q: What happens when AI is unavailable?**
A: Automatically switches to Fallback mode (default: Cassandra). Game continues normally. AI takes over once API is configured.

**Q: Will AI choose extreme events?**
A: AI adjusts event intensity based on colony state and difficulty settings. The tension system prevents consecutive catastrophic events. You can also limit AI behavior via custom prompts.

**Q: Can it coexist with vanilla storytellers?**
A: RimMind Director is an independent storyteller. Selecting it replaces the vanilla one. Switch back to restore the original.

**Q: Does it work better with Memory?**
A: Yes. Memory provides colony history, and Storyteller references these memories for more narratively coherent event selection. Dialogue records are also automatically pushed to Memory.

**Q: Is the trigger interval fixed?**
A: No. It uses MTB (Mean Time Between) random trigger mechanism, similar to vanilla storytellers. 1.5 days is the average; actual timing has random variation.

## Acknowledgments

This project references the following excellent RimWorld mods:

- [RimTalk](https://github.com/jlibrary/RimTalk.git) - Dialogue system reference
- [RimTalk-ExpandActions](https://github.com/sanguodxj-byte/RimTalk-ExpandActions.git) - Action expansion reference
- [NewRatkin](https://github.com/solaris0115/NewRatkin.git) - Race mod architecture reference
- [VanillaExpandedFramework](https://github.com/Vanilla-Expanded/VanillaExpandedFramework.git) - Framework design reference

## Contributing

Issues and Pull Requests are welcome! If you have any suggestions or find bugs, please feedback via GitHub Issues.
