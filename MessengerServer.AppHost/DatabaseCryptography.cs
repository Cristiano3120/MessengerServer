using DotNetEnv;
using Konscious.Security.Cryptography;
using MessengerServer.AppHost.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MessengerServer.AppHost
{
    internal static class DatabaseCryptography
    {
        private static readonly HashAlgorithmName _hashAlgorithmName = HashAlgorithmName.SHA256;
        private const int HashingIterations = 100_000;
        private static readonly byte[] _key = [];
        private const int TagSize = 16;

        static DatabaseCryptography() 
            => _key = GetKey();

        private static byte[] GetKey()
        {
            const int KeySize = 32;

            byte[] _salt = Encoding.UTF8.GetBytes(Env.GetString("ENCRYPTION_POSTGRES"));
            byte[] _password = Encoding.UTF8.GetBytes(Env.GetString("ENCRYPTION_PASSWORD"));
            int _memorySize = 64 * 1024;

            Argon2i argon2 = new(_password)
            {
                DegreeOfParallelism = Environment.ProcessorCount,
                Salt = _salt,
                Iterations = 3,
                MemorySize = _memorySize,
            };

            byte[] derived = argon2.GetBytes(KeySize);
            byte[] key = new byte[KeySize];

            Buffer.BlockCopy(derived, 0, key, 0, KeySize);

            return key;
        }

        public static byte[] Hash(string text)
        {
            Span<byte> salt = stackalloc byte[16];
            RandomNumberGenerator.Fill(salt);

            using Rfc2898DeriveBytes pbkdf2 = new(
                password: text,
                salt: salt.ToArray(),
                iterations: HashingIterations,
                _hashAlgorithmName);

            byte[] hash = pbkdf2.GetBytes(32);

            byte[] hashWithSalt = new byte[hash.Length + salt.Length];
            Buffer.BlockCopy(hash, 0, hashWithSalt, 0, hash.Length);
            salt.CopyTo(hashWithSalt.AsSpan(hash.Length));

            return hashWithSalt;
        }


        public static bool VerifyHash(string password, byte[] storedHashWithSalt)
        {
            const byte hashSize = 32;
            (byte[] hash, byte[] salt) = Helper.GetSaltFromData(storedHashWithSalt, hashSize);

            using Rfc2898DeriveBytes pbkdf2 = new(password, salt, HashingIterations, _hashAlgorithmName);
            byte[] computedHash = pbkdf2.GetBytes(hashSize);

            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }

        public static byte[] Encrypt(string plainText)
        {
            using AesGcm aesGcm = new(_key, TagSize);

            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);

            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[TagSize];

            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // alles zusammenpacken: [nonce | tag | ciphertext]
            byte[] result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return result;
        }


        public static string Decrypt(byte[] encryptedData)
        {
            using AesGcm aesGcm = new(_key, TagSize);

            byte[] nonce = new byte[12];
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[encryptedData.Length - nonce.Length - tag.Length];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(encryptedData, nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(encryptedData, nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

            byte[] plaintextBytes = new byte[ciphertext.Length];
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
    }
}
