using MessengerServer.AppHost.Logging;

namespace MessengerServer.AppHost.UserResources
{
    public sealed record User
    {
        public string? Email { get; init; }

        [Filter("***")]
        public string? Password { get; init; }
        public required string Username { get; init; }

        [Filter("byte[]")]
        public required byte[] ProfilPicture { get; init; }
        public required ulong Id { get; init; }
        public required string? Biography { get; init; }

        /// <summary> Two Factor Authentication </summary>
        public bool? TFAEnabled { get; init; }
        public DateOnly Birthday { get; init; }
    }
}
