namespace FamilyBudget.Web.Security;

/// <summary>Shared helpers for where to send the user after logging in.</summary>
public static class LoginRedirects
{
    /// <summary>Only allow redirects within this site, never to another host.</summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
            ? returnUrl
            : "/";
}
