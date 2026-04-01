using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Client;
using Microsoft.Extensions.Options;

namespace Equinor.ProCoSys.Auth.Person
{
    /// <summary>
    /// Service to get Person info from Main, using Main Api
    /// </summary>
    public class MainApiPersonService(
        IMainApiClientForApplication mainApiClientForApplication,
        IOptionsMonitor<MainApiOptions> options)
        : IPersonApiService
    {
        private readonly Uri _baseAddress = new(options.CurrentValue.BaseAddress);
        private readonly string _apiVersion = options.CurrentValue.ApiVersion;

        public async Task<ProCoSysPerson> TryGetPersonByOidAsync(
            Guid azureOid,
            bool includeVoidedPerson,
            CancellationToken cancellationToken)
        {
            var url = $"{_baseAddress}Person" +
                      $"?azureOid={azureOid:D}" +
                      $"&includeVoidedPerson={includeVoidedPerson.ToString().ToLower()}" +
                      $"&api-version={_apiVersion}";

            // Execute as application. The Person endpoint in Main Api requires
            // a special role "User.Read.All", which the Azure application registration has
            return await mainApiClientForApplication.TryQueryAndDeserializeAsync<ProCoSysPerson>(url, cancellationToken);
        }

        public async Task<List<ProCoSysPerson>> GetAllPersonsAsync(string plant, CancellationToken cancellationToken)
        {
            var url = $"{_baseAddress}Person/AllPersons" +
                      $"?plantId={plant}" +
                      $"&api-version={_apiVersion}";
            return await mainApiClientForApplication.TryQueryAndDeserializeAsync<List<ProCoSysPerson>>(url, cancellationToken);
        }
    }
}
