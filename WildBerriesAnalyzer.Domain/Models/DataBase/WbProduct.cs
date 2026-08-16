using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using WildBerriesAnalyzer.Domain.Enums;

namespace WildBerriesAnalyzer.Domain.Models.DataBase
{
    /// <summary>
    /// Продукт маркетплейса (WB / Ozon).
    /// </summary>
    public class WbProduct : BaseDbEntity
    {
        /// <summary>
        /// Маркетплейс (Wildberries / Ozon).
        /// </summary>
        public MarketType MarketType { get; set; } = MarketType.Wildberries;

        /// <summary>
        /// Id в магазине (nmId WB / SKU Ozon).
        /// </summary>
        public long IdInMarket { get; set; }

        /// <summary>
        /// Название
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Бренд
        /// </summary>
        public string Brand { get; set; }

        /// <summary>
        /// Id категории товара
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Рейтинг "Wb"
        /// </summary>
        public double Rating { get; set; }

        /// <summary>
        /// Рейтинг пользователей
        /// </summary>
        public double ReviewRating { get; set; }

        /// <summary>
        /// Количество отзывов
        /// </summary>
        public int FeedBacksCount { get; set; }

        /// <summary>
        /// Товар 18+ (adult) по WB viewFlags.
        /// </summary>
        public bool IsAdult { get; set; }

        /// <summary>
        /// Ссылка на изображение
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Ссылка на изображение по размеру
        /// </summary>
        public string SizeImageUrl =>
            string.IsNullOrWhiteSpace(ImageUrl)
                ? ImageUrl
                : ImageUrl.Replace("big", "c246x328");

        /// <summary>
        /// Ссылка на товар
        /// </summary>
        public string Link { get; set; }

        /// <summary>
        /// История цен продукта
        /// </summary>
        public List<WbPrice>? PricesHistory { get; set; }

        /// <summary>
        /// Основная категория (для совместимости с фильтрами; обычно первая из ProductCategories).
        /// </summary>
        public WbCategory? Category { get; set; }

        /// <summary>
        /// Все категории товара (модерация может назначить несколько).
        /// </summary>
        public List<WbProductCategory>? ProductCategories { get; set; }

        /// <summary>
        /// Корзины, в которых используется продукт
        /// </summary>
        public List<WbFilterBag>? Bags { get; set; }

        /// <summary>
        /// Цена при инициализации продукта
        /// </summary>
        [NotMapped]
        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public WbPrice PriceFromInit { get; set; }

        /// <summary>
        /// Последняя цена продукта
        /// </summary>
        [NotMapped]
        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public WbPrice LastPrice
        {
            get
            {
                if (PricesHistory == null || PricesHistory.Count == 0)
                {
                    return new WbPrice
                    {
                        CheckTime = DateTime.UtcNow,
                        Price = 0,
                        ProductId = Id
                    };
                }

                return PricesHistory.OrderBy(price => price.CheckTime).Last();
            }
        }
    }
}
