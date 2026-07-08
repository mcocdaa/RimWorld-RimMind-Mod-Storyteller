using System;
using System.Collections;
using System.Reflection;
using System.Text;

namespace RimMind.Storyteller.Extensions
{
    /// <summary>
    /// Unified reflection bridge to RimMind-Memory mod.
    /// Single entry point for both write (TryPushNarratorEntry) and read (GetRecentNarrations).
    ///
    /// 设计说明：
    /// 此类为纯反射逻辑，不直接依赖 RimWorld 运行时 (LoadedModManager/Verse) 或
    /// RimMind-Core 运行时 (RimMindErrors/Translate)，以便在 net10.0 测试项目中直接编译。
    /// 三个委托 (AssemblyResolver / Warn / Translate) 由 RimMindStorytellerMod 在初始化时注入。
    /// 委托为 null 时桥接退化为 no-op (返回 false/empty)，这正是 Memory 模组未加载时的正确行为。
    ///
    /// 反射目标 (已对照 RimMind-Memory 源码核实):
    ///   RimMind.Memory.Data.RimMindMemoryWorldComponent
    ///     - static property Instance (返回 RimMindMemoryWorldComponent?)
    ///     - instance property NarratorStore (返回 NarratorMemoryStore)
    ///     - instance method GetNarratorMemories() 返回 IReadOnlyList&lt;MemoryEntry&gt;
    ///   RimMind.Memory.Data.NarratorMemoryStore
    ///     - instance method AddActive(MemoryEntry, int maxActive, int maxArchive)
    ///   RimMind.Memory.Data.MemoryEntry
    ///     - public fields: content, tick (小写字段名，非属性)
    ///     - static method Create(string content, MemoryType type, int tick, float importance, string? pawnId)
    ///   RimMind.Memory.Data.MemoryType enum: { Work, Event, Manual, Dark }
    ///   RimMind.Memory.RimMindMemoryMod
    ///     - static field Settings (RimMindMemorySettings)
    ///   RimMind.Memory.Settings.RimMindMemorySettings
    ///     - public fields: enableMemory (bool), narratorMaxActive (int), narratorMaxArchive (int)
    /// </summary>
    public static class StorytellerMemoryBridge
    {
        // --- 反射目标常量 ---
        private const string WorldCompType = "RimMind.Memory.Data.RimMindMemoryWorldComponent";
        private const string MemoryEntryType = "RimMind.Memory.Data.MemoryEntry";
        private const string MemoryTypeEnum = "RimMind.Memory.Data.MemoryType";
        private const string SettingsType = "RimMind.Memory.RimMindMemoryMod";

        // 默认值 (与 RimMindMemorySettings 默认值一致)
        private const bool DefaultEnableMemory = true;
        private const int DefaultNarratorMaxActive = 30;
        private const int DefaultNarratorMaxArchive = 10;
        private const int TicksPerDay = 60000;

        // --- 可注入依赖 (由 RimMindStorytellerMod 注入) ---
        // null = 未注入 → 桥接退化为 no-op，返回 false/empty。
        // 测试环境不注入任何委托，因此所有方法返回“未加载”结果。

        /// <summary>解析 RimMindMemory 程序集。生产环境由 RimMindStorytellerMod 注入，
        /// 使用 LoadedModManager.RunningMods (RimWorld 规范方式)。测试环境保持 null。</summary>
        internal static Func<Assembly?>? AssemblyResolver = null;

        /// <summary>警告日志输出。生产环境注入 RimMindErrors.Warn。测试环境保持 null。</summary>
        internal static Action<string>? Warn = null;

        /// <summary>翻译键查找。生产环境注入 key =&gt; key.Translate()。测试环境保持 null。</summary>
        internal static Func<string, string>? Translate = null;

        /// <summary>Memory 模组是否已加载 (程序集可解析)。</summary>
        public static bool IsMemoryModLoaded => ResolveAssembly() != null;

