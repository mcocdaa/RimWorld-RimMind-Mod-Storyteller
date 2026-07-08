using RimMind.Storyteller.Extensions;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    /// <summary>
    /// Tests for the unified Memory reflection bridge.
    /// In the net10.0 test environment, RimWorld and RimMindMemory are not loaded,
    /// so the bridge must gracefully return false/empty.
    /// </summary>
    public class StorytellerMemoryBridgeTests
    {
        [Fact]
        public void TryPushNarratorEntry_WhenMemoryModNotLoaded_ReturnsFalse()
        {
            // Memory mod assembly is not loaded in test environment
            bool result = StorytellerMemoryBridge.TryPushNarratorEntry(
                "user: hello", tick: 1000, importance: 0.3f);
            Assert.False(result);
        }

        [Fact]
        public void GetRecentNarrations_WhenMemoryModNotLoaded_ReturnsEmpty()
        {
            string result = StorytellerMemoryBridge.GetRecentNarrations(count: 5);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void IsMemoryModLoaded_WhenNotLoaded_ReturnsFalse()
        {
            Assert.False(StorytellerMemoryBridge.IsMemoryModLoaded);
        }
    }
}
