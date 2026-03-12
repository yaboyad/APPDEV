namespace Label_CRM_demo.Models;

public sealed record ManagedAccountRecord(
    string Username,
    string DisplayName,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Email,
    string AccountTier,
    bool IsBanned)
{
    public bool IsMaster => AccountTiers.IsMaster(AccountTier);

    public string TierLabel => AccountTiers.Normalize(AccountTier);

    public string AccessStatus => IsMaster ? "Protected" : IsBanned ? "Banned" : "Active";

    public string AccessNotes => IsMaster
        ? "Master access stays available on this device."
        : IsBanned
            ? "Sign-in is blocked until the account is restored."
            : "Can sign in normally.";

    public string ContactLabel => !string.IsNullOrWhiteSpace(Email)
        ? Email
        : !string.IsNullOrWhiteSpace(PhoneNumber)
            ? PhoneNumber
            : "No contact info";
}