        /// <summary>
        /// WRITE 路径：向 NarratorMemoryStore 写入一条叙事者记忆条目。
        /// 失败 (模组未加载 / 反射失败 / enableMemory=false) 时返回 false，不抛异常。
        /// </summary>
        /// <param name="content">记忆内容 (已含角色前缀)。</param>
        /// <param name="tick">游戏 Tick。</param>
        /// <param name="importance">重要度 0..1。</param>
        /// <returns>是否成功写入。</returns>
        public static bool TryPushNarratorEntry(string content, int tick, float importance)
        {
            var asm = ResolveAssembly();
            if (asm == null) return false;

            try
            {
                var worldComp = ResolveWorldComponentInstance(asm);
                if (worldComp == null)
                {
                    Warn?.Invoke("[RimMind-Storyteller] Bridge: RimMindMemoryWorldComponent.Instance not resolved");
                    return false;
                }

                var narratorStore = ResolveNarratorStore(worldComp);
                if (narratorStore == null)
                {
                    Warn?.Invoke("[RimMind-Storyteller] Bridge: NarratorStore not resolved");
                    return false;
                }

                var (enableMemory, maxActive, maxArchive) = ReadMemorySettings(asm);
                if (!enableMemory) return false;

                var entry = CreateMemoryEntry(asm, content, tick, importance);
                if (entry == null)
                {
                    Warn?.Invoke("[RimMind-Storyteller] Bridge: MemoryEntry.Create failed");
                    return false;
                }

                var addActiveMethod = narratorStore.GetType().GetMethod("AddActive",
                    BindingFlags.Public | BindingFlags.Instance);
                if (addActiveMethod == null)
                {
                    Warn?.Invoke("[RimMind-Storyteller] Bridge: AddActive method not found");
                    return false;
                }

                addActiveMethod.Invoke(narratorStore, new object[] { entry, maxActive, maxArchive });
                return true;
            }
            catch (Exception ex)
            {
                Warn?.Invoke($"[RimMind-Storyteller] Bridge.TryPushNarratorEntry failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// READ 路径：读取最近的叙事者记忆，格式化为 Prompt 上下文字符串。
        /// 失败 (模组未加载 / 反射失败 / 无记忆) 时返回 string.Empty，不抛异常。
        /// </summary>
        /// <param name="count">最多读取条数。</param>
        /// <returns>格式化字符串 (含表头)，或 string.Empty。</returns>
        public static string GetRecentNarrations(int count)
        {
            var asm = ResolveAssembly();
            if (asm == null) return string.Empty;

            try
            {
                var worldComp = ResolveWorldComponentInstance(asm);
                if (worldComp == null) return string.Empty;

                var memories = InvokeGetNarratorMemories(worldComp);
                if (memories == null || memories.Count == 0) return string.Empty;

                var sb = new StringBuilder();
                sb.AppendLine(TranslateHeader("RimMind.Storyteller.Prompt.RecentIncidents"));

                var entryType = asm.GetType(MemoryEntryType);
                var contentField = entryType?.GetField("content", BindingFlags.Public | BindingFlags.Instance);
                var tickField = entryType?.GetField("tick", BindingFlags.Public | BindingFlags.Instance);

                int limit = Math.Min(count, memories.Count);
                for (int i = 0; i < limit; i++)
                {
                    var entry = memories[i];
                    if (entry == null) continue;

                    string content = (contentField?.GetValue(entry) as string) ?? string.Empty;
                    int tick = 0;
                    try
                    {
                        if (tickField?.GetValue(entry) is int t) tick = t;
                    }
                    catch { /* keep default 0 */ }

                    int day = tick / TicksPerDay + 1;
                    sb.AppendLine($"[Day {day}] {content}");
                }
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                Warn?.Invoke($"[RimMind-Storyteller] Bridge.GetRecentNarrations failed: {ex.Message}");
                return string.Empty;
            }
        }

        // --- 内部辅助方法 ---

        private static Assembly? ResolveAssembly()
        {
            try
            {
                return AssemblyResolver?.Invoke();
            }
            catch (Exception ex)
            {
                Warn?.Invoke($"[RimMind-Storyteller] Bridge assembly resolve failed: {ex.Message}");
                return null;
            }
        }

        private static object? ResolveWorldComponentInstance(Assembly asm)
        {
            var worldCompType = asm.GetType(WorldCompType);
            if (worldCompType == null) return null;

            var instanceProp = worldCompType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            return instanceProp?.GetValue(null);
        }

        private static object? ResolveNarratorStore(object worldComp)
        {
            var narratorStoreProp = worldComp.GetType().GetProperty("NarratorStore",
                BindingFlags.Public | BindingFlags.Instance);
            return narratorStoreProp?.GetValue(worldComp);
        }

        private static IList? InvokeGetNarratorMemories(object worldComp)
        {
            var getMemoriesMethod = worldComp.GetType().GetMethod("GetNarratorMemories",
                BindingFlags.Public | BindingFlags.Instance);
            if (getMemoriesMethod == null) return null;

            var result = getMemoriesMethod.Invoke(worldComp, null);
            return result as IList;
        }

        private static (bool enableMemory, int maxActive, int maxArchive) ReadMemorySettings(Assembly asm)
        {
            bool enableMemory = DefaultEnableMemory;
            int maxActive = DefaultNarratorMaxActive;
            int maxArchive = DefaultNarratorMaxArchive;

            var settingsType = asm.GetType(SettingsType);
            if (settingsType == null) return (enableMemory, maxActive, maxArchive);

            var settingsField = settingsType.GetField("Settings",
                BindingFlags.Public | BindingFlags.Static);
            var memSettings = settingsField?.GetValue(null);
            if (memSettings == null) return (enableMemory, maxActive, maxArchive);

            var settingsInstanceType = memSettings.GetType();

            var enableField = settingsInstanceType.GetField("enableMemory",
                BindingFlags.Public | BindingFlags.Instance);
            if (enableField != null)
            {
                try
                {
                    if (enableField.GetValue(memSettings) is bool b) enableMemory = b;
                }
                catch (Exception ex)
                {
                    Warn?.Invoke($"[RimMind-Storyteller] Bridge: enableMemory.GetValue failed: {ex.Message}");
                }
            }

            var maxActiveField = settingsInstanceType.GetField("narratorMaxActive",
                BindingFlags.Public | BindingFlags.Instance);
            if (maxActiveField != null)
            {
                try
                {
                    if (maxActiveField.GetValue(memSettings) is int ma) maxActive = ma;
                }
                catch (Exception ex)
                {
                    Warn?.Invoke($"[RimMind-Storyteller] Bridge: narratorMaxActive.GetValue failed: {ex.Message}");
                }
            }

            var maxArchiveField = settingsInstanceType.GetField("narratorMaxArchive",
                BindingFlags.Public | BindingFlags.Instance);
            if (maxArchiveField != null)
            {
                try
                {
                    if (maxArchiveField.GetValue(memSettings) is int mar) maxArchive = mar;
                }
                catch (Exception ex)
                {
                    Warn?.Invoke($"[RimMind-Storyteller] Bridge: narratorMaxArchive.GetValue failed: {ex.Message}");
                }
            }

            return (enableMemory, maxActive, maxArchive);
        }

        private static object? CreateMemoryEntry(Assembly asm, string content, int tick, float importance)
        {
            var entryType = asm.GetType(MemoryEntryType);
            if (entryType == null) return null;

            var createMethod = entryType.GetMethod("Create",
                BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null) return null;

            var memoryTypeEnum = asm.GetType(MemoryTypeEnum);
            if (memoryTypeEnum == null) return null;

            object eventType;
            try { eventType = Enum.Parse(memoryTypeEnum, "Event"); }
            catch (Exception ex)
            {
                Warn?.Invoke($"[RimMind-Storyteller] Bridge: MemoryType.Event parse failed: {ex.Message}");
                return null;
            }

            try
            {
                return createMethod.Invoke(null, new object[] { content, eventType, tick, importance, null! });
            }
            catch (Exception ex)
            {
                Warn?.Invoke($"[RimMind-Storyteller] Bridge: MemoryEntry.Create.Invoke failed: {ex.Message}");
                return null;
            }
        }

        private static string TranslateHeader(string key)
        {
            try
            {
                if (Translate != null) return Translate.Invoke(key);
            }
            catch
            {
                // fall through to default
            }
            return key;
        }
    }
}
