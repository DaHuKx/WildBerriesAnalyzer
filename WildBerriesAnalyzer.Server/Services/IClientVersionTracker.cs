namespace WildBerriesAnalyzer.Server.Services
{
    public interface IClientVersionTracker
    {
        Task TrackFromRequestAsync(int userId, HttpRequest request, CancellationToken cancellationToken = default);
    }
}
