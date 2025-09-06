namespace MessengerServer.AppHost
{
    internal static class Helper
    {
        public static (byte[] hash, byte[] salt) GetSaltFromData(byte[] data, byte hashSize = 32)
        {
            const int saltSize = 16;

            byte[] storedSalt = new byte[saltSize];
            Buffer.BlockCopy(data, hashSize, storedSalt, 0, saltSize);

            byte[] storedHash = new byte[hashSize];
            Buffer.BlockCopy(data, 0, storedHash, 0, hashSize);

            return (storedHash, storedSalt);
        }

        public static string GetDynamicPath(string relativePath)
        {
            string projectBasePath = AppContext.BaseDirectory;
            int binIndex = projectBasePath.IndexOf($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

            if (binIndex == -1)
            {
                throw new Exception("Could not determine project base path!");
            }

            projectBasePath = projectBasePath[..binIndex];
            return Path.Combine(projectBasePath, relativePath);
        }
        public static string ToCamelCase(this string str)
        {
            if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            {
                return str;
            }

            return char.ToLower(str[0]) + str[1..];
        }
    }
}
