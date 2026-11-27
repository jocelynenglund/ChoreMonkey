using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ChoreMonkey.Core.Feature.DisplayLinks;

/// <summary>
/// Where display-link secrets live: a folder of their own beside the event store, never in an event. Each secret is
/// kept SEALED (AES-GCM, a key derived with HKDF from Display:SealKey, or from {store}/seal.key made once when that isn't set) so an admin can copy the link
/// again any time, and by its SHA-256 hash so the public address can be looked up without unsealing anything.
///
///   {store}/by-hash/{sha256 hex}                      -> "{householdId}:{linkId}"
///   {store}/links/{householdId}/{linkId}.json         -> { hash, sealed }
///
/// Settings: Display:SealKey (optional; without it a key is made once in {store}/seal.key), Display:StorePath (default EVENTSTORE_PATH/display-links).
/// </summary>
public sealed class DisplayLinkVault(IConfiguration config)
{
    const string KeyPurpose = "choremonkey display links v1";

    /// <summary>A key to seal with: Display:SealKey when it is set, otherwise one made once and kept in the vault's own folder
    /// (owner, 2026-11-27: nobody should have to set a secret by hand for this). False only when that folder can't be written.</summary>
    public bool IsConfigured => KeyMaterial() is not null;

    string? KeyMaterial()
    {
        if (config["Display:SealKey"] is { Length: > 0 } configured) return configured;
        var file = Path.Combine(Root, "seal.key");
        try
        {
            if (File.Exists(file)) return File.ReadAllText(file).Trim();
            Directory.CreateDirectory(Root);
            var made = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            try
            {
                using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream)) writer.Write(made);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                return made;
            }
            catch (IOException) when (File.Exists(file))
            {
                return File.ReadAllText(file).Trim(); // another request made it first
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    string Root => config["Display:StorePath"] is { Length: > 0 } p
        ? p
        : Path.Combine(Environment.GetEnvironmentVariable("EVENTSTORE_PATH") ?? Path.Combine(Directory.GetCurrentDirectory(), "data"), "display-links");

    /// <summary>A fresh secret: 32 random bytes, base64url (43 characters).</summary>
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>Whether a string could be a token at all, so nothing else ever becomes a file name.</summary>
    public static bool LooksLikeToken(string? token) =>
        token is { Length: >= 43 and <= 128 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public void Save(Guid householdId, Guid linkId, string token)
    {
        var hash = Hash(token);
        var sealedToken = Seal(token, linkId);

        var linkFile = LinkFile(householdId, linkId);
        Directory.CreateDirectory(Path.GetDirectoryName(linkFile)!);
        WriteAtomic(linkFile, JsonSerializer.Serialize(new Stored(hash, sealedToken)));

        var hashFile = HashFile(hash);
        Directory.CreateDirectory(Path.GetDirectoryName(hashFile)!);
        WriteAtomic(hashFile, $"{householdId}:{linkId}");
    }

    /// <summary>The household and link a token belongs to, or null when the token is unknown or revoked.</summary>
    public (Guid HouseholdId, Guid LinkId)? Resolve(string token)
    {
        if (!LooksLikeToken(token)) return null;
        var file = HashFile(Hash(token));
        if (!File.Exists(file)) return null;

        var parts = File.ReadAllText(file).Split(':');
        return parts.Length == 2 && Guid.TryParse(parts[0], out var h) && Guid.TryParse(parts[1], out var l) ? (h, l) : null;
    }

    /// <summary>The secret again, unsealed; null when it was never kept, was revoked, or the key has changed.</summary>
    public string? Reveal(Guid householdId, Guid linkId)
    {
        if (!IsConfigured) return null;
        var file = LinkFile(householdId, linkId);
        if (!File.Exists(file)) return null;
        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(file));
            return stored == null ? null : Unseal(stored.Sealed, linkId);
        }
        catch (Exception e) when (e is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    public void Remove(Guid householdId, Guid linkId)
    {
        var file = LinkFile(householdId, linkId);
        if (!File.Exists(file)) return;
        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(file));
            if (stored != null && LooksLikeHash(stored.Hash)) File.Delete(HashFile(stored.Hash));
        }
        catch (JsonException) { }
        File.Delete(file);
    }

    static bool LooksLikeHash(string? hash) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit);

    string LinkFile(Guid householdId, Guid linkId) => Path.Combine(Root, "links", householdId.ToString("D"), linkId.ToString("D") + ".json");
    string HashFile(string hash) => Path.Combine(Root, "by-hash", hash);

    byte[] Key() => HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(KeyMaterial()!), 32, salt: null, info: Encoding.UTF8.GetBytes(KeyPurpose));

    // nonce (12) | tag (16) | ciphertext, base64. The link id is bound in as associated data.
    string Seal(string token, Guid linkId)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(token);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(Key(), 16);
        aes.Encrypt(nonce, plain, cipher, tag, linkId.ToByteArray());
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    string Unseal(string sealedToken, Guid linkId)
    {
        var all = Convert.FromBase64String(sealedToken);
        var nonce = all[..12];
        var tag = all[12..28];
        var cipher = all[28..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(Key(), 16);
        aes.Decrypt(nonce, cipher, tag, plain, linkId.ToByteArray());
        return Encoding.UTF8.GetString(plain);
    }

    static void WriteAtomic(string path, string text)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }

    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    sealed record Stored(string Hash, string Sealed);
}
