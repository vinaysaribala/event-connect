using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace EventConnectService.Implementations;

/// <summary>
/// Packs a user id and an Identity token into one opaque, URL-safe value, so email links carry a single "token".
/// </summary>
internal static class LinkToken
{
    public static string Create(int userId, string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes($"{userId}:{token}"));

    public static bool TryRead(string value, out int userId, out string token)
    {
        userId = 0;
        token = "";
        try
        {
            var text = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value));
            var separator = text.IndexOf(':');
            if (separator <= 0 || !int.TryParse(text[..separator], out userId))
            {
                return false;
            }

            token = text[(separator + 1)..];
            return token.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
