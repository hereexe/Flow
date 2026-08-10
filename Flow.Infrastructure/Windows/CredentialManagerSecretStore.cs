using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flow.Application.Abstractions;

namespace Flow.Infrastructure.Windows;

public class CredentialManagerSecretStore : ISecretStore
{
    private const string TargetPrefix = "FlowApp:ProviderKey:";
    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    private readonly string _fallbackFilePath;
    private readonly object _lock = new();

    public CredentialManagerSecretStore(string? customFallbackPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFallbackPath))
        {
            _fallbackFilePath = customFallbackPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _fallbackFilePath = Path.Combine(appData, "Flow", "secrets.dat");
        }
    }

    public void SaveSecret(string providerId, string secret)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be empty", nameof(providerId));

        var targetName = GetTargetName(providerId);

        try
        {
            if (TrySaveWin32Credential(targetName, secret))
            {
                // If saved successfully to Win32 Credential Manager, remove any leftover DPAPI secret
                DeleteFallbackSecret(providerId);
                return;
            }
        }
        catch
        {
            // Win32 call exception, fallback to DPAPI
        }

        SaveFallbackSecret(providerId, secret);
    }

    public string? GetSecret(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return null;

        var targetName = GetTargetName(providerId);

        try
        {
            if (TryReadWin32Credential(targetName, out var win32Secret))
            {
                return win32Secret;
            }
        }
        catch
        {
            // Win32 call exception, try fallback
        }

        return GetFallbackSecret(providerId);
    }

    public bool HasSecret(string providerId)
    {
        return !string.IsNullOrEmpty(GetSecret(providerId));
    }

    public void DeleteSecret(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return;

        var targetName = GetTargetName(providerId);

        try
        {
            CredDelete(targetName, CRED_TYPE_GENERIC, 0);
        }
        catch
        {
            // Ignore Win32 deletion errors
        }

        DeleteFallbackSecret(providerId);
    }

    private static string GetTargetName(string providerId) => $"{TargetPrefix}{providerId}";

    #region Win32 Credential Manager P/Invoke

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string targetName, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string targetName, uint type, uint reservedFlag);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree([In] IntPtr credentialPtr);

    private static bool TrySaveWin32Credential(string targetName, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var blobPtr = Marshal.AllocHGlobal(secretBytes.Length);

        try
        {
            Marshal.Copy(secretBytes, 0, blobPtr, secretBytes.Length);

            var credential = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = targetName,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blobPtr,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = Environment.UserName
            };

            return CredWrite(ref credential, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    private static bool TryReadWin32Credential(string targetName, out string? secret)
    {
        secret = null;
        if (!CredRead(targetName, CRED_TYPE_GENERIC, 0, out var credPtr) || credPtr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (credential.CredentialBlobSize > 0 && credential.CredentialBlob != IntPtr.Zero)
            {
                var secretBytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, secretBytes, 0, (int)credential.CredentialBlobSize);
                secret = Encoding.UTF8.GetString(secretBytes);
                return true;
            }
        }
        finally
        {
            CredFree(credPtr);
        }

        return false;
    }

    #endregion

    #region DPAPI Fallback Storage

    private void SaveFallbackSecret(string providerId, string secret)
    {
        lock (_lock)
        {
            var secrets = LoadFallbackDictionary();
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(secret),
                null,
                DataProtectionScope.CurrentUser);

            secrets[providerId] = Convert.ToBase64String(encrypted);
            SaveFallbackDictionary(secrets);
        }
    }

    private string? GetFallbackSecret(string providerId)
    {
        lock (_lock)
        {
            var secrets = LoadFallbackDictionary();
            if (!secrets.TryGetValue(providerId, out var base64))
            {
                return null;
            }

            try
            {
                var encrypted = Convert.FromBase64String(base64);
                var decrypted = ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }
    }

    private void DeleteFallbackSecret(string providerId)
    {
        lock (_lock)
        {
            var secrets = LoadFallbackDictionary();
            if (secrets.Remove(providerId))
            {
                SaveFallbackDictionary(secrets);
            }
        }
    }

    private Dictionary<string, string> LoadFallbackDictionary()
    {
        if (!File.Exists(_fallbackFilePath))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(_fallbackFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveFallbackDictionary(Dictionary<string, string> secrets)
    {
        var directory = Path.GetDirectoryName(_fallbackFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (secrets.Count == 0)
        {
            if (File.Exists(_fallbackFilePath))
            {
                File.Delete(_fallbackFilePath);
            }
            return;
        }

        var json = JsonSerializer.Serialize(secrets);
        File.WriteAllText(_fallbackFilePath, json);
    }

    #endregion
}
