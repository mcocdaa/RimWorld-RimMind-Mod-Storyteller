using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimMind.Core.Context;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller.UI
{
    public class Window_StorytellerDialogue : Window
    {
        private Map _map;
        private List<(string role, string content)> _messages = new List<(string, string)>();
        private string _inputText = "";
        private bool _waitingForResponse;
        private Vector2 _scrollPos = Vector2.zero;
        private bool _autoScroll = true;
        private readonly ConcurrentQueue<(string role, string content)> _responseQueue = new ConcurrentQueue<(string, string)>();
        private const int MaxHistoryRounds = 6;
        private const float Padding = 8f;
        private const float InputHeight = 36f;
        private const float SendButtonWidth = 80f;
        private const float HeaderHeight = 28f;
        private const float StatusHeight = 22f;
        private const float ScrollbarWidth = 16f;
        private static readonly Color UserColor = new Color(0.7f, 0.85f, 1f);
        private static readonly Color StorytellerColor = new Color(1f, 0.95f, 0.8f);
        private static readonly Color ChatBgColor = new Color(0.08f, 0.08f, 0.12f, 0.5f);
        private static readonly Color StatusColor = new Color(1f, 1f, 0.5f);
        private static readonly Color HeaderColor = new Color(1f, 0.9f, 0.7f);

        public override Vector2 InitialSize => new Vector2(520f, 560f);

        public Window_StorytellerDialogue(Map map) : base()
        {
            _map = map;
            doCloseButton = false;
            doCloseX = true;
            closeOnClickedOutside = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            draggable = true;

            var memory = StorytellerMemory.Instance;
            if (memory != null)
            {
                int loadCount = System.Math.Min(MaxHistoryRounds, memory.DialogueRecords.Count);
                var recentRecords = memory.DialogueRecords
                    .Skip(System.Math.Max(0, memory.DialogueRecords.Count - loadCount)).ToList();
                foreach (var r in recentRecords)
                    _messages.Add((r.role, r.content));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            while (_responseQueue.TryDequeue(out var resp))
            {
                _waitingForResponse = false;
                if (resp.role == "system") continue;
                _messages.Add(resp);
                _autoScroll = true;
                RecordDialogueToMemory(resp.role, resp.content);
            }

            Text.Font = GameFont.Small;

            float statusH = _waitingForResponse ? StatusHeight + Padding : 0f;
            float chatHeight = inRect.height - HeaderHeight - statusH - InputHeight - Padding * 3;
            float inputY = inRect.yMax - InputHeight;

            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, HeaderHeight);
            Rect chatRect = new Rect(inRect.x, inRect.y + HeaderHeight + Padding, inRect.width, chatHeight);

            GUI.color = HeaderColor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(headerRect, "RimMind.Storyteller.Dialogue.Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            Widgets.DrawBoxSolid(chatRect, ChatBgColor);
            DrawChatHistory(chatRect);

            if (_waitingForResponse)
            {
                Rect statusRect = new Rect(inRect.x, inputY - StatusHeight - Padding, inRect.width, StatusHeight);
                GUI.color = StatusColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(statusRect, "RimMind.Storyteller.Dialogue.Thinking".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            Rect inputRect = new Rect(inRect.x, inputY, inRect.width - SendButtonWidth - Padding, InputHeight);
            Rect sendRect = new Rect(inRect.xMax - SendButtonWidth, inputY, SendButtonWidth, InputHeight);

            _inputText = Widgets.TextField(inputRect, _inputText);

            if (!_waitingForResponse)
            {
                if (Widgets.ButtonText(sendRect, "RimMind.Storyteller.Dialogue.Send".Translate()))
                    SendMessage();
            }
            else
            {
                Widgets.ButtonText(sendRect, "RimMind.Storyteller.Dialogue.Send".Translate());
            }

            if (!_waitingForResponse && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Return && !_inputText.NullOrEmpty())
            {
                SendMessage();
                Event.current.Use();
            }
        }

        private void DrawChatHistory(Rect rect)
        {
            float contentWidth = rect.width - ScrollbarWidth;
            float contentHeight = CalcMessagesHeight(contentWidth - Padding * 2);
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);

            float prevScrollY = _scrollPos.y;
            Widgets.BeginScrollView(rect, ref _scrollPos, viewRect);

            float y = 0f;
            for (int i = 0; i < _messages.Count; i++)
            {
                var msg = _messages[i];
                string prefix = msg.role == "user"
                    ? "RimMind.Storyteller.Dialogue.PlayerPrefix".Translate()
                    : "RimMind.Storyteller.Dialogue.StorytellerPrefix".Translate();
                string line = prefix + msg.content;
                float lineH = Text.CalcHeight(line, contentWidth - Padding * 2) + Padding;

                if (i % 2 == 0)
                {
                    Widgets.DrawBoxSolid(new Rect(0f, y, contentWidth, lineH),
                        new Color(1f, 1f, 1f, 0.03f));
                }

                GUI.color = msg.role == "user" ? UserColor : StorytellerColor;
                Widgets.Label(new Rect(Padding, y, contentWidth - Padding * 2, lineH), line);
                GUI.color = Color.white;

                y += lineH;
            }

            Widgets.EndScrollView();

            float maxScroll = Mathf.Max(0f, contentHeight - rect.height);
            if (_autoScroll && maxScroll > 0f)
                _scrollPos.y = maxScroll;

            if (Mathf.Abs(prevScrollY - _scrollPos.y) > 1f && _scrollPos.y < maxScroll - 1f)
                _autoScroll = false;

            if (_scrollPos.y >= maxScroll - 2f)
                _autoScroll = true;
        }

        private void SendMessage()
        {
            if (_inputText.NullOrEmpty() || _waitingForResponse) return;
            if (!RimMindAPI.IsConfigured()) return;

            string userMsg = _inputText.Trim();
            _inputText = "";
            _messages.Add(("user", userMsg));
            _autoScroll = true;

            RecordDialogueToMemory("user", userMsg);

            if (_messages.Count > MaxHistoryRounds * 2)
                _messages = _messages.TakeLast(MaxHistoryRounds * 2).ToList();

            _waitingForResponse = true;

            // 祭坛对话走 Chat 路径，由 ContextEngine 接管 Prompt 构建
            float budget = GetStorytellerBudget();
            var request = new ContextRequest
            {
                NpcId = "NPC-storyteller",
                Scenario = ScenarioIds.Storyteller,
                Budget = budget,
                CurrentQuery = userMsg,
                MaxTokens = 300,
                Temperature = 0.9f,
            };

            RimMindAPI.Chat(request).ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    _responseQueue.Enqueue(("system", ""));
                    return;
                }
                var result = task.Result;
                if (result == null || !string.IsNullOrEmpty(result.Error))
                {
                    _responseQueue.Enqueue(("system", ""));
                    return;
                }
                string assistantMsg = result.Message?.Trim() ?? "";
                if (assistantMsg.NullOrEmpty())
                {
                    _responseQueue.Enqueue(("system", ""));
                    return;
                }
                _responseQueue.Enqueue(("assistant", assistantMsg));
            });
        }

        // 从 ContextSettings 读取 Storyteller 场景预算
        private static float GetStorytellerBudget()
        {
            var settings = RimMind.Core.RimMindCoreMod.Settings?.Context;
            if (settings == null) return 0.6f;
            return settings.ContextBudget;
        }

        private float CalcMessagesHeight(float width)
        {
            float h = 0f;
            for (int i = 0; i < _messages.Count; i++)
            {
                var msg = _messages[i];
                string prefix = msg.role == "user"
                    ? "RimMind.Storyteller.Dialogue.PlayerPrefix".Translate()
                    : "RimMind.Storyteller.Dialogue.StorytellerPrefix".Translate();
                string line = prefix + msg.content;
                h += Text.CalcHeight(line, width) + Padding;
            }
            return h + Padding;
        }

        private static void RecordDialogueToMemory(string role, string content)
        {
            var memory = StorytellerMemory.Instance;
            if (memory == null) return;

            var settings = RimMind.Storyteller.RimMindStorytellerMod.Settings;
            if (settings != null)
            {
                memory.MaxDialogueRecords = settings.maxDialogueRecords;
                memory.MaxRecords = settings.maxEventRecords;
            }

            int tick = Find.TickManager.TicksGame;
            memory.RecordDialogue(role, content, tick);

            TryPushToMemoryMod(role, content, tick);
        }

        private static void TryPushToMemoryMod(string role, string content, int tick)
        {
            try
            {
                var memoryAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimMindMemory");
                if (memoryAssembly == null) return;

                var worldCompType = memoryAssembly.GetType("RimMind.Memory.Data.RimMindMemoryWorldComponent");
                if (worldCompType == null) return;

                var instanceProp = worldCompType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null) return;

                var worldComp = instanceProp.GetValue(null);
                if (worldComp == null) return;

                var narratorStoreProp = worldCompType.GetProperty("NarratorStore");
                if (narratorStoreProp == null) return;

                var narratorStore = narratorStoreProp.GetValue(worldComp);
                if (narratorStore == null) return;

                var settingsType = memoryAssembly.GetType("RimMind.Memory.RimMindMemoryMod");
                if (settingsType == null) return;

                var settingsProp = settingsType.GetProperty("Settings",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var memSettings = settingsProp?.GetValue(null);

                bool enableMemory = true;
                int narratorMaxActive = 30;
                int narratorMaxArchive = 10;

                if (memSettings != null)
                {
                    var enableField = memSettings.GetType().GetField("enableMemory");
                    if (enableField != null) enableMemory = (bool)enableField.GetValue(memSettings);

                    var maxActiveField = memSettings.GetType().GetField("narratorMaxActive");
                    if (maxActiveField != null) narratorMaxActive = (int)maxActiveField.GetValue(memSettings);

                    var maxArchiveField = memSettings.GetType().GetField("narratorMaxArchive");
                    if (maxArchiveField != null) narratorMaxArchive = (int)maxArchiveField.GetValue(memSettings);
                }

                if (!enableMemory) return;

                var entryType = memoryAssembly.GetType("RimMind.Memory.Data.MemoryEntry");
                if (entryType == null) return;

                var createMethod = entryType.GetMethod("Create",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (createMethod == null) return;

                var memoryTypeEnum = memoryAssembly.GetType("RimMind.Memory.Data.MemoryType");
                if (memoryTypeEnum == null) return;

                var eventType = System.Enum.Parse(memoryTypeEnum, "Event");

                string prefix = role == "user"
                    ? "RimMind.Storyteller.Prompt.RolePlayer".Translate()
                    : "RimMind.Storyteller.Prompt.RoleNarrator".Translate();
                object entry = createMethod.Invoke(null, new object[] { $"{prefix}: {content}", eventType, tick, 0.3f, null! })!;

                var addActiveMethod = narratorStore.GetType().GetMethod("AddActive");
                if (addActiveMethod != null && entry != null)
                    addActiveMethod.Invoke(narratorStore, new object[] { entry, narratorMaxActive, narratorMaxArchive });
            }
            catch (System.Exception ex)
            {
                Log.WarningOnce($"[RimMind-Storyteller] TryPushToMemoryMod failed: {ex.Message}", 76543210);
            }
        }
    }
}
