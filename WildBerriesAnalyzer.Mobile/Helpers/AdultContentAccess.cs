namespace WildBerriesAnalyzer.Mobile.Helpers
{
    public static class AdultContentAccess
    {
        public const string RestrictedTitle = "Контент 18+";

        public const string RestrictedMessage =
            "Этот товар недоступен для просмотра. " +
            "Чтобы открыть его, включите отображение товаров 18+ в настройках.";

        public static bool IsRestricted(bool isAdult, bool showAdultContent) =>
            isAdult && !showAdultContent;

        public static async Task ShowRestrictedAsync()
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
                page.DisplayAlert(RestrictedTitle, RestrictedMessage, "Понятно"));
        }
    }
}
