using System.Diagnostics.CodeAnalysis;

namespace MessengerServer.AppHost.APIResponse
{
    internal sealed record APIResponse<T>
    {
        [MemberNotNullWhen(true, nameof(Data))]
        public required bool IsSuccess { get; init; }
        public T? Data { get; init; }
        public APIError APIError { get; init; }
        public List<APIFieldError> FieldErrors { get; init; } = [];

        public static string GetDataPropertyName() => nameof(Data);
    }
}
