using System.ComponentModel;
using WildBerriesAnalyzer.Modules.MainWindow.ViewModels;

namespace WildBerriesAnalyzer.Modules.MainWindow.Views
{
    public partial class MainWindowPage : ContentPage
    {
        private const double MenuWidth = 280;
        private const uint AnimMs = 260;

        private MainWindowPageViewModel? _viewModel;
        private bool _isAnimating;

        public MainWindowPage()
        {
            InitializeComponent();
            SideMenuPanel.TranslationX = -MenuWidth;
            SideMenuOverlay.Opacity = 0;
            SideMenuOverlay.InputTransparent = true;
            SideMenuPanel.InputTransparent = true;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = BindingContext as MainWindowPageViewModel;
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _ = ApplyMenuStateAsync(_viewModel.IsMenuOpen, animate: false);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainWindowPageViewModel.IsMenuOpen) || _viewModel is null)
            {
                return;
            }

            _ = ApplyMenuStateAsync(_viewModel.IsMenuOpen, animate: true);
        }

        private async Task ApplyMenuStateAsync(bool open, bool animate)
        {
            if (_isAnimating)
            {
                SideMenuPanel.CancelAnimations();
                SideMenuOverlay.CancelAnimations();
            }

            _isAnimating = true;
            try
            {
                if (open)
                {
                    SideMenuOverlay.InputTransparent = false;
                    SideMenuPanel.InputTransparent = false;

                    if (!animate)
                    {
                        SideMenuPanel.TranslationX = 0;
                        SideMenuOverlay.Opacity = 1;
                        return;
                    }

                    SideMenuPanel.TranslationX = -MenuWidth;
                    SideMenuOverlay.Opacity = 0;
                    await Task.WhenAll(
                        SideMenuPanel.TranslateTo(0, 0, AnimMs, Easing.CubicOut),
                        SideMenuOverlay.FadeTo(1, AnimMs, Easing.CubicOut));
                }
                else
                {
                    if (!animate)
                    {
                        SideMenuPanel.TranslationX = -MenuWidth;
                        SideMenuOverlay.Opacity = 0;
                        SideMenuOverlay.InputTransparent = true;
                        SideMenuPanel.InputTransparent = true;
                        return;
                    }

                    await Task.WhenAll(
                        SideMenuPanel.TranslateTo(-MenuWidth, 0, AnimMs, Easing.CubicIn),
                        SideMenuOverlay.FadeTo(0, AnimMs, Easing.CubicIn));

                    SideMenuOverlay.InputTransparent = true;
                    SideMenuPanel.InputTransparent = true;
                }
            }
            finally
            {
                _isAnimating = false;
            }
        }
    }
}
