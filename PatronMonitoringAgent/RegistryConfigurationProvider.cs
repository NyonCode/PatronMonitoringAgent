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

        private static byte[] GetOrCreateAesKey()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(RegPath))
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

        private string GetFromRegistry(string name)
        {
            using (var key = Registry.LocalMachine.OpenSubKey(RegPath))
                return key?.GetValue(name)?.ToString();
        }

        private void SetToRegistry(string name, string value)
        {
            using (var key = Registry.LocalMachine.CreateSubKey(RegPath))
                key.SetValue(name, value);
        }

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
        private string GetOrCreateUUID()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(RegPath))
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
        /// <returns>
        /// Boolean indicating whether UUID exists.
        /// </returns>
        private bool IsExistUUID()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(RegPath))
            {
                var uuid = key.GetValue(UUIDName) as string;
                return !string.IsNullOrEmpty(uuid);
            }
        }

        private bool IsSetToken()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(RegPath))
            {
                var token = key.GetValue("Token") as string;
                return !string.IsNullOrEmpty(token);
            }
        }
        private bool IsSetInterval()
        {
            using (var key = Registry.LocalMachine.CreateSubKey(RegPath))
            {
                var interval = key.GetValue("Interval") as string;
                return !string.IsNullOrEmpty(interval);
            }
        }

    }
}