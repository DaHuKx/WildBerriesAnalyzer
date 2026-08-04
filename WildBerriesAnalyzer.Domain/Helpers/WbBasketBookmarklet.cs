using System;

namespace WildBerriesAnalyzer.Domain.Helpers
{
    /// <summary>
    /// Bookmarklet для выгрузки артикулов из корзины Wildberries в буфер обмена.
    /// </summary>
    public static class WbBasketBookmarklet
    {
        /// <summary>
        /// Имя закладки, которое удобно показать пользователю.
        /// </summary>
        public const string BookmarkName = "PriceLab: артикулы корзины WB";

        /// <summary>
        /// JS без префикса javascript: — выполняется в контексте страницы корзины WB.
        /// $x из DevTools здесь недоступен, поэтому используется document.evaluate.
        /// </summary>
        public const string ScriptBody =
            "(function(){" +
            "var xp=\"//div[contains(@class,'j-b-basket-item')]//a[contains(@class,'good-info__title')]\";" +
            "var nodes=document.evaluate(xp,document,null,XPathResult.ORDERED_NODE_SNAPSHOT_TYPE,null);" +
            "var articles=[],seen={};" +
            "for(var i=0;i<nodes.snapshotLength;i++){" +
            "var href=(nodes.snapshotItem(i).href||'');" +
            "var m=href.match(/\\/catalog\\/(\\d+)\\//);" +
            "if(m&&!seen[m[1]]){seen[m[1]]=1;articles.push(m[1]);}" +
            "}" +
            "if(!articles.length){" +
            "alert('Артикулы не найдены. Откройте корзину WB на компьютере и прокрутите список до конца.');" +
            "return;" +
            "}" +
            "var text=articles.join(' ');" +
            "var ok=function(){alert('Готово! Артикулов: '+articles.length+'. Список в буфере — вставьте в PriceLab или бота.');};" +
            "var fallback=function(){window.prompt('Скопируйте артикулы ('+articles.length+'):',text);};" +
            "if(navigator.clipboard&&navigator.clipboard.writeText){" +
            "navigator.clipboard.writeText(text).then(ok).catch(fallback);" +
            "}else{fallback();}" +
            "})();";

        /// <summary>
        /// Готовый URL закладки: javascript:...
        /// </summary>
        public static string BookmarkletUri => "javascript:" + System.Uri.EscapeDataString(ScriptBody);

        /// <summary>
        /// Краткая инструкция для бота / UI.
        /// </summary>
        public static string ShortInstructions =>
            "ИМПОРТ ИЗ КОРЗИНЫ WILDBERRIES\n\n" +
            "Нужен компьютер и браузер (телефон не подойдёт).\n\n" +
            "1. Скопируйте закладку PriceLab (текст начинается с javascript:).\n" +
            "2. В браузере создайте новую закладку, в поле адреса вставьте скопированный текст, сохраните.\n" +
            "3. Откройте корзину wildberries.ru и прокрутите товары до конца.\n" +
            "4. Нажмите сохранённую закладку — артикулы скопируются в буфер.\n" +
            "5. Вставьте список в бота или в PriceLab.\n\n" +
            "Если закладка не сработала — обновите страницу корзины и снова прокрутите список вниз.";
    }
}
