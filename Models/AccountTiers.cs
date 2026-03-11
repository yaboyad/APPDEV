using System;

namespace Label_CRM_demo.Models;

public static class AccountTiers
{
    public const string User = "User";
    public const string Master = "Master";

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return User;
        }

        return normalized.ToLowerInvariant() switch
        {
            "master" => Master,
            "admin" => Master,
            "administrator" => Master,
            _ => User
        };
    }

    public static bool IsMaster(string? value)
        => string.Equals(Normalize(value), Master, StringComparison.Ordinal);
}
