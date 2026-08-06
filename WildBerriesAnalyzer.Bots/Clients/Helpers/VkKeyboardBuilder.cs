using VkNet.Enums.StringEnums;
using VkNet.Model;
using WildBerriesAnalyzer.Bots.Consts;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Bots.Clients.Helpers
{
    public static class VkKeyboardBuilder
    {
        private static readonly Dictionary<BotUserPlace, MessageKeyboard> _keyboards;

        static VkKeyboardBuilder()
        {
            _keyboards = new Dictionary<BotUserPlace, MessageKeyboard>();

            InitializeKeyboards();
        }

        public static MessageKeyboard GetKeyboardByPlace(BotUserPlace place)
        {
            return _keyboards[place];
        }

        private static void InitializeKeyboards()
        {
            KeyboardBuilder keyboardBuilder = new KeyboardBuilder();

            InitBackButtons(keyboardBuilder);
            InitMenuButtons(keyboardBuilder);
            InitFiltersButtons(keyboardBuilder);
            InitAddProductsButtons(keyboardBuilder);
            InitFiltersTypeButtons(keyboardBuilder);
            InitOwnBagButtons(keyboardBuilder);
        }

        private static void InitBackButtons(KeyboardBuilder builder)
        {
            builder.Clear();

            builder.AddButton(new AddButtonParams
            {
                Label = "Назад",
                Color = KeyboardButtonColor.Secondary,
            });

            var backButton = builder.Build();

            _keyboards.Add(BotUserPlace.Filters_Percent, backButton);
            _keyboards.Add(BotUserPlace.Filters_Strategy, backButton);
            _keyboards.Add(BotUserPlace.Filters_Rating, backButton);
            _keyboards.Add(BotUserPlace.Filters_Reviews, backButton);
            _keyboards.Add(BotUserPlace.AddProducts_Ids, backButton);
            _keyboards.Add(BotUserPlace.AddProducts_Name, backButton);
            _keyboards.Add(BotUserPlace.Filters_ChangeProducts_OwnBag_Add, backButton);
            _keyboards.Add(BotUserPlace.Filters_ChangeProducts_OwnBag_AddShare, backButton);
        }

        private static void InitMenuButtons(KeyboardBuilder builder)
        {
            builder.Clear();

            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.AddProducts, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.ActualDisconts, Color = KeyboardButtonColor.Primary });

            _keyboards.Add(BotUserPlace.Menu, builder.Build());
        }

        private static void InitFiltersButtons(KeyboardBuilder builder)
        {
            builder.Clear();

            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Info, Color = KeyboardButtonColor.Positive });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Percent, Color = KeyboardButtonColor.Primary });
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_MinRating, Color = KeyboardButtonColor.Primary });
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_MinReviews, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Type, Color = KeyboardButtonColor.Primary });
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_ChangeProducts, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Strategy, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Back, Color = KeyboardButtonColor.Secondary });

            _keyboards.Add(BotUserPlace.Filters, builder.Build());
        }

        private static void InitFiltersTypeButtons(KeyboardBuilder builder)
        {
            builder.Clear();

            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Type_None, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Type_OwnBug, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            //builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Type_WhiteList, Color = KeyboardButtonColor.Primary });
            //builder.AddLine();
            //builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_Type_BlackList, Color = KeyboardButtonColor.Primary });
            //builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Back, Color = KeyboardButtonColor.Secondary });

            _keyboards.Add(BotUserPlace.Filters_Type, builder.Build());
        }

        private static void InitAddProductsButtons(KeyboardBuilder builder)
        {
            builder.Clear();

            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.AddProducts_Name, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.AddProducts_Ids, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Back, Color = KeyboardButtonColor.Secondary });

            _keyboards.Add(BotUserPlace.AddProducts, builder.Build());
        }

        private static void InitOwnBagButtons(KeyboardBuilder builder)
        {
            builder.Clear();

            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_OwnBag_ProductsList, Color = KeyboardButtonColor.Positive });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_OwnBag_AddProducts, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_OwnBag_AddShare, Color = KeyboardButtonColor.Primary });
            //builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_OwnBag_RemoveProducts, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Filters_OwnBag_Instruction, Color = KeyboardButtonColor.Primary });
            builder.AddLine();
            builder.AddButton(new AddButtonParams { Label = ExpectedUserMessages.Back, Color = KeyboardButtonColor.Secondary });

            _keyboards.Add(BotUserPlace.Filters_ChangeProducts_OwnBag, builder.Build());
        }
    }
}
