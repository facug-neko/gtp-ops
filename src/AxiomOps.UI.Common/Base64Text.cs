using System.Text;

namespace AxiomOps.UI.Services;

/// <summary>
/// Helpers for the Base64-encoded file content the Manage/Content endpoints
/// return and accept. Decoding is strict (rejects plain text that merely looks
/// Base64-ish) and preserves the presence of a UTF-8 BOM for exact round-trips.
/// </summary>
public static class Base64Text
{
    private const char Bom = '﻿';

    /// <summary>
    /// Decodes <paramref name="value"/> as Base64 UTF-8 text. Succeeds only when
    /// it is well-formed Base64 that decodes to valid UTF-8. A leading BOM is
    /// stripped and reported via <paramref name="hadBom"/>.
    /// </summary>
    public static bool TryDecode(string value, out string decoded, out bool hadBom)
    {
        decoded = string.Empty;
        hadBom = false;

        var trimmed = value.Trim();
        if (trimmed.Length < 4 || trimmed.Length % 4 != 0)
        {
            return false;
        }

        Span<byte> buffer = trimmed.Length <= 4096 ? stackalloc byte[trimmed.Length] : new byte[trimmed.Length];
        if (!Convert.TryFromBase64String(trimmed, buffer, out var written))
        {
            return false;
        }

        try
        {
            var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var text = strict.GetString(buffer[..written]);
            hadBom = text.StartsWith(Bom);
            decoded = hadBom ? text[1..] : text;
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Encodes UTF-8 text to Base64, optionally restoring a leading BOM.</summary>
    public static string Encode(string text, bool withBom)
    {
        var content = withBom ? Bom + text : text;
        return Convert.ToBase64String(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));
    }

    /// <summary>UTF-8 bytes for a new testdata file, matching the existing files' BOM convention.</summary>
    public static byte[] ToUtf8Bytes(string text, bool withBom)
    {
        var body = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
        if (!withBom)
        {
            return body;
        }

        return [.. Encoding.UTF8.GetPreamble(), .. body];
    }
}
