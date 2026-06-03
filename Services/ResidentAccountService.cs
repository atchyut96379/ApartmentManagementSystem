using System.Security.Cryptography;

namespace ApartmentManagementSystem.Services
{
    public class ResidentAccountService
    {
        public string GenerateTemporaryPassword()
        {
            const string chars =
                "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$";

            var bytes = new byte[12];
            RandomNumberGenerator.Fill(bytes);
            var result = new char[12];

            for (var i = 0; i < result.Length; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }

            return new string(result);
        }
    }
}
