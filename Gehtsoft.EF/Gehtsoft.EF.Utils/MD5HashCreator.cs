using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;


namespace Gehtsoft.EF.Utils
{
    public static class MD5HashCreator
    {
        public static byte[] GetBytes(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            byte[] encodedPassword = new UTF8Encoding().GetBytes(password);
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(encodedPassword);
            return hash;
        }

        public static string GetString(string password)
        {
            byte[] hash = GetBytes(password);
            string encoded = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
            return encoded;
        }
    }
}
