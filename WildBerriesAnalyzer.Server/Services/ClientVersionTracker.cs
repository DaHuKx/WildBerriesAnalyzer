using WildBerriesAnalyzer.Data.Repositories.Interfaces;

namespace WildBerriesAnalyzer.Server.Services
{
    public sealed class ClientVersionTracker : IClientVersionTracker
    {
        private readonly IUsersRepository _usersRepository;

        public ClientVersionTracker(IUsersRepository usersRepository)
        {
            _usersRepository = usersRepository;
        }

        public async Task TrackFromRequestAsync(
            int userId,
            HttpRequest request,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0 ||
                !request.Headers.TryGetValue(ClientVersionHeaders.ClientVersion, out var versionValues))
            {
                return;
            }

            var platform = request.Headers.TryGetValue(ClientVersionHeaders.ClientPlatform, out var platformValues)
                ? platformValues.ToString()
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(platform) &&
                !platform.Equals("mobile", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var version = ClientSemVersion.Normalize(versionValues.ToString());
            if (version is null)
            {
                return;
            }

            await _usersRepository.TryUpdateMobileClientVersionAsync(userId, version);
        }
    }
}
