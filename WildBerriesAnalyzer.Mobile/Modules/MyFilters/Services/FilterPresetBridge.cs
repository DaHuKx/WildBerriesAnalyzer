using WildBerriesAnalyzer.Modules.MyFilters.Models;

namespace WildBerriesAnalyzer.Modules.MyFilters.Services
{
    public sealed class FilterPresetBridge : IFilterPresetBridge
    {
        public Action<FilterPreset>? OnPresetChosen { get; set; }
    }
}
