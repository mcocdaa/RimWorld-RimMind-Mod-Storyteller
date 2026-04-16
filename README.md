# RimMind - Storyteller

AI 驱动的叙事者，基于殖民地历史和当前局势，通过 LLM 选择最具戏剧性的随机事件，创造连贯的叙事体验。

## RimMind 是什么

RimMind 是一套 AI 驱动的 RimWorld 模组套件，通过接入大语言模型（LLM），让殖民者拥有人格、记忆、对话和自主决策能力。

## 子模组列表与依赖关系

| 模组 | 职责 | 依赖 |
|------|------|------|
| RimMind-Core | API 客户端、请求调度、上下文打包 | Harmony |
| RimMind-Actions | AI 控制小人的动作执行库 | Core |
| RimMind-Advisor | AI 扮演小人做出工作决策 | Core, Actions |
| RimMind-Dialogue | AI 驱动的对话系统 | Core |
| RimMind-Memory | 记忆采集与上下文注入 | Core |
| RimMind-Personality | AI 生成人格与想法 | Core |
| **RimMind-Storyteller** | **AI 叙事者，智能选择事件** | Core |

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
git clone git@github.com:mcocdaa/RimWorld-RimMind-Mod-Storyteller.git
cd RimWorld-RimMind-Mod-Storyteller
./script/deploy-single.sh <your RimWorld path>
```

**Windows:**
```powershell
git clone git@github.com:mcocdaa/RimWorld-RimMind-Mod-Storyteller.git
cd RimWorld-RimMind-Mod-Storyteller
./script/deploy-single.ps1 <your RimWorld path>
```

### 从 Steam 安装

1. 安装 [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) 前置模组
2. 安装 RimMind-Core
3. 安装 RimMind-Storyteller
4. 在模组管理器中确保加载顺序：Harmony → Core → Storyteller

<!-- ![安装步骤](images/install-steps.png) -->

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

<!-- ![叙事者选择](images/screenshot-storyteller-select.png) -->

## 截图展示

<!-- ![AI事件选择](images/screenshot-storyteller-event.png) -->
<!-- ![事件历史](images/screenshot-storyteller-history.png) -->
<!-- ![叙事者祭坛](images/screenshot-storyteller-altar.png) -->

## 核心功能

### AI 事件选择

RimMind Director 是一个全新的叙事者，取代传统的 Cassandra/Randy/Phoebe：

- **定期评估**：定时向 AI 发送当前局势
- **候选事件**：从可触发事件中筛选候选
- **智能选择**：AI 根据剧情连贯性、挑战平衡、戏剧性选择最佳事件
- **历史记忆**：记录已触发事件，避免重复和冷却期冲突

### 事件链系统

AI 可以创建多步连锁事件，通过 chain_id 和 chain_step 控制。例如：先来一波小袭击试探，再来一波大袭击收尾。

### 张力系统

追踪殖民地当前的紧张程度（0~1），事件触发时增加，自然衰减。AI 根据张力水平调整事件强度，实现张弛有度的叙事节奏。

### Fallback 机制

AI 不可用时（未配置 API、请求失败），自动切换到经典叙事者模式：

| Fallback 模式 | 特点 |
|-------------|------|
| Cassandra | 渐进式难度，规律威胁 |
| Randy | 完全随机，大起大落 |
| Phoebe | 友好型，大量休整期 |
| None | 禁用 Fallback，纯 AI 驱动 |

### 叙事者祭坛

游戏内建筑，玩家可交互触发 AI 叙事，主动推动剧情发展。

### 自定义叙事风格

通过设置页的"叙事者风格 Prompt"自定义 AI 行为，例如"你是一个冷酷的叙事者，喜欢制造极端困境"。

## 设置项

| 设置 | 默认值 | 说明 |
|------|--------|------|
| 启用定时触发 | 开启 | 定期触发 AI 事件选择 |
| Fallback 模式 | Cassandra | AI 不可用时的备用行为 |
| 请求间隔 | 约 5 游戏天 | AI 评估频率 |
| 候选事件数上限 | 15 | 每次评估的候选事件数量 |
| 叙事者风格 Prompt | - | 追加到系统 Prompt 的自定义指令 |
| 详细日志 | 关闭 | 输出 AI 选择过程到 Player.log |

## 常见问题

**Q: 必须新建游戏才能用吗？**
A: 不需要。载入已有存档后，在叙事者选择界面切换为 RimMind Director 即可。

**Q: AI 不可用时会怎样？**
A: 自动切换到 Fallback 模式（默认 Cassandra），游戏不会中断。配置好 API 后 AI 自动接管。

**Q: AI 会选择过于极端的事件吗？**
A: AI 会根据殖民地当前状态调整事件强度。张力系统确保不会连续触发灾难性事件。你也可以通过自定义 Prompt 限制 AI 行为。

**Q: 可以和原版叙事者共存吗？**
A: RimMind Director 是独立的叙事者，选择它后替代原版叙事者。切换回原版叙事者即可恢复。

**Q: 配合 Memory 模组效果更好吗？**
A: 是的。Memory 提供殖民地历史记忆，Storyteller 参考这些记忆做出更有叙事连贯性的事件选择。

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

| Module | Role | Depends On |
|--------|------|------------|
| RimMind-Core | API client, request dispatch, context packaging | Harmony |
| RimMind-Actions | AI-controlled pawn action execution | Core |
| RimMind-Advisor | AI role-plays colonists for work decisions | Core, Actions |
| RimMind-Dialogue | AI-driven dialogue system | Core |
| RimMind-Memory | Memory collection & context injection | Core |
| RimMind-Personality | AI-generated personality & thoughts | Core |
| **RimMind-Storyteller** | **AI storyteller, smart event selection** | Core |

## Installation

### Install from Source

**Linux/macOS:**
```bash
git clone git@github.com:mcocdaa/RimWorld-RimMind-Mod-Storyteller.git
cd RimWorld-RimMind-Mod-Storyteller
./script/deploy-single.sh <your RimWorld path>
```

**Windows:**
```powershell
git clone git@github.com:mcocdaa/RimWorld-RimMind-Mod-Storyteller.git
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

