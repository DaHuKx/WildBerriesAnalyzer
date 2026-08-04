namespace WildBerriesAnalyzer.Mobile.Helpers
{
    /// <summary>
    /// Resolves Ledger design tokens from App.Resources for the current RequestedTheme.
    /// </summary>
    public static class ThemeColors
    {
        public static bool IsDark =>
            Application.Current?.RequestedTheme == AppTheme.Dark;

        public static Color Get(string lightKey, string darkKey)
        {
            var key = IsDark ? darkKey : lightKey;
            if (Application.Current?.Resources.TryGetValue(key, out var value) == true &&
                value is Color color)
            {
                return color;
            }

            return Colors.Transparent;
        }

        public static Color Primary => Get("Primary", "PrimaryDarkTheme");

        public static Color PrimarySoft => Get("PrimarySoft", "PrimarySoftDark");

        public static Color Surface => Get("Surface", "SurfaceDark");

        public static Color SurfaceMuted => Get("SurfaceMuted", "SurfaceMutedDark");

        public static Color Outline => Get("Outline", "OutlineDark");

        public static Color TextPrimary => Get("TextPrimary", "TextPrimaryDark");

        public static Color TextSecondary => Get("TextSecondary", "TextSecondaryDark");

        public static Color TextMuted => Get("TextMuted", "TextMutedDark");

        public static Color Error => Get("Error", "ErrorDark");

        public static Color Success => Get("Success", "SuccessDark");

        public static Color Accent => Get("Accent", "AccentDark");

        public static Color OnPrimary => Get("OnPrimary", "OnPrimaryDark");
    }
}
