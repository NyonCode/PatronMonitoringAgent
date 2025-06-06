using Microsoft.Win32;
using PatronMonitoringAgent.Common;
using System;
using System.Security.Cryptography;
using System.Text;

namespace PatronMonitoringAgent
{
    public class RegistryConfigurationProvider : IConfigurationProvider
    {
        private const string RegPath = @"SOFTWARE\NyonCode\PatronMonitoringAgent";
        private const string AesKeyName = "AesKey";
        private const string UUIDName = "UUID";
        private static readonly byte[] AesKey = GetOrCreateAesKey();

        public string GetToken() => DecryptFromRegistry("Token");
        public string GetUUID() => GetOrCreateUUID();
        public bool UUIDExist() => IsExistUUID();
        public bool TokenExist() => IsSetToken();
        public bool IntervalExist() => IsSetInterval();
        public int GetInterval() => int.TryParse(GetFromRegistry("Interval"), out var v) ? v : 60;

        public void SaveToken(string token) => SetEncryptedToRegistry("Token", token);
        public void SaveInterval(int? interval) => SetToRegistry("Interval", interval.ToString());

        /// <summary>
        /// Retrieves or creates an AES key for encryption/decryption.
        /// </summary>
        /// <returns>
        /// A byte array representing the AES key.
        /// </returns>
        private static byte[] GetOrCreateAesKey()
        {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(RegPath))
            {
                var protectedKey = key.GetValue(AesKeyName) as byte[];
                if (protectedKey != null)
                {
                    // Decrypt the key using DPAPI
                    return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.LocalMachine);
                }
                // Generate a new key
                var aesKey = new byte[16];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(aesKey);
                }
                // Encrypt the key using DPAPI
                var encryptedKey = ProtectedData.Protect(aesKey, null, DataProtectionScope.LocalMachine);
                key.SetValue(AesKeyName, encryptedKey, RegistryValueKind.Binary);
                return aesKey;
            }
        }

        /// <summary>
        /// Retrieves a string value from the registry under the specified name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>
        /// The string value from the registry, or null if not found.
        /// </returns>
        private string GetFromRegistry(string name)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath))
            {
                return key?.GetValue(name)?.ToString();
            }
        }

        /// <summary>
        /// Sets a string value to the registry under the specified name.
        /// If the key does not exist, it will be created.
        /// </summary>
        /// <param name="name">The name of the registry value.</param>
        /// <param name="value">The string value to set.</param>
        /// <remarks>
        /// This method creates or opens the registry key at the specified path and sets the value.
        /// If the key already exists, it will overwrite the existing value.
        /// </remarks>
        private void SetToRegistry(string name, string value)
        {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(RegPath))
                key.SetValue(name, value);
        }

        /// <summary>
        /// Encrypts a string value and sets it to the registry under the specified name.
        /// If the key does not exist, it will be created.
        /// </summary>
        /// <param name="name">The name of the registry value.</param>
        /// <param name="value">The string value to encrypt and set.</param>
        /// <remarks>
        /// This method uses AES encryption to secure the value before storing it in the registry.
        /// If the key already exists, it will overwrite the existing value.
        /// The AES key is stored securely in the registry and used for encryption/decryption.
        /// The encrypted value is stored as a Base64 string to ensure it can be safely written to the registry.
        /// The IV (Initialization Vector) is prepended to the encrypted data to allow decryption later.
        /// </remarks>
        private void SetEncryptedToRegistry(string name, string value)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = AesKey;
                aes.GenerateIV();
                var encryptor = aes.CreateEncryptor();
                var bytes = Encoding.UTF8.GetBytes(value);
                var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
                var result = new byte[aes.IV.Length + encrypted.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
                SetToRegistry(name, Convert.ToBase64String(result));
            }
        }

        /// <summary>
        /// Decrypts a string value from the registry under the specified name.
        /// If the value is not found or cannot be decrypted, returns null.
        /// </summary>
        /// 
        /// <param name="name">The name of the registry value.</param>
        /// 
        /// <returns>
        /// The decrypted string value from the registry, or null if not found or decryption fails.
        /// </returns>
        private string DecryptFromRegistry(string name)
        {
            var val = GetFromRegistry(name);
            if (string.IsNullOrEmpty(val)) return null;
            var raw = Convert.FromBase64String(val);
            using (var aes = Aes.Create())
            {
                aes.Key = AesKey;
                var iv = new byte[16];
                Buffer.BlockCopy(raw, 0, iv, 0, 16);
                aes.IV = iv;
                var decryptor = aes.CreateDecryptor();
                var decrypted = decryptor.TransformFinalBlock(raw, 16, raw.Length - 16);
                return Encoding.UTF8.GetString(decrypted);
            }
        }

        /// <summary>
        /// Returns existing UUID or generates and stores a new one if not present.
        /// </summary>
        /// 
        /// <returns>
        /// The UUID as a string.
        /// </returns>
        private string GetOrCreateUUID()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath))
            {
                var uuid = key.GetValue(UUIDName) as string;
                if (string.IsNullOrEmpty(uuid))
                {
                    uuid = Guid.NewGuid().ToString();
                    key.SetValue(UUIDName, uuid);
                }
                return uuid;
            }
        }

        /// <summary>
        /// Returns true if UUID exists in the registry, false otherwise.
        /// </summary>
        /// 
        /// <returns>
        /// Boolean indicating whether UUID exists.
        /// </returns>
        private bool IsExistUUID()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath))
            {
                var uuid = key.GetValue(UUIDName) as string;
                return !string.IsNullOrEmpty(uuid);
            }
        }

        /// <summary>
        /// Returns true if Token exists in the registry, false otherwise.
        /// </summary>
        /// 
        /// <returns>
        /// Boolean indicating whether Token exists.
        /// </returns>
        private bool IsSetToken()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath))
            {
                var token = key.GetValue("Token") as string;
                return !string.IsNullOrEmpty(token);
            }
        }

        /// <summary>
        /// Returns true if Interval exists in the registry, false otherwise.
        /// </summary>
        /// 
        /// <returns>
        /// Boolean indicating whether Interval exists.
        /// </returns>
        private bool IsSetInterval()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(RegPath))
            {
                var interval = key.GetValue("Interval") as string;
                return !string.IsNullOrEmpty(interval);
            }
        }

    }
}