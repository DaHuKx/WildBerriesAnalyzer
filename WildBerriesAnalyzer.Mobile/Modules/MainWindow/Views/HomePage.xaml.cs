namespace WildBerriesAnalyzer.Modules.MainWindow.Views
{
    public partial class HomePage : ContentView
    {
        private readonly HashSet<VisualElement> _revealed = [];
        private readonly List<VisualElement> _revealTargets = [];
        private bool _layoutHooked;

        public HomePage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += (_, _) => _ = TryRevealVisibleAsync();
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            if (_layoutHooked)
            {
                return;
            }

            _layoutHooked = true;
            CollectRevealTargets();
            PrepareHiddenSections();
            _ = RevealHeroAndVisibleAsync();
        }

        private void CollectRevealTargets()
        {
            _revealTargets.Clear();
            _revealTargets.Add(HeroSection);
            _revealTargets.Add(DiscountsSection);
            _revealTargets.Add(ProductsSection);
            _revealTargets.Add(ScheduleSection);
            _revealTargets.Add(HowToSection);
            _revealTargets.Add(QuickLinksSection);
        }

        private void PrepareHiddenSections()
        {
            foreach (var element in _revealTargets)
            {
                if (element == HeroSection)
                {
                    continue;
                }

                element.Opacity = 0;
                element.TranslationY = 28;
            }
        }

        private async Task RevealHeroAndVisibleAsync()
        {
            await Task.Delay(40);
            await RevealElementAsync(HeroSection, delayMs: 0);
            await TryRevealVisibleAsync();
        }

        private void OnHomeScroll(object? sender, ScrolledEventArgs e)
        {
            _ = TryRevealVisibleAsync();
        }

        private async Task TryRevealVisibleAsync()
        {
            if (_revealTargets.Count == 0 || HomeScroll.Height <= 0)
            {
                return;
            }

            var viewportTop = HomeScroll.ScrollY;
            var viewportBottom = viewportTop + HomeScroll.Height;
            var revealMargin = 48;

            foreach (var element in _revealTargets)
            {
                if (_revealed.Contains(element) || element.Height <= 0)
                {
                    continue;
                }

                var y = GetYRelativeToScrollContent(element);
                var top = y;
                var bottom = y + element.Height;

                var visible = bottom > viewportTop - revealMargin && top < viewportBottom + revealMargin;
                if (!visible)
                {
                    continue;
                }

                var index = _revealTargets.IndexOf(element);
                await RevealElementAsync(element, delayMs: Math.Max(0, index - 1) * 70);
            }
        }

        private async Task RevealElementAsync(VisualElement element, int delayMs)
        {
            if (!_revealed.Add(element))
            {
                return;
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }

            element.CancelAnimations();
            await Task.WhenAll(
                element.FadeTo(1, 320, Easing.CubicOut),
                element.TranslateTo(0, 0, 320, Easing.CubicOut));
        }

        private static double GetYRelativeToScrollContent(VisualElement element)
        {
            double y = 0;
            Element? current = element;

            while (current is VisualElement visual && current is not ScrollView)
            {
                y += visual.Y;
                current = current.Parent;
            }

            return y;
        }
    }
}
