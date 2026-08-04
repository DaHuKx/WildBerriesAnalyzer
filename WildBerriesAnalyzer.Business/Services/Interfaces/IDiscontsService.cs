using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Business.Services.Interfaces
{
    public interface IDiscontsService
    {
        List<Discont> GetDiscontsFromProducts(IEnumerable<WbProduct> products, ReferencePriceStrategy strategy);
    }
}
