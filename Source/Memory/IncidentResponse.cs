namespace RimMind.Storyteller
{
    public class IncidentResponse
    {
        public string defName = string.Empty;
        public string reason = string.Empty;
        public string? announce;
        public IncidentParams? @params;
        public ChainInfo? chain;
    }

    public class IncidentParams
    {
        public float? points_multiplier;
        public string? faction_hint;
        public string? raid_strategy_hint;
    }

    public class ChainInfo
    {
        public string chain_id = string.Empty;
        public int chain_step;
        public int chain_total;
        public string? next_hint;
    }
}
