namespace WildBerriesAnalyzer.Business.Helpers
{
    /// <summary>
    /// Строит URL изображений товара на CDN Wildberries (basket-XX.wbbasket.ru).
    /// </summary>
    /// <remarks>
    /// Формула пути стабильна:
    ///   vol  = nmId / 100_000
    ///   part = nmId / 1_000
    ///   https://basket-{NN}.wbbasket.ru/vol{vol}/part{part}/{nmId}/images/big/1.webp
    ///
    /// Номер корзины NN зависит от vol и периодически расширяется WB.
    /// Таблица актуализирована по живым HEAD-пробам (2026-08); для vol выше
    /// последнего известного диапазона используется экстраполяция ~230 vol на корзину.
    /// </remarks>
    public static class WbProductImageUrlBuilder
    {
        /// <summary>
        /// Inclusive upper bound of vol for each basket host (1-based host number).
        /// </summary>
        private static readonly (int MaxVol, int Basket)[] BasketRanges =
        [
            (143, 1),
            (287, 2),
            (431, 3),
            (719, 4),
            (1007, 5),
            (1061, 6),
            (1115, 7),
            (1169, 8),
            (1313, 9),
            (1601, 10),
            (1655, 11),
            (1919, 12),
            (2045, 13),
            (2189, 14),
            (2405, 15),
            (2621, 16),
            (2837, 17),
            (3063, 18),
            (3284, 19),
            (3507, 20),
            (3701, 21),
            (3972, 22),
            (4151, 23),
            (4355, 24),
            (4598, 25),
            (4884, 26),
            (5190, 27),
            (5522, 28),
            (5813, 29),
            (6136, 30),
            (6444, 31),
            (6770, 32),
            (7080, 33),
            (7399, 34),
            (7689, 35),
            (8002, 36),
            (8317, 37),
            (8760, 38),
            (9178, 39),
            (9617, 40),
            (10396, 41),
            (11173, 42),
            (11920, 43),
            (12709, 44),
            (13100, 45)
        ];

        private const int ApproxVolsPerBasket = 230;

        public static string BuildBigImageUrl(long nmId, int imageIndex = 1)
        {
            if (nmId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nmId));
            }

            if (imageIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(imageIndex));
            }

            var vol = nmId / 100_000L;
            var part = nmId / 1_000L;
            var basket = ResolveBasketNumber((int)vol);

            return $"https://basket-{basket:D2}.wbbasket.ru/vol{vol}/part{part}/{nmId}/images/big/{imageIndex}.webp";
        }

        public static int ResolveBasketNumber(int vol)
        {
            if (vol < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vol));
            }

            foreach (var (maxVol, basket) in BasketRanges)
            {
                if (vol <= maxVol)
                {
                    return basket;
                }
            }

            var last = BasketRanges[^1];
            var extra = vol - last.MaxVol;
            var offset = (extra + ApproxVolsPerBasket - 1) / ApproxVolsPerBasket;
            return last.Basket + Math.Max(1, offset);
        }
    }
}
