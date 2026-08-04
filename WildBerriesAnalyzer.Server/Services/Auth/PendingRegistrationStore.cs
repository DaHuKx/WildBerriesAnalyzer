using System.Collections.Concurrent;

namespace WildBerriesAnalyzer.Server.Services.Auth
{
    public sealed class PendingRegistrationStore : IPendingRegistrationStore
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<string, PendingRegistration> _items = new(StringComparer.Ordinal);
        private DateTime _lastCleanupUtc = DateTime.UtcNow;

        public void Save(PendingRegistration registration)
        {
            CleanupExpired();
            _items[registration.RegistrationId] = registration;
        }

        public PendingRegistration? Get(string registrationId)
        {
            CleanupExpired();
            if (string.IsNullOrWhiteSpace(registrationId))
            {
                return null;
            }

            if (!_items.TryGetValue(registrationId.Trim(), out var item))
            {
                return null;
            }

            if (item.ExpiresAtUtc < DateTime.UtcNow)
            {
                _items.TryRemove(registrationId.Trim(), out _);
                return null;
            }

            return item;
        }

        public bool TryRemove(string registrationId, out PendingRegistration? registration)
        {
            registration = null;
            if (string.IsNullOrWhiteSpace(registrationId))
            {
                return false;
            }

            if (!_items.TryRemove(registrationId.Trim(), out var item))
            {
                return false;
            }

            registration = item;
            return true;
        }

        public bool HasActiveLogin(string login)
        {
            CleanupExpired();
            if (string.IsNullOrWhiteSpace(login))
            {
                return false;
            }

            var normalized = login.Trim();
            return _items.Values.Any(x =>
                x.ExpiresAtUtc >= DateTime.UtcNow &&
                string.Equals(x.Login, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public bool HasActiveVkId(string vkId)
        {
            CleanupExpired();
            if (string.IsNullOrWhiteSpace(vkId))
            {
                return false;
            }

            var normalized = vkId.Trim();
            return _items.Values.Any(x =>
                x.ExpiresAtUtc >= DateTime.UtcNow &&
                string.Equals(x.VkId, normalized, StringComparison.Ordinal));
        }

        public void Update(PendingRegistration registration)
        {
            _items[registration.RegistrationId] = registration;
        }

        private void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            if (now - _lastCleanupUtc < CleanupInterval)
            {
                return;
            }

            _lastCleanupUtc = now;
            foreach (var pair in _items)
            {
                if (pair.Value.ExpiresAtUtc < now)
                {
                    _items.TryRemove(pair.Key, out _);
                }
            }
        }
    }
}
