using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        public async Task<IList<string>> GetPlantIdsWithAccessForUserAsync(Guid userOid, CancellationToken cancellationToken)
        {
            var allPlants = await GetAllPlantsForUserAsync(userOid, cancellationToken);
            return allPlants?.Where(p => p.HasAccess).Select(p => p.Id).ToList();
        }

        public async Task<bool> HasUserAccessToPlantAsync(string plantId, Guid userOid, CancellationToken cancellationToken)
        {
            var plantIds = await GetPlantIdsWithAccessForUserAsync(userOid, cancellationToken);
            return plantIds.Contains(plantId);
        }

        public async Task<bool> HasCurrentUserAccessToPlantAsync(string plantId, CancellationToken cancellationToken)
        {
            var userOid = _currentUserProvider.GetCurrentUserOid();

            return await HasUserAccessToPlantAsync(plantId, userOid, cancellationToken);
        }

        public async Task<bool> IsAValidPlantForCurrentUserAsync(string plantId, CancellationToken cancellationToken)
        {
            var userOid = _currentUserProvider.GetCurrentUserOid();
            var allPlants = await GetAllPlantsForUserAsync(userOid, cancellationToken);
            return allPlants != null && allPlants.Any(p => p.Id == plantId);
        }

        public async Task<string> GetPlantTitleForCurrentUserAsync(string plantId, CancellationToken cancellationToken)
        {
            var userOid = _currentUserProvider.GetCurrentUserOid();
            var allPlants = await GetAllPlantsForUserAsync(userOid, cancellationToken);
            return allPlants?.Where(p => p.Id == plantId).SingleOrDefault()?.Title;
        }

        public async Task<IList<string>> GetPermissionsForUserAsync(string plantId, Guid userOid, CancellationToken cancellationToken)
            => await _cacheManager.GetOrCreateAsync(
                PermissionsCacheKey(plantId, userOid),
                token => _permissionApiService.GetPermissionsForCurrentUserAsync(plantId, token),
                CacheDuration.Minutes,
                _options.CurrentValue.PermissionCacheMinutes,
                cancellationToken);

        public async Task<IList<string>> GetProjectNamesForUserAsync(string plantId, Guid userOid, CancellationToken cancellationToken)
        {
            var allProjects = await GetProjectsForUserAsync(plantId, userOid, cancellationToken);
            return allProjects?.Select(p => p.Name).ToList();
        }

        public async Task<IList<AccessableProject>> GetProjectsForUserAsync(string plantId, Guid userOid, CancellationToken cancellationToken)
        {
            var allProjects = await GetAllProjectsForUserAsync(plantId, userOid, cancellationToken);
            return allProjects?.Where(p => p.HasAccess).ToList();
        }

        public async Task<bool> IsAValidProjectForUserAsync(string plantId, Guid userOid, string projectName, CancellationToken cancellationToken)
        {
            var allProjects = await GetAllProjectsForUserAsync(plantId, userOid, cancellationToken);
            return allProjects != null && allProjects.Any(p => p.Name == projectName);
        }

        public async Task<bool> IsAValidProjectForUserAsync(string plantId, Guid userOid, Guid projectGuid, CancellationToken cancellationToken)
        {
            var allProjects = await GetAllProjectsForUserAsync(plantId, userOid, cancellationToken);
            return allProjects != null && allProjects.Any(p => p.ProCoSysGuid == projectGuid);
        }

        public async Task<IList<string>> GetRestrictionRolesForUserAsync(string plantId, Guid userOid,
            CancellationToken cancellationToken)
            => await _cacheManager.GetOrCreateAsync(
                RestrictionRolesCacheKey(plantId, userOid),
                token => _permissionApiService.GetRestrictionRolesForCurrentUserAsync(plantId, token),
                CacheDuration.Minutes,
                _options.CurrentValue.PermissionCacheMinutes,
                cancellationToken);

        public async Task ClearAllAsync(string plantId, Guid userOid, CancellationToken cancellationToken)
        {
            var t1 = _cacheManager.RemoveAsync(PlantsCacheKey(userOid), cancellationToken);
            var t2 = _cacheManager.RemoveAsync(ProjectsCacheKey(plantId, userOid), cancellationToken);
            var t3 = _cacheManager.RemoveAsync(PermissionsCacheKey(plantId, userOid), cancellationToken);
            var t4 = _cacheManager.RemoveAsync(RestrictionRolesCacheKey(plantId, userOid), cancellationToken);
            var t5 = _cacheManager.RemoveAsync(UserPlantPermissionDataKey(userOid, plantId), cancellationToken);

            await Task.WhenAll([t1, t2, t3, t4, t5]);
        }

        public async Task<UserPlantPermissionData> GetUserPlantPermissionDataAsync(Guid userOid, string plantId,
            CancellationToken cancellationToken)
            => await _cacheManager.GetOrCreateAsync(
                UserPlantPermissionDataKey(userOid, plantId), async ct =>
                {
                    var plantsTask = _permissionApiService.GetAllPlantsForUserAsync(userOid, ct);
                    var permissionsTask = _permissionApiService.GetPermissionsForCurrentUserAsync(plantId, ct);
                    var projectsTask = _permissionApiService.GetAllOpenProjectsForCurrentUserAsync(plantId, ct);
                    var restrictionRolesTask = _permissionApiService.GetRestrictionRolesForCurrentUserAsync(plantId, ct);

                    await Task.WhenAll(plantsTask, permissionsTask, projectsTask, restrictionRolesTask);

                    var allPlantsForUser = await plantsTask;
                    var permissions = await permissionsTask;
                    var projects = await projectsTask;
                    var restrictionRoles = await restrictionRolesTask;

                    var userPlantPermissionData = new UserPlantPermissionData(
                        Oid: userOid,
                        Plant: plantId,
                        AllPlantsForUser: allPlantsForUser.Where(x => x.HasAccess).ToArray(),
                        Permissions: permissions.ToArray(),
                        Projects: projects.Where(x => x.HasAccess).ToArray(),
                        RestrictionRoles: restrictionRoles.ToArray()
                    );
                    return userPlantPermissionData;
                },
                CacheDuration.Minutes,
                _options.CurrentValue.PlantCacheMinutes,
                cancellationToken);

        private async Task<IList<AccessableProject>> GetAllProjectsForUserAsync(string plantId, Guid userOid, CancellationToken cancellationToken)
            => await _cacheManager.GetOrCreateAsync(
                ProjectsCacheKey(plantId, userOid),
                token => GetAllOpenProjectsAsync(plantId, token),
                CacheDuration.Minutes,
                _options.CurrentValue.PermissionCacheMinutes,
                cancellationToken);

        private async Task<IList<AccessablePlant>> GetAllPlantsForUserAsync(Guid userOid, CancellationToken cancellationToken)
            => await _cacheManager.GetOrCreateAsync(
                PlantsCacheKey(userOid),
                token => _permissionApiService.GetAllPlantsForUserAsync(userOid, token),
                CacheDuration.Minutes,
                _options.CurrentValue.PlantCacheMinutes,
                cancellationToken);

        private static string UserPlantPermissionDataKey(Guid userOid, string plantId)
            => $"USER_PLANT_PERMISSION_DATA_{userOid.ToString().ToUpper()}_{plantId}";

        private static string PlantsCacheKey(Guid userOid)
            => $"PLANTS_{userOid.ToString().ToUpper()}";

        private static string ProjectsCacheKey(string plantId, Guid userOid)
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

        private async Task<IList<AccessableProject>> GetAllOpenProjectsAsync(string plantId, CancellationToken cancellationToken)
            => await _permissionApiService.GetAllOpenProjectsForCurrentUserAsync(plantId, cancellationToken);
    }
}
