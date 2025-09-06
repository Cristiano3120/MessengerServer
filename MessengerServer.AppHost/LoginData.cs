namespace MessengerServer.AppHost
{
    public sealed record LoginData
    {
        public required string Email { get; init; }
        public required string Password { get; init; }
        public required bool IsAutoLogin { get; init; }
    }
}
