namespace QubiCostElectrical;

public sealed record CollaborationUserContext(
    string UserId,
    string DisplayName,
    string Email,
    string RoleDisplayName,
    string MachineName)
{
    public static CollaborationUserContext FromEnvironment(
        string userId,
        string displayName,
        string email,
        string roleDisplayName)
        => new(
            string.IsNullOrWhiteSpace(userId) ? Guid.NewGuid().ToString("N") : userId.Trim(),
            string.IsNullOrWhiteSpace(displayName) ? Environment.UserName : displayName.Trim(),
            string.IsNullOrWhiteSpace(email) ? "" : email.Trim(),
            string.IsNullOrWhiteSpace(roleDisplayName) ? "Usuario" : roleDisplayName.Trim(),
            Environment.MachineName);

    public static CollaborationUserContext LocalFallback()
        => FromEnvironment("", Environment.UserName, "", "Usuario");
}
