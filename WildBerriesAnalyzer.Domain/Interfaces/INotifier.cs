namespace WildBerriesAnalyzer.Domain.Interfaces
{
    public interface INotifier
    {
        void Ok(string message);

        void Warning(string message);

        void Error(string message);
    }
}
