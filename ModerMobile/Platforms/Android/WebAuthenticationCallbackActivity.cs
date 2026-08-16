using Android.App;
using Android.Content;
using Android.Content.PM;

namespace ModerMobile;

[Activity(
    NoHistory = true,
    LaunchMode = LaunchMode.SingleTop,
    Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[]
    {
        Intent.CategoryDefault,
        Intent.CategoryBrowsable
    },
    DataScheme = "vk54707637",
    DataHost = "vk.ru",
    DataPath = "/blank.html")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[]
    {
        Intent.CategoryDefault,
        Intent.CategoryBrowsable
    },
    DataScheme = "wbmoder",
    DataHost = "vk-auth")]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
