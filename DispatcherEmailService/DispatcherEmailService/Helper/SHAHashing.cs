using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DispatcherEmailService.Helper
{
    public class SHAHashing
    {
        public static string GetSHA256Hash(string rawData)
        {
            using SHA256 sha256Hash = SHA256.Create();
            // Compute256Hash - returns byte array  
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return ByteArrayToString(bytes);
        }

        private static string ByteArrayToString(byte[] bytes)
        {
            // Convert byte array to a string   
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString().ToLower();
        }
    }
}
