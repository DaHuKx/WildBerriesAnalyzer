using System.Text;
using WildBerriesAnalyzer.GPT.Models;

namespace WildBerriesAnalyzer.GPT
{
    public static class PromtBases
    {
        public static string GetInitCategoriesPromt(List<ProductPromtDTO> products, List<string> categories)
        {
            if (products == null || products.Count == 0)
                throw new ArgumentException("Список товаров не может быть пустым", nameof(products));

            categories ??= new List<string>();

            var sb = new StringBuilder();

            sb.AppendLine("Ты — AI-классификатор товаров для e-commerce. Твоя задача — сопоставить названия товаров с существующими категориями из справочника или предложить новые категории, если подходящей нет.");
            sb.AppendLine();

            // ⚠️ ЖЁСТКИЕ ПРАВИЛА — в самом верху
            sb.AppendLine("КРИТИЧЕСКИЕ ПРАВИЛА (нарушение любого из них = ошибка):");
            sb.AppendLine("1. ЯЗЫК: Все названия НОВЫХ категорий — СТРОГО на РУССКОМ. Никаких английских слов (Smartphones, Cases, Accessories и т.п.).");
            sb.AppendLine("2. PRODUCT_ID: Используй ТОЧНЫЕ значения product_id из входных данных. НЕ выдумывай свои, НЕ нумеруй с 1, НЕ пропускай. Если во входе ID=0 — в ответе тоже должен быть 0.");
            sb.AppendLine("3. УРОВЕНЬ АБСТРАКАЦИИ: Категории должны быть ОБЩИМИ, а не по бренду/модели.");
            sb.AppendLine("   • ПРАВИЛЬНО: \"Смартфоны\", \"Ноутбуки\", \"Телевизоры\", \"Аудиотехника\"");
            sb.AppendLine("   • НЕПРАВИЛЬНО: \"Смартфоны iPhone 17\", \"Ноутбуки ASUS ROG\", \"Телевизоры Samsung QLED\"");
            sb.AppendLine("   • Бренд и модель — это ХАРАКТЕРИСТИКА товара, а не категория.");
            sb.AppendLine("4. ТОЛЬКО РЕАЛЬНЫЕ ТОВАРЫ: Создавай новую категорию ТОЛЬКО если в списке ЕСТЬ товары, которые в неё попадают. Не придумывай категории для сопутствующих товаров (чехлы, стёкла, зарядки), если их НЕТ во входном списке.");
            sb.AppendLine("5. БЕЗ MARKDOWN: Никаких ```json, никаких обёрток. Чистый JSON.");
            sb.AppendLine();

            // Инструкции
            sb.AppendLine("ИНСТРУКЦИИ:");
            sb.AppendLine("1. Для каждого товара найди ОДНУ наиболее точную категорию из справочника. Учитывай синонимы и отраслевую терминологию.");
            sb.AppendLine("2. Если подходящей категории нет — укажи \"category_id\": null для этого товара. ВАЖНО: product_id бери СТРОГО из входных данных.");
            sb.AppendLine("3. Собери глобальный список НОВЫХ категорий. Требования:");
            sb.AppendLine("   • Только общие категории (без брендов и моделей)");
            sb.AppendLine("   • Только на русском языке");
            sb.AppendLine("   • Только если в списке ЕСТЬ товары для этой категории");
            sb.AppendLine("   • Без дублей и синонимов (не \"Смартфоны\" и \"Мобильные телефоны\" одновременно)");
            sb.AppendLine("4. Выведи СТРОГО два JSON-массива. Каждый на отдельной строке. БЕЗ markdown, БЕЗ пояснений, БЕЗ преамбулы.");
            sb.AppendLine();

            // Пример вывода — с реальными ID и общими категориями
            sb.AppendLine("ПРИМЕР ПРАВИЛЬНОГО ВЫВОДА:");
            sb.AppendLine("[{\"product_id\": 0, \"category_id\": \"CAT_0001\"}, {\"product_id\": 0, \"category_id\": null}, {\"product_id\": 5, \"category_id\": \"CAT_0002\"}]");
            sb.AppendLine("[\"Смартфоны\", \"Ноутбуки\", \"Аудиотехника\"]");

            // Блок существующих категорий
            sb.AppendLine("СУЩЕСТВУЮЩИЕ КАТЕГОРИИ (ID -> Название):");
            if (categories.Count == 0)
            {
                sb.AppendLine("- (справочник пуст)");
            }
            else
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    sb.AppendLine($"- CAT_{i + 1:D4}: {categories[i]}");
                }
            }
            sb.AppendLine();

