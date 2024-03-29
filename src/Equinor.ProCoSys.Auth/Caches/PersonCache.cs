﻿using System;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Person;
using Equinor.ProCoSys.Common.Caches;
using Microsoft.Extensions.Options;

namespace Equinor.ProCoSys.Auth.Caches
{
    /// <summary>
    /// Cache person information
    /// The cache expiration time is controlled by CacheOptions. Default expiration time is 1440 minutes (24h)
    /// </summary>
    public class PersonCache : IPersonCache
    {
        private readonly ICacheManager _cacheManager;
        private readonly IPersonApiService _personApiService;
        private readonly IOptionsMonitor<CacheOptions> _options;

        public PersonCache(
            ICacheManager cacheManager, 
            IPersonApiService personApiService,
            IOptionsMonitor<CacheOptions> options)
        {
            _cacheManager = cacheManager;
            _personApiService = personApiService;
            _options = options;
        }

        public async Task<ProCoSysPerson> GetAsync(Guid userOid)
            => await _cacheManager.GetOrCreate(
                PersonsCacheKey(userOid),
                async () =>
                {
                    var person = await _personApiService.TryGetPersonByOidAsync(userOid);
                    return person;
                },
                CacheDuration.Minutes,
                _options.CurrentValue.PersonCacheMinutes);

        public async Task<bool> ExistsAsync(Guid userOid)
        {
            var pcsPerson = await GetAsync(userOid);
            return pcsPerson != null;
        }

        private string PersonsCacheKey(Guid userOid)
            => $"PERSONS_{userOid.ToString().ToUpper()}";
    }
}
