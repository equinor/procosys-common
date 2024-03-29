﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Permission;
using Equinor.ProCoSys.Common.Caches;
using Equinor.ProCoSys.Common.Misc;
using Microsoft.Extensions.Options;

namespace Equinor.ProCoSys.Auth.Caches
{
    /// <summary>
    /// Cache permissions for an user in a plant
    /// Caches:
    ///  * list of plants where user has access
    ///  * list of projects where user has access
    ///  * list of permissions (TAG/WRITE, TAG/READ, etc) for user
    ///  * list of restriction roles for user
    /// The cache expiration time is controlled by CacheOptions. Default expiration time is 20 minutes
    /// </summary>
    public class PermissionCache : IPermissionCache
    {
        private readonly ICacheManager _cacheManager;
        private readonly ICurrentUserProvider _currentUserProvider;
        private readonly IPermissionApiService _permissionApiService;
        private readonly IOptionsMonitor<CacheOptions> _options;

        public PermissionCache(
            ICacheManager cacheManager,
            ICurrentUserProvider currentUserProvider,
            IPermissionApiService permissionApiService,
            IOptionsMonitor<CacheOptions> options)
        {
            _cacheManager = cacheManager;
            _currentUserProvider = currentUserProvider;
            _permissionApiService = permissionApiService;
            _options = options;
        }

        public async Task<IList<string>> GetPlantIdsWithAccessForUserAsync(Guid userOid)
        {
            var allPlants = await GetAllPlantsForUserAsync(userOid);
            return allPlants?.Where(p => p.HasAccess).Select(p => p.Id).ToList();
        }

        public async Task<bool> HasUserAccessToPlantAsync(string plantId, Guid userOid)
        {
            var plantIds = await GetPlantIdsWithAccessForUserAsync(userOid);
            return plantIds.Contains(plantId);
        }

        public async Task<bool> HasCurrentUserAccessToPlantAsync(string plantId)
        {
            var userOid = _currentUserProvider.GetCurrentUserOid();

            return await HasUserAccessToPlantAsync(plantId, userOid);
        }

        public async Task<bool> IsAValidPlantForCurrentUserAsync(string plantId)
        {
            var userOid = _currentUserProvider.GetCurrentUserOid();
            var allPlants = await GetAllPlantsForUserAsync(userOid);
            return allPlants != null && allPlants.Any(p => p.Id == plantId);
        }

        public async Task<string> GetPlantTitleForCurrentUserAsync(string plantId)
        {
            var userOid = _currentUserProvider.GetCurrentUserOid();
            var allPlants = await GetAllPlantsForUserAsync(userOid);
            return allPlants?.Where(p => p.Id == plantId).SingleOrDefault()?.Title;
        }

        public async Task<IList<string>> GetPermissionsForUserAsync(string plantId, Guid userOid)
            => await _cacheManager.GetOrCreate(
                PermissionsCacheKey(plantId, userOid),
                async () => await _permissionApiService.GetPermissionsForCurrentUserAsync(plantId),
                CacheDuration.Minutes,
                _options.CurrentValue.PermissionCacheMinutes);

        public async Task<IList<string>> GetProjectNamesForUserAsync(string plantId, Guid userOid)
        {
            var allProjects = await GetProjectsForUserAsync(plantId, userOid);
            return allProjects?.Select(p => p.Name).ToList();
        }

        public async Task<IList<AccessableProject>> GetProjectsForUserAsync(string plantId, Guid userOid)
        {
            var allProjects = await GetAllProjectsForUserAsync(plantId, userOid);
            return allProjects?.Where(p => p.HasAccess).ToList();
        }

        public async Task<bool> IsAValidProjectForUserAsync(string plantId, Guid userOid, string projectName)
        {
            var allProjects = await GetAllProjectsForUserAsync(plantId, userOid);
            return allProjects != null && allProjects.Any(p => p.Name == projectName);
        }

        public async Task<bool> IsAValidProjectForUserAsync(string plantId, Guid userOid, Guid projectGuid)
        {
            var allProjects = await GetAllProjectsForUserAsync(plantId, userOid);
            return allProjects != null && allProjects.Any(p => p.ProCoSysGuid == projectGuid);
        }

        public async Task<IList<string>> GetRestrictionRolesForUserAsync(string plantId, Guid userOid)
            => await _cacheManager.GetOrCreate(
                RestrictionRolesCacheKey(plantId, userOid),
                async () => await _permissionApiService.GetRestrictionRolesForCurrentUserAsync(plantId),
                CacheDuration.Minutes,
                _options.CurrentValue.PermissionCacheMinutes);

        public void ClearAll(string plantId, Guid userOid)
        {
            _cacheManager.Remove(PlantsCacheKey(userOid));
            _cacheManager.Remove(ProjectsCacheKey(plantId, userOid));
            _cacheManager.Remove(PermissionsCacheKey(plantId, userOid));
            _cacheManager.Remove(RestrictionRolesCacheKey(plantId, userOid));
        }

        private async Task<IList<AccessableProject>> GetAllProjectsForUserAsync(string plantId, Guid userOid)
            => await _cacheManager.GetOrCreate(
                ProjectsCacheKey(plantId, userOid),
                async () => await GetAllOpenProjectsAsync(plantId),
                CacheDuration.Minutes,
                _options.CurrentValue.PermissionCacheMinutes);

        private async Task<IList<AccessablePlant>> GetAllPlantsForUserAsync(Guid userOid)
            => await _cacheManager.GetOrCreate(
                PlantsCacheKey(userOid),
                async () =>
                {
                    var plants = await _permissionApiService.GetAllPlantsForUserAsync(userOid);
                    return plants;
                },
                CacheDuration.Minutes,
                _options.CurrentValue.PlantCacheMinutes);

        private string PlantsCacheKey(Guid userOid)
            => $"PLANTS_{userOid.ToString().ToUpper()}";

        private string ProjectsCacheKey(string plantId, Guid userOid)
        {
            if (userOid == Guid.Empty)
            {
                throw new Exception("Illegal userOid for cache");
            }
            return $"PROJECTS_{userOid.ToString().ToUpper()}_{plantId}";
        }

        private static string PermissionsCacheKey(string plantId, Guid userOid)
        {
            if (userOid == Guid.Empty)
            {
                throw new Exception("Illegal userOid for cache");
            }
            return $"PERMISSIONS_{userOid.ToString().ToUpper()}_{plantId}";
        }

        private static string RestrictionRolesCacheKey(string plantId, Guid userOid)
        {
            if (userOid == Guid.Empty)
            {
                throw new Exception("Illegal userOid for cache");
            }
            return $"CONTENTRESTRICTIONS_{userOid.ToString().ToUpper()}_{plantId}";
        }

        private async Task<IList<AccessableProject>> GetAllOpenProjectsAsync(string plantId)
            => await _permissionApiService.GetAllOpenProjectsForCurrentUserAsync(plantId);
    }
}
