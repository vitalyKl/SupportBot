using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SupportBot.Core.Configurations;
using SupportBot.Core.Interfaces.Security;

namespace SupportBot.Infrastructure
{
    public class AesEncryptionService : IAesEncryptionService
    {
        private readonly string _encryptionKey;

        public AesEncryptionService(IOptions<BotSettings> options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _encryptionKey = options.Value.EncryptionKey;
            if (string.IsNullOrWhiteSpace(_encryptionKey))
                throw new InvalidOperationException("Encryption key is not configured.");
        }

        public string Encrypt(string plainText)
        {
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = GetAesKey(_encryptionKey);
            aesAlg.GenerateIV();
            byte[] iv = aesAlg.IV;

            using MemoryStream msEncrypt = new MemoryStream();
            // Записываем IV в начало потока
            msEncrypt.Write(iv, 0, iv.Length);

            using CryptoStream csEncrypt = new CryptoStream(msEncrypt, aesAlg.CreateEncryptor(aesAlg.Key, iv), CryptoStreamMode.Write);
            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt, Encoding.UTF8))
            {
                swEncrypt.Write(plainText);
            }
            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (cipherText == null) throw new ArgumentNullException(nameof(cipherText));

            byte[] fullCipher = Convert.FromBase64String(cipherText);

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = GetAesKey(_encryptionKey);

            // Извлекаем IV (первые 16 байт)
            byte[] iv = new byte[aesAlg.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            aesAlg.IV = iv;

            int cipherTextLength = fullCipher.Length - iv.Length;
            byte[] cipher = new byte[cipherTextLength];
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipherTextLength);

            using MemoryStream msDecrypt = new MemoryStream(cipher);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV), CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8);
            return srDecrypt.ReadToEnd();
        }

        private byte[] GetAesKey(string key)
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        }
    }
}
