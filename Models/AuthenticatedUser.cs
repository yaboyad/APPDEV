namespace Label_CRM_demo.Models;

public sealed record AuthenticatedUser(
    string Username,
    string DisplayName,
    string FirstName = "",
    string LastName = "",
    string PhoneNumber = "",
    string Email = "",
    string AccountTier = AccountTiers.User)
{
    public bool IsMaster => AccountTiers.IsMaster(AccountTier);

    public string TierLabel => AccountTiers.Normalize(AccountTier);
}
