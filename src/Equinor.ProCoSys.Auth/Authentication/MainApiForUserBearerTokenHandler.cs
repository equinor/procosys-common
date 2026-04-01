using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Equinor.ProCoSys.Auth.Authentication;

public class MainApiForUserBearerTokenHandler(
    IOptionsMonitor<MainApiAuthenticatorOptions> options,
    ITokenAcquisition tokenAcquisition)
    : DelegatingHandler
{
    /// <summary>
    /// cache created tokens during this request
    /// </summary>
    private readonly ConcurrentDictionary<string, string> _tokenCache = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetTokenForUser(options.CurrentValue.MainApiScope, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new Exception("There was a problem fetching the MainApi bearer token for user", ex);
        }
    }

    private async Task<string> GetTokenForUser(string scope, CancellationToken cancellationToken)
    {
        // if (!_tokenCache.ContainsKey(scope))
        // {
        return await tokenAcquisition.GetAccessTokenForUserAsync(
            new[] { scope },
            tokenAcquisitionOptions: new TokenAcquisitionOptions { CancellationToken = cancellationToken });
        //     _tokenCache.TryAdd(scope, token);
        // }
        //
        // return _tokenCache[scope];
    }
}
