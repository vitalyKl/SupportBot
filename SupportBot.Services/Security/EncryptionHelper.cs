using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SupportBot.Services.Security
{
    public static class EncryptionHelper
    {
        public static string EncryptString(string plainText, string key)
        {
            ArgumentNullException.ThrowIfNull(plainText, nameof(plainText));
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = GetAesKey(key);
            aesAlg.GenerateIV();
            byte[] iv = aesAlg.IV;

            using MemoryStream msEncrypt = new();
            // Записываем IV в начало потока
            msEncrypt.Write(iv, 0, iv.Length);

            using CryptoStream csEncrypt = new(msEncrypt, aesAlg.CreateEncryptor(aesAlg.Key, iv), CryptoStreamMode.Write);
            using (StreamWriter swEncrypt = new(csEncrypt, Encoding.UTF8))
            {
                swEncrypt.Write(plainText);
            }
            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        public static string DecryptString(string cipherText, string key)
        {
            ArgumentNullException.ThrowIfNull(cipherText, nameof(cipherText));
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            byte[] fullCipher = Convert.FromBase64String(cipherText);
            using Aes aesAlg = Aes.Create();
            aesAlg.Key = GetAesKey(key);
            int ivLength = aesAlg.BlockSize / 8;
            if (fullCipher.Length < ivLength)
            {
                throw new ArgumentException("Cipher text is too short, cannot extract IV.", nameof(cipherText));
            }
            byte[] iv = new byte[ivLength];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aesAlg.IV = iv;

            int cipherTextLength = fullCipher.Length - iv.Length;
            if (cipherTextLength <= 0)
            {
                throw new ArgumentException("Cipher text does not contain payload.", nameof(cipherText));
            }
            byte[] cipher = new byte[cipherTextLength];
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipherTextLength);

            using MemoryStream msDecrypt = new(cipher);
            using CryptoStream csDecrypt = new(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Read);
            using StreamReader srDecrypt = new(csDecrypt, Encoding.UTF8);
            return srDecrypt.ReadToEnd();
        }

        private static byte[] GetAesKey(string key)
        {
            // Используем статический метод HashData (начиная с .NET 6)
            return SHA256.HashData(Encoding.UTF8.GetBytes(key));
        }
    }
}
