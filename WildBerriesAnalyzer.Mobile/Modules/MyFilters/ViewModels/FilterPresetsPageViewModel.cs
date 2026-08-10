using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using WildBerriesAnalyzer.Modules.MyFilters.Models;
using WildBerriesAnalyzer.Modules.MyFilters.Services;

namespace WildBerriesAnalyzer.Modules.MyFilters.ViewModels
{
    public sealed class FilterPresetsPageViewModel : BindableBase
    {
        private readonly INavigationService _navigationService;
        private readonly IFilterPresetBridge _presetBridge;

        public FilterPresetsPageViewModel(
            INavigationService navigationService,
            IFilterPresetBridge presetBridge)
        {
            _navigationService = navigationService;
            _presetBridge = presetBridge;

            Presets = new ObservableCollection<FilterPreset>(FilterPresetsCatalog.All);
            GoBackCommand = new DelegateCommand(async () => await GoBackAsync());
            SelectPresetCommand = new DelegateCommand<FilterPreset>(async p => await SelectPresetAsync(p));
        }

        public string Title => "Пресеты фильтров";

        public string Subtitle => "Выберите готовый набор настроек. Состав корзины не изменится.";

        public ObservableCollection<FilterPreset> Presets { get; }

        public DelegateCommand GoBackCommand { get; }

        public DelegateCommand<FilterPreset> SelectPresetCommand { get; }

        private async Task SelectPresetAsync(FilterPreset? preset)
        {
            if (preset is null)
            {
                return;
            }

            _presetBridge.OnPresetChosen?.Invoke(preset);
            await GoBackAsync();
        }

        private async Task GoBackAsync()
        {
            await _navigationService.GoBackAsync(new NavigationParameters
            {
                { KnownNavigationParameters.UseModalNavigation, true }
            });
        }
    }
}
