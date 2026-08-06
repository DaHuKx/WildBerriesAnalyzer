using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Consts
{
    public static class ExpectedUserMessages
    {
        public const string Filters = "Фильтры";
        public const string AddProducts = "Добавление продуктов";
        public const string ActualDisconts = "Актуальные скидки";

        public const string Filters_Info = "Мои текущие фильтры";
        public const string Filters_Percent = "% скидки";
        public const string Filters_MinRating = "Рейтинг";
        public const string Filters_MinReviews = "Отзывы";
        public const string Filters_Strategy = "Стратегии определения скидок";
        public const string Filters_Type = "Тип фильтрации";
        public const string Filters_ChangeProducts = "Корзина/категории";

        public const string Filters_Type_None = "Без фильтрации";
        public const string Filters_Type_OwnBug = "Корзина";
        public const string Filters_Type_BlackList = "Чёрный список категорий";
        public const string Filters_Type_WhiteList = "Белый список категорий";

        public const string Filters_OwnBag_AddProducts = "Добавить штучно";
        public const string Filters_OwnBag_AddShare = "Импорт корзины из WB";
        public const string Filters_OwnBag_RemoveProducts = "Удалить товары";
        public const string Filters_OwnBag_ProductsList = "Мой список";

        public const string AddProducts_Name = "По названию";
        public const string AddProducts_Ids = "По артикулу";

        public const string Back = "Назад";

        public static Dictionary<string, BotUserPlace?> MenuExpectsPlaces => new Dictionary<string, BotUserPlace?>()
        {
            [Filters] = BotUserPlace.Filters,
            [ActualDisconts] = null
        };

        public static Dictionary<string, BotUserPlace?> FiltersExceptsPlaces => new Dictionary<string, BotUserPlace?>()
        {
            [Filters_Info] = null,
            [Filters_Percent] = BotUserPlace.Filters_Percent,
            [Filters_MinRating] = BotUserPlace.Filters_Rating,
            [Filters_MinReviews] = BotUserPlace.Filters_Reviews,
            [Filters_Strategy] = BotUserPlace.Filters_Strategy,
            [Filters_Type] = BotUserPlace.Filters_Type,
            [Filters_ChangeProducts] = null,
            [Back] = BotUserPlace.Menu
        };

        public static Dictionary<string, BotUserPlace?> FiltersOwnBagExpectsPlaces => new Dictionary<string, BotUserPlace?>()
        {
            [Filters_OwnBag_ProductsList] = null,
            [Filters_OwnBag_AddProducts] = BotUserPlace.Filters_ChangeProducts_OwnBag_Add,
            [Filters_OwnBag_AddShare] = BotUserPlace.Filters_ChangeProducts_OwnBag_AddShare,
            [Filters_OwnBag_RemoveProducts] = BotUserPlace.Filters_ChangeProducts_OwnBag_Remove,
            [Back] = BotUserPlace.Filters
        };

        public static Dictionary<string, BotUserPlace?> AddProductsExpectsPlaces => new Dictionary<string, BotUserPlace?>()
        {
            [AddProducts_Name] = BotUserPlace.AddProducts_Name,
            [AddProducts_Ids] = BotUserPlace.AddProducts_Ids,
            [Back] = BotUserPlace.Menu
        };

        public static bool IsBackMessage(string text)
        {
            return text.Equals(Back);
        }
    }
}
