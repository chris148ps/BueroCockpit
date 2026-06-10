using System.Diagnostics;
using System.Security.Cryptography;

namespace BueroCockpit.Services;

public sealed class FileHashService
{
    public string? ComputeSha256(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hash could not be calculated for '{path}': {ex}");
            return null;
        }
    }
}
