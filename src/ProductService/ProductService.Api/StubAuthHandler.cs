using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProductService.Api;

public sealed class StubAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "stub-user"),
            new Claim(ClaimTypes.Name, "stub"),
            new Claim(ClaimTypes.Role, "customer"),
        ], "Stub");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Stub");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
