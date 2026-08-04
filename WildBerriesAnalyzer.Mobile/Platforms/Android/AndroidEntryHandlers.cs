using Android.Content.Res;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AColor = Android.Graphics.Color;

namespace WildBerriesAnalyzer.Mobile;

/// <summary>
/// Контрастный текст + снятие Material underline (без обрезки текста padding'ом).
/// </summary>
internal static class AndroidEntryHandlers
{
    private static readonly AColor LightText = AColor.ParseColor("#0F1C1A");
    private static readonly AColor DarkText = AColor.ParseColor("#F5FFFD");
    private static readonly AColor Teal = AColor.ParseColor("#0F766E");

    public static void Configure()
    {
        EntryHandler.Mapper.AppendToMapping("WbEntryChrome", (handler, view) =>
        {
            if (handler.PlatformView is not EditText edit)
            {
                return;
            }

            var entry = view as Entry;
            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            if (entry?.BackgroundColor is not null)
            {
                dark = IsDarkMauiColor(entry.BackgroundColor);
            }

            var text = dark ? DarkText : LightText;
            if (entry?.TextColor is not null)
            {
                var requested = entry.TextColor.ToPlatform();
                // Не даём Material поставить почти невидимый цвет.
                if (HasContrastAgainstTheme(requested, dark))
                {
                    text = requested;
                }
            }

            edit.SetTextColor(text);

            // Убираем underline/focus-tint платформы — рамку рисуем MAUI Border.
            var transparent = ColorStateList.ValueOf(AColor.Transparent);
            edit.BackgroundTintList = transparent;
            AndroidX.Core.View.ViewCompat.SetBackgroundTintList(edit, transparent);
            edit.SetBackgroundColor(AColor.Transparent);

            if (OperatingSystem.IsAndroidVersionAtLeast(29) && edit.TextCursorDrawable is not null)
            {
                var cursor = edit.TextCursorDrawable.Mutate();
                cursor.SetTint(Teal);
                edit.TextCursorDrawable = cursor;
            }

            edit.SetHighlightColor(AColor.ParseColor("#330F766E"));
        });
    }

    private static bool IsDarkMauiColor(Color color)
    {
        var yiq = (color.Red * 255 * 299 + color.Green * 255 * 587 + color.Blue * 255 * 114) / 1000.0;
        return yiq < 140;
    }

    private static bool HasContrastAgainstTheme(AColor text, bool darkBackground)
    {
        var argb = unchecked((uint)text.ToArgb());
        var r = (argb >> 16) & 0xFF;
        var g = (argb >> 8) & 0xFF;
        var b = argb & 0xFF;
        var yiq = (r * 299 + g * 587 + b * 114) / 1000.0;
        return darkBackground ? yiq >= 160 : yiq <= 120;
    }
}
