using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Harmonify.MusicServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Harmonify.MusicServer.Auth;

public class BasicAuthHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder,
  IOptions<MusicServerOptions> config)
  : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  public const string SchemeName = "BasicAuthentication";

  private readonly MusicServerOptions _config = config.Value;

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.ContainsKey("Authorization"))
      return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));

    try
    {
      var authHeader = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);

      if (authHeader.Scheme != "Basic" || authHeader.Parameter is null)
        return Task.FromResult(AuthenticateResult.Fail("Invalid authorization scheme"));

      var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
      var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

      if (credentials.Length != 2)
        return Task.FromResult(AuthenticateResult.Fail("Invalid credentials format"));

      var username = credentials[0];
      var password = credentials[1];

      if (username != _config.Username || password != _config.Password)
        return Task.FromResult(AuthenticateResult.Fail("Invalid username or password"));

      var claims = new[] { new Claim(ClaimTypes.Name, username) };
      var identity = new ClaimsIdentity(claims, Scheme.Name);
      var principal = new ClaimsPrincipal(identity);
      var ticket = new AuthenticationTicket(principal, Scheme.Name);

      return Task.FromResult(AuthenticateResult.Success(ticket));
    }
    catch
    {
      return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header"));
    }
  }
}
