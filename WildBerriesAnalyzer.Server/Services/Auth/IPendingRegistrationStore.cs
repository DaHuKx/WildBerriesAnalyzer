namespace WildBerriesAnalyzer.Server.Services.Auth
{
    public interface IPendingRegistrationStore
    {
        void Save(PendingRegistration registration);

        PendingRegistration? Get(string registrationId);

        bool TryRemove(string registrationId, out PendingRegistration? registration);

        bool HasActiveLogin(string login);

        bool HasActiveVkId(string vkId);

        void Update(PendingRegistration registration);
    }
}
