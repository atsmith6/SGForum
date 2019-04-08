using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace SGDataModel
{
    public class CryptoUtils
    {
        public static (string, string) CreateHashAndSalt(string password)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));
			
			string saltStr = Convert.ToBase64String(salt);
			return (saltStr, hashed);
        }

		public static bool CheckPassword(string password, string passwordHash, string salt)
		{
			byte[] saltBytes = Convert.FromBase64String(salt);

			byte[] hashBytes =  KeyDerivation.Pbkdf2(
				password: password,
				salt: saltBytes,
				prf: KeyDerivationPrf.HMACSHA1,
				iterationCount: 10000,
				numBytesRequested: 256 / 8);

			string hashed = Convert.ToBase64String(hashBytes);
			
			return passwordHash == hashed;
		}
    }
}