using System.Security.Cryptography;
using System.Text;

namespace XM.ID.Invitations.Net
{
    class HashAlgos
    {
        public string GetHashedValue(string rawData, string hashAlgo)
        {
            if (string.IsNullOrWhiteSpace(rawData))
                return null;

            return hashAlgo switch
            {
                "sha256" => GetSHA256Hash(rawData?.ToLower()),
                "sha384" => GetSHA384Hash(rawData?.ToLower()),
                "sha512" => GetSHA512Hash(rawData?.ToLower()),
                _ => GetSHA512Hash(rawData?.ToLower()),// Incase no hashing algo is specified, it will default to sha512 hash
            };
        }

        public string GetSHA256Hash(string rawData)
        {
            using SHA256 sha256Hash = SHA256.Create();
            // Compute256Hash - returns byte array  
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return ("sha256:" + ByteArrayToString(bytes));
        }

        public string GetSHA384Hash(string rawData)
        {
            using SHA384 sha384Hash = SHA384.Create();
            // Compute384Hash - returns byte array  
            byte[] bytes = sha384Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return ("sha384:" + ByteArrayToString(bytes));
        }

        public string GetSHA512Hash(string rawData)
        {
            using SHA512 sha512Hash = SHA512.Create();
            // Compute512Hash - returns byte array  
            byte[] bytes = sha512Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return ("sha512:" + ByteArrayToString(bytes));
        }

        private string ByteArrayToString(byte[] bytes)
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
