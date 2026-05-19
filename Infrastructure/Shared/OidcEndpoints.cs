namespace Infrastructure.Shared;

/// <summary>
/// Стандартные эндпоинты OpenID Connect / OAuth 2.0.
/// См.: <see href="https://datatracker.ietf.org/doc/html/rfc8414"/>
/// </summary>
public static class OidcEndpoints
{
    public const string Token = "/connect/token";
    public const string Discovery = "/.well-known/openid-configuration";
    public const string Jwks = "/.well-known/openid-configuration/jwks";
}
