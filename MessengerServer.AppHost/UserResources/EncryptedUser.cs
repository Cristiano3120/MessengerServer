using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace MessengerServer.AppHost.UserResources
{
    [Index(nameof(EmailHash), IsUnique = true)]
    [Index(nameof(Username), IsUnique = true)]
    public sealed class EncryptedUser
    {
        [Key]
        public ulong Id { get; init; }
        public byte[] Email { get; init; }
        public string Username { get; init; }
        public byte[] ProfilPicture { get; init; }
        public string Biography { get; init; }
        public bool TFAEnabled { get; init; }
        public byte[] EmailHash { get; init; }
        public byte[] PasswordHash { get; init; }
        public DateOnly Birthday { get; init; }
    }
}
