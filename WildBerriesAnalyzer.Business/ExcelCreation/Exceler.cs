using ClosedXML.Excel;
using WildBerriesAnalyzer.Domain.Models;

namespace WildBerriesAnalyzer.Business.ExcelCreation
{
    public class Exceler : ExcelerBase
    {
        public string? CreateDiscontsFile(IEnumerable<Discont> disconts)
        {
            var time = DateTime.UtcNow;

            var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), $"/Excels/Disconts-{time:yyyy-MM-dd HH-mm-ss}.xlsx"));

            var directory = Directory.GetParent(path);

            if (!Directory.Exists(directory.FullName))
            {
                Directory.CreateDirectory(directory.FullName);
            }

            disconts = disconts.OrderByDescending(d => d.DiscontPercent);

            using (XLWorkbook workbook = new XLWorkbook())
            {
                IXLWorksheet worksheet = workbook.AddWorksheet($"Скидки {time:dd-MM-yyyy}");

                WriteColumn(worksheet, 1, "Название товара", disconts.Select(d => d.Product.Name));
                WriteColumn(worksheet, 2, "Размер скидки %", disconts.Select(d => Math.Round(d.DiscontPercent).ToString()));
                WriteColumn(worksheet, 4, "Текущая цена", disconts.Select(d => d.CurrentPrice.ToString()));
                WriteColumn(worksheet, 5, "Дата проверки текущей цены", disconts.Select(d => d.CurrentPrice.CheckTime.ToString("g")));
                WriteColumn(worksheet, 7, "Предыдущая цена", disconts.Select(d => d.ReferencePrice.ToString()));
                WriteColumn(worksheet, 8, "Дата проверки предыдущей цены", disconts.Select(d => d.ReferencePrice.CheckTime.ToString("g")));
                WriteColumn(worksheet, 10, "Рейтинг товара", disconts.Select(d => d.Product.Rating.ToString()));
                WriteColumn(worksheet, 11, "Количество отзывов", disconts.Select(d => d.Product.FeedBacksCount.ToString()));
                WriteColumn(worksheet, 12, "Ссылка на товар", disconts.Select(d => d.Product.Link));

                workbook.SaveAs(path);

                return path;
            }
        }
    }
}
