using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Hangfire.Dashboard;
using MMS.Play.Contracts.Extensions;

namespace MMS.Play.Infrastructure.BackgroundJobs;

/// <summary>
/// Protects the Hangfire dashboard with HTTP Basic authentication. Without a filter Hangfire allows local requests only.
/// </summary>
public class BasicAuthDashboardAuthorizationFilter(string? username, string? password) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (IsAuthorized(httpContext.Request.Headers.Authorization.ToString()))
            return true;

        // Hangfire returns 401 itself; the header makes the browser show the login prompt
        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\", charset=\"UTF-8\"";
        return false;
    }

    private bool IsAuthorized(string authorizationHeader)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return false;

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var header)
            || !header.Scheme.iEquals("Basic")
            || string.IsNullOrEmpty(header.Parameter))
            return false;

        string credentials;
        try
        {
            credentials = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
        }
        catch (FormatException)
        {
            return false;
        }

        var separatorIndex = credentials.IndexOf(':');
        if (separatorIndex < 0)
            return false;

        return FixedTimeEquals(credentials[..separatorIndex], username)
            & FixedTimeEquals(credentials[(separatorIndex + 1)..], password);
    }

    private static bool FixedTimeEquals(string actual, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));
}