            // Блок товаров
            sb.AppendLine("ТОВАРЫ ДЛЯ КЛАССИФИКАЦИИ:");
            foreach (var p in products)
            {
                var nameJson = p.Name;
                var categoryHint = string.IsNullOrWhiteSpace(p.Category)
                    ? ""
                    : $", текущая категория: {p.Category}";
                sb.AppendLine($"- ID: {p.ProductId}, Название: {nameJson}{categoryHint}");
            }
            sb.AppendLine();

            return sb.ToString();
        }

        public static string GetCategoryPromt(string productName, List<string> categories)
        {
            if (string.IsNullOrWhiteSpace(productName))
                throw new ArgumentException("Название товара не может быть пустым", nameof(productName));

            categories ??= new List<string>();

            var sb = new StringBuilder();

            sb.AppendLine("Ты — AI-классификатор товаров для e-commerce. Определи категорию для товара.");
            sb.AppendLine();

            // Критические правила
            sb.AppendLine("КРИТИЧЕСКИЕ ПРАВИЛА:");
            sb.AppendLine("1. ЯЗЫК: Все названия НОВЫХ категорий — СТРОГО на РУССКОМ. Никаких английских слов (Smartphones, Cases, Accessories и т.п.).");
            sb.AppendLine("2. УРОВЕНЬ АБСТРАКАЦИИ: Категории должны быть ОБЩИМИ, а не по бренду/модели.");
            sb.AppendLine("   • ПРАВИЛЬНО: \"Смартфоны\", \"Ноутбуки\", \"Телевизоры\", \"Аудиотехника\"");
            sb.AppendLine("   • НЕПРАВИЛЬНО: \"Смартфоны iPhone 17\", \"Ноутбуки ASUS ROG\", \"Телевизоры Samsung QLED\"");
            sb.AppendLine("   • Бренд и модель — это ХАРАКТЕРИСТИКА товара, а не категория.");
            sb.AppendLine("3. БЕЗ MARKDOWN: Никаких ```json, никаких обёрток.");
            sb.AppendLine();

            // Существующие категории
            sb.AppendLine("СУЩЕСТВУЮЩИЕ КАТЕГОРИИ (ID -> Название):");
            if (categories.Count == 0)
            {
                sb.AppendLine("- (справочник пуст)");
            }
            else
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    sb.AppendLine($"- {i + 1:D4}: {categories[i]}");
                }
            }
            sb.AppendLine();

            // Товар
            sb.AppendLine("ТОВАР ДЛЯ КЛАССИФИКАЦИИ:");
            sb.AppendLine($"- Название: {productName}");
            sb.AppendLine();

            // Инструкции
            sb.AppendLine("ИНСТРУКЦИИ:");
            sb.AppendLine("1. Найди ОДНУ наиболее точную категорию из справочника. Учитывай синонимы и отраслевую терминологию.");
            sb.AppendLine("3. Если предлагаешь НОВУЮ категорию:");
            sb.AppendLine("   • Только общая категория (без брендов и моделей)");
            sb.AppendLine("   • Только на русском языке");
            sb.AppendLine("   • Без дублей существующих категорий и их синонимов");
            sb.AppendLine("4. Выведи СТРОГО название категории без лишних слов");
            sb.AppendLine();

            return sb.ToString();
        }
    }
}
