using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Equinor.ProCoSys.Auth.Authentication;

public class MainApiForApplicationBearerTokenHandler(
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
            var token = await GetTokenForApplication(options.CurrentValue.MainApiScope, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new Exception("There was a problem fetching the MainApi bearer token for application", ex);
        }
    }

    private async Task<string> GetTokenForApplication(string scope, CancellationToken cancellationToken)
    {
        if (!_tokenCache.ContainsKey(scope))
        {
            var token = await tokenAcquisition.GetAccessTokenForAppAsync(
                scope,
                tokenAcquisitionOptions: new TokenAcquisitionOptions { CancellationToken = cancellationToken });
            _tokenCache.TryAdd(scope, token);
        }

        return _tokenCache[scope];
    }
}
