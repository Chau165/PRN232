using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.Security;

/// <summary>Validates Google ID tokens with Google.Apis.Auth, audience = Google:ClientId.</summary>
public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly string? _clientId;
    private readonly ILogger<GoogleIdTokenValidator> _logger;

    public GoogleIdTokenValidator(string? clientId, ILogger<GoogleIdTokenValidator> logger)
    {
        _clientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();
        _logger = logger;
    }

    public bool IsConfigured => _clientId is not null;

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (_clientId is null)
        {
            throw new InvalidOperationException("Google:ClientId is not configured.");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [_clientId] });
            if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException exception)
        {
            // Never log the token itself.
            _logger.LogWarning("Google ID token rejected: {Reason}", exception.Message);
            return null;
        }
    }
}
