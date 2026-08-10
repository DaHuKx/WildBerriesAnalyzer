using WildBerriesAnalyzer.Modules.MyFilters.Models;

namespace WildBerriesAnalyzer.Modules.MyFilters.Services
{
    /// <summary>
    /// Связка страницы пресетов с MyFilters (ContentView внутри MainWindow).
    /// </summary>
    public interface IFilterPresetBridge
    {
        Action<FilterPreset>? OnPresetChosen { get; set; }
    }
}