- **AI Event Selection**: RimMind Director replaces traditional storytellers with LLM-powered event selection
- **Event Chain System**: AI can create multi-step chained events for narrative arcs
- **Tension System**: Tracks colony tension (0~1), adjusting event intensity for dramatic pacing
- **Fallback Mechanism**: Automatically switches to classic storyteller mode when AI is unavailable
- **Storyteller Altar**: In-game building for player-triggered AI narrative events
- **Custom Narrative Style**: Define AI behavior through custom prompts

## FAQ

**Q: Do I need a new game?**
A: No. Load an existing save and switch to RimMind Director in the storyteller selection screen.

**Q: What happens when AI is unavailable?**
A: Automatically switches to Fallback mode (default: Cassandra). Game continues normally. AI takes over once API is configured.

**Q: Will AI choose extreme events?**
A: AI adjusts event intensity based on colony state. The tension system prevents consecutive catastrophic events. You can also limit AI behavior via custom prompts.

**Q: Can it coexist with vanilla storytellers?**
A: RimMind Director is an independent storyteller. Selecting it replaces the vanilla one. Switch back to restore the original.

**Q: Does it work better with Memory?**
A: Yes. Memory provides colony history, and Storyteller references these memories for more narratively coherent event selection.

## Acknowledgments

This project references the following excellent RimWorld mods:

- [RimTalk](https://github.com/jlibrary/RimTalk.git) - Dialogue system reference
- [RimTalk-ExpandActions](https://github.com/sanguodxj-byte/RimTalk-ExpandActions.git) - Action expansion reference
- [NewRatkin](https://github.com/solaris0115/NewRatkin.git) - Race mod architecture reference
- [VanillaExpandedFramework](https://github.com/Vanilla-Expanded/VanillaExpandedFramework.git) - Framework design reference

## Contributing

Issues and Pull Requests are welcome! If you have any suggestions or find bugs, please feedback via GitHub Issues.
