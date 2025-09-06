using MessengerServer.AppHost.UserResources;

namespace MessengerServer.AppHost
{
    public sealed class AuthControllerFields
    {
        public Dictionary<ulong, (int code, Timer timer)> VerificationCodes { get; init; } = [];
        public List<EncryptedUser> FailedUsers { get; init; } = [];
        public Task? SaveFailedUsersTask { get; set; }
    }
}
