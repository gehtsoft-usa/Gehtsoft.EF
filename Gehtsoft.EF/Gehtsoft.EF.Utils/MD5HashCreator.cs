using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Gehtsoft.EF.Utils
{
    /// <summary>
    /// The class that creates MD5 hash for a password
    /// </summary>
    public static class MD5HashCreator
    {
        /// <summary>
        /// Create a password hash as a byte array
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static byte[] GetBytes(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            byte[] encodedPassword = new UTF8Encoding().GetBytes(password);
            MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(encodedPassword);
            return hash;
        }

        /// <summary>
        /// Create a password hash as a string.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetString(string password)
        {
            byte[] hash = GetBytes(password);
            string encoded = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
            return encoded;
        }
    }
}
