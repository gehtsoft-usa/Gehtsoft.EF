using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Gehtsoft.EF.Utils
{
    /// <summary>
    /// The class that creates the specified hash algorithm for a password.
    ///
    /// The type parameter should be a class derived from `HashAlgorithm`.
    /// </summary>
    public static class HashCreator
    {
        private static ConcurrentDictionary<Type, HashAlgorithm> mHashAlgorithm = new ConcurrentDictionary<Type, HashAlgorithm>();

        private static HashAlgorithm Create(Type type)
        {
            if (!mHashAlgorithm.TryGetValue(type, out var algorithm))
            {
                var method = type.GetMethod(nameof(MD5.Create), new Type[] { });
                if (method == null || !method.IsStatic || !typeof(HashAlgorithm).IsAssignableFrom(method.ReturnType))
                    throw new ArgumentException("The specified type does not contain a parameterless static method Create that returns a HashAlorithm", nameof(type));
                algorithm = method.Invoke(null, Array.Empty<object>()) as HashAlgorithm;
                if (mHashAlgorithm == null)
                    throw new ArgumentException("The Create method returns null", nameof(type));
                mHashAlgorithm.AddOrUpdate(type, algorithm, (a, b) => algorithm);
            }
            return algorithm;
        }

        /// <summary>
        /// Create a password hash as a byte array
        /// </summary>
        /// <param name="type">The hash type (must be a HashAlogorithm-derived class)</param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static byte[] GetBytes(Type type, string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            byte[] encodedPassword = new UTF8Encoding().GetBytes(password);
            HashAlgorithm algorithm = Create(type);
            byte[] hash = algorithm.ComputeHash(encodedPassword);
            return hash;
        }

        public static byte[] GetBytes<T>(string password)
            where T : HashAlgorithm => GetBytes(typeof(T), password);

        /// <summary>
        /// Create a password hash as a string.
        /// </summary>
        /// <param name="type">The hash type (must be a HashAlogorithm-derived class)</param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetHexString(Type type, string password)
        {
            byte[] hash = GetBytes(type, password);
            string encoded = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
            return encoded;
        }

        /// <summary>
        /// Create a password hash as a string.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetHexString<T>(string password)
            where T : HashAlgorithm => GetHexString(typeof(T), password);

        /// <summary>
        /// Create a password hash as a string.
        /// </summary>
        /// <param name="type">The hash type (must be a HashAlogorithm-derived class)</param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetBase64String(Type type, string password)
        {
            byte[] hash = GetBytes(type, password);
            string encoded = Convert.ToBase64String(hash);
            return encoded;
        }

        /// <summary>
        /// Create a password hash as a string.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetBase64String<T>(string password)
            where T : HashAlgorithm => GetBase64String(typeof(T), password);
    }

    /// <summary>
    /// The class that creates MD5 hash for a password.
    /// 
    /// The class is obsolete. Use <see cref="HashCreator"/> class with more secure algorithm (e.g. SHA512) instead.
    /// </summary>
    [Obsolete("MD5 hash is not considered safe enough anymore. Use HashCreator class and specify more secure algorithm as a parameter")]
    public static class MD5HashCreator
    {
        /// <summary>
        /// Create a password hash as a byte array
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static byte[] GetBytes(string password) => HashCreator.GetBytes<MD5>(password);

        /// <summary>
        /// Create a password hash as a string.
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string GetString(string password) => HashCreator.GetHexString<MD5>(password);
    }
}
