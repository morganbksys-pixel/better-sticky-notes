using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BetterStickyNotes;

public sealed record EncryptedNote(int Version, string Salt, string Nonce, string Ciphertext, string Tag);

public static class NoteProtection
{
    public static byte[] DeriveKey(string password, string salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), 600_000, HashAlgorithmName.SHA256, 32);

    public static EncryptedNote Encrypt(Note note, byte[] key, string salt)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(new[] { note.Title, note.Text });
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[plain.Length]; var tag = new byte[16];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, cipher, tag, note.Id.ToByteArray());
            return new(1, salt, Convert.ToBase64String(nonce), Convert.ToBase64String(cipher), Convert.ToBase64String(tag));
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public static string[] Decrypt(Note note, byte[] key)
    {
        var data = note.Protection ?? throw new InvalidOperationException();
        if (data.Version != 1) throw new CryptographicException("Version de protection inconnue.");
        var cipher = Convert.FromBase64String(data.Ciphertext); var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(Convert.FromBase64String(data.Nonce), cipher, Convert.FromBase64String(data.Tag), plain, note.Id.ToByteArray());
            var result = JsonSerializer.Deserialize<string[]>(plain);
            if (result is not { Length: 2 }) throw new CryptographicException("Note endommagée.");
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public static void Protect(Note note, byte[] key, string salt)
    {
        note.Protection = Encrypt(note, key, salt);
        note.ProtectionKey = key.ToArray();
    }
    public static void Unlock(Note note, byte[] key)
    {
        var content = Decrypt(note, key);
        note.Title = content[0]; note.Text = content[1]; note.ProtectionKey = key.ToArray();
    }
    public static void Lock(Note note)
    {
        if (!note.IsProtected || note.IsLocked) return;
        note.Protection = Encrypt(note, note.ProtectionKey!, note.Protection!.Salt);
        CryptographicOperations.ZeroMemory(note.ProtectionKey!); note.ProtectionKey = null;
        note.Title = ""; note.Text = "";
    }
    public static void Remove(Note note)
    {
        if (note.IsLocked) throw new InvalidOperationException("Déverrouillez la note d’abord.");
        if (note.ProtectionKey != null) CryptographicOperations.ZeroMemory(note.ProtectionKey);
        note.ProtectionKey = null; note.Protection = null;
    }
}
