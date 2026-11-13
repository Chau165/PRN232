namespace PRN232.Application.Interfaces;

/// <summary>Claims taken from a Google ID token after its signature and audience are verified.</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>
/// Verifies Google ID tokens for "Sign in with Google". Kept behind an interface so the sign-in
/// flow can be tested without calling Google.
/// </summary>
public interface IGoogleIdTokenValidator
{
    /// <summary>False when Google:ClientId is not configured; the endpoint then returns 503.</summary>
    bool IsConfigured { get; }

    /// <summary>Returns the verified identity, or null when the token is invalid or expired.</summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
