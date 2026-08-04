using Android.App;
using Android.Content.PM;
using Android.OS;

namespace WildBerriesAnalyzer.Mobile
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Обход бага MAUI/.NET Android: при редеплое на устройство FragmentManager
            // пытается восстановить NavigationRootManager_ElementBasedFragment по «левому»
            // resource id (jumpToStart, labeled, italic и т.п.) → IllegalArgumentException.
            base.OnCreate(null);
        }
    }
}
