namespace MessengerServer.AppHost.APIResponse
{
    internal sealed record APIFieldError
    {
        public required string Field { get; init; }
        public required string Message { get; init; }
    }
}
