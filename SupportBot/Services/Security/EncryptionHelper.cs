using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TelegramEmailBot.Services.Security
{
    public static class EncryptionHelper
    {
        // Метод для шифрования строки.
        public static string EncryptString(string plainText, string key)
        {
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));
            if (key == null) throw new ArgumentNullException(nameof(key));

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

        // Метод для дешифрования строки.
        public static string DecryptString(string cipherText, string key)
        {
            if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));
            if (key == null) throw new ArgumentNullException(nameof(key));

            byte[] fullCipher = Convert.FromBase64String(cipherText);

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = GetAesKey(key);
            // Извлекаем IV (первые 16 байт)
            byte[] iv = new byte[aesAlg.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aesAlg.IV = iv;

            int cipherTextLength = fullCipher.Length - iv.Length;
            byte[] cipher = new byte[cipherTextLength];
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipherTextLength);

            using MemoryStream msDecrypt = new(cipher);
            using CryptoStream csDecrypt = new(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Read);
            using StreamReader srDecrypt = new(csDecrypt, Encoding.UTF8);
            return srDecrypt.ReadToEnd();
        }

        // Преобразование строки-ключа в 256-битный массив ключа с использованием SHA256.
        private static byte[] GetAesKey(string key)
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        }
    }
}
