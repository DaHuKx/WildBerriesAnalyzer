using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using WildBerriesAnalyzer.Mobile.Services;

namespace WildBerriesAnalyzer.Mobile
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        // SingleTask: share из другого приложения не создаёт второй Activity (иначе MAUI падает).
        LaunchMode = LaunchMode.SingleTask,
        Exported = true,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density)]
    [IntentFilter(
        [Intent.ActionSend],
        Categories = [Intent.CategoryDefault],
        DataMimeType = "text/plain",
        Label = "В корзину PriceLab")]
    public class MainActivity : MauiAppCompatActivity
    {
        private bool _shareCapturedForResume;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Обход бага MAUI/.NET Android: при редеплое на устройство FragmentManager
            // пытается восстановить NavigationRootManager_ElementBasedFragment по «левому»
            // resource id (jumpToStart, labeled, italic и т.п.) → IllegalArgumentException.
            base.OnCreate(null);
            // Не уведомляем UI здесь — Activity/Prism ещё не готовы (часто при возврате из фона).
            CaptureShareIntent(Intent, notifyListeners: false);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            if (intent is null)
            {
                return;
            }

            Intent = intent;
            CaptureShareIntent(intent, notifyListeners: false);
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (!_shareCapturedForResume && !PendingShareStore.Instance.HasPending)
            {
                return;
            }

            _shareCapturedForResume = false;

            // После полного resume: UI и навигация уже живы.
            // Небольшая пауза — иначе при возврате из фона Prism/MAUI ещё не готовы к смене контента.
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(150);
                    PendingShareStore.Instance.NotifyPendingListeners();
                }
                catch
                {
                    // Не даём необработанному исключению убить процесс.
                }
            });
        }

        private void CaptureShareIntent(Intent? intent, bool notifyListeners)
        {
            if (intent?.Action != Intent.ActionSend)
            {
                return;
            }

            var text = intent.GetStringExtra(Intent.ExtraText);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = intent.GetStringExtra(Intent.ExtraSubject);
            }

            PendingShareStore.Instance.EnqueueRaw(text, notifyListeners);
            _shareCapturedForResume = PendingShareStore.Instance.HasPending;

            // Чтобы повторный OnResume / recreate не считал тот же SEND «новым».
            intent.SetAction(Intent.ActionMain);
            intent.RemoveExtra(Intent.ExtraText);
            intent.RemoveExtra(Intent.ExtraSubject);
        }
    }
}
