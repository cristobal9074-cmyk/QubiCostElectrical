using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Auth;

public static class CollaborationSessionBridge
{
    public static CollaborationUserContext CurrentUserContext()
    {
        var session = SessionManager.Current;
        if (session?.Account == null)
            return CollaborationUserContext.LocalFallback();

        return CollaborationUserContext.FromEnvironment(
            session.Account.Id.ToString("N"),
            session.DisplayName,
            session.Account.Email,
            SessionManager.RoleDisplayName);
    }
}
