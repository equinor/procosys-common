using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Authentication;
using Equinor.ProCoSys.Auth.Caches;
using Equinor.ProCoSys.Auth.Permission;
using Equinor.ProCoSys.Auth.Person;
using Equinor.ProCoSys.Common.Misc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Equinor.ProCoSys.Auth.Authorization
{
    /// <summary>
    /// Implement IClaimsTransformation to extend the ClaimsPrincipal with claims to be used during authorization.
    /// Claims added only for authenticated users. User must exist in ProCoSys
    ///  * If ProCoSys user is a superuser, a claim of type ClaimTypes.Role with value SUPERUSER is added.
    ///    The SUPERUSER claim is added regardless if request is a plant request or not
    /// For requests handling a valid plant for user, these types of claims are added:
    ///  * ClaimTypes.Role claim for each user permission (such as TAG/READ)
    ///  * ClaimTypes.UserData claim for each project user has access to. These claim name start with ProjectPrefix
    ///  * ClaimTypes.UserData claim for each restriction role for user. These claim name start with RestrictionRolePrefix
    ///    (Restriction role = "%" means "User has no restriction roles")
    /// </summary>
    public class ClaimsTransformation : IClaimsTransformation
    {
        public const string Superuser = "SUPERUSER";
        public const string PersonExist = "Person-Exists-";
        public const string ClaimsIssuer = "ProCoSys";
        public const string ProjectPrefix = "PCS_Project##";
        public const string RestrictionRolePrefix = "PCS_RestrictionRole##";
        public const string NoRestrictions = "%";

        private readonly ILocalPersonRepository _localPersonRepository;
        private readonly IPersonCache _personCache;
        private readonly IPlantProvider _plantProvider;
        private readonly IPermissionCache _permissionCache;
        private readonly ILogger<ClaimsTransformation> _logger;
        private readonly IOptionsMonitor<MainApiAuthenticatorOptions> _authenticatorOptions;

        public ClaimsTransformation(
            ILocalPersonRepository localPersonRepository,
            IPersonCache personCache,
            IPlantProvider plantProvider,
            IPermissionCache permissionCache,
            ILogger<ClaimsTransformation> logger,
            IOptionsMonitor<MainApiAuthenticatorOptions> authenticatorOptions)
        {
            _localPersonRepository = localPersonRepository;
            _personCache = personCache;
            _plantProvider = plantProvider;
            _permissionCache = permissionCache;
            _logger = logger;
            _authenticatorOptions = authenticatorOptions;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var sw = new Stopwatch();
            sw.Start();
            _logger.LogInformation("----- {Name} start", GetType().Name);

            // Can't use CurrentUserProvider here. Middleware setting current user not called yet. 
            var userOid = principal.Claims.TryGetOid();
            if (!userOid.HasValue)
            {
                _logger.LogInformation("----- {Name} early exit, not authenticated yet, Elapsed ({Elapsed}ms)", GetType().Name, sw.ElapsedMilliseconds);
                return principal;
            }
            var claimsIdentity = GetOrCreateClaimsIdentityForThisIssuer(principal);

            var proCoSysPerson = await GetProCoSysPersonAsync(userOid.Value, claimsIdentity);
            if (proCoSysPerson is null)
            {
                _logger.LogInformation("----- {Name} early exit, {UserOid} don\'t exists in ProCoSys, Elapsed ({Elapsed}ms)", GetType().Name, userOid, sw.ElapsedMilliseconds);
                return principal;
            }

            if (proCoSysPerson.Super)
            {
                AddSuperRoleToIdentity(claimsIdentity);
                _logger.LogInformation("----- {Name}: {UserOid} logged in as a ProCoSys superuser, Elapsed ({Elapsed}ms)", GetType().Name, userOid, sw.ElapsedMilliseconds);
            }

            var plantId = _plantProvider.Plant;
            if (string.IsNullOrEmpty(plantId))
            {
                _logger.LogInformation("----- {Name} early exit, not a plant request, Elapsed ({Elapsed}ms)", GetType().Name, sw.ElapsedMilliseconds);
                return principal;
            }

            var userPlantPermissionData = await _permissionCache.GetUserPlantPermissionDataAsync(userOid.Value, plantId, CancellationToken.None);

            if (!userPlantPermissionData.HasAccessToPlant(plantId))
            {
                _logger.LogInformation("----- {Name} early exit, not a valid plant for user, Elapsed ({Elapsed}ms)", GetType().Name, sw.ElapsedMilliseconds);
                return principal;
            }

            AddRoleForAllPermissionsToIdentity(claimsIdentity, userPlantPermissionData.Permissions);
            if (!_authenticatorOptions.CurrentValue.DisableProjectUserDataClaims)
            {
                AddUserDataClaimForAllOpenProjectsToIdentity(claimsIdentity, userPlantPermissionData.Projects);
            }
            if (!_authenticatorOptions.CurrentValue.DisableRestrictionRoleUserDataClaims)
            {
                AddUserDataClaimForAllRestrictionRolesToIdentity(claimsIdentity, userPlantPermissionData.RestrictionRoles);
            }

            _logger.LogInformation("----- {Name} completed, Elapsed ({Elapsed}ms)", GetType().Name, sw.ElapsedMilliseconds);
            return principal;
        }

        private static void AddPersonExistsClaim(ClaimsIdentity claimsIdentity, string azureOid)
        {
            claimsIdentity.AddClaim(new Claim(ClaimTypes.UserData, $"{PersonExist}{azureOid}"));
        }

        public static string GetProjectClaimValue(string projectName) => $"{ProjectPrefix}{projectName}";
        public static string GetProjectClaimValue(Guid projectGuid) => $"{ProjectPrefix}{projectGuid}";

        public static string GetRestrictionRoleClaimValue(string restrictionRole) => $"{RestrictionRolePrefix}{restrictionRole}";

        private async Task<ProCoSysPerson> GetProCoSysPersonAsync(Guid userOid, ClaimsIdentity claimsIdentity)
        {
            // check if user exists in local repository before checking
            // cache which get user from ProCoSys
            var proCoSysPerson = await _localPersonRepository.GetAsync(userOid, CancellationToken.None);
            if (proCoSysPerson is not null)
            {
                AddPersonExistsClaim(claimsIdentity, proCoSysPerson.AzureOid);
                return proCoSysPerson;
            }

            return await _personCache.GetAsync(userOid, CancellationToken.None);
        }

        private ClaimsIdentity GetOrCreateClaimsIdentityForThisIssuer(ClaimsPrincipal principal)
        {
            var identity = principal.Identities.SingleOrDefault(i => i.Label == ClaimsIssuer);
            if (identity == null)
            {
                identity = new ClaimsIdentity { Label = ClaimsIssuer };
                principal.AddIdentity(identity);
            }
            else
            {
                ClearOldClaims(identity);
            }

            return identity;
        }

        private void ClearOldClaims(ClaimsIdentity identity)
        {
            var oldClaims = identity.Claims.Where(c => c.Issuer == ClaimsIssuer).ToList();
            oldClaims.ForEach(identity.RemoveClaim);
        }

        private void AddSuperRoleToIdentity(ClaimsIdentity claimsIdentity)
        {
            claimsIdentity.AddClaim(CreateClaim(ClaimTypes.Role, Superuser));
        }

        private static void AddRoleForAllPermissionsToIdentity(ClaimsIdentity claimsIdentity, IReadOnlyCollection<string> permissions)
        {
            foreach (var permission in permissions)
            {
                claimsIdentity.AddClaim(CreateClaim(ClaimTypes.Role, permission));
            }
        }

        private static void AddUserDataClaimForAllOpenProjectsToIdentity(ClaimsIdentity claimsIdentity, IReadOnlyCollection<AccessableProject> projects)
        {
            foreach (var project in projects)
            {
                claimsIdentity.AddClaim(CreateClaim(ClaimTypes.UserData, GetProjectClaimValue(project.Name)));
                claimsIdentity.AddClaim(CreateClaim(ClaimTypes.UserData, GetProjectClaimValue(project.ProCoSysGuid)));
            }
        }

        private static void AddUserDataClaimForAllRestrictionRolesToIdentity(ClaimsIdentity claimsIdentity, string[] restrictions)
        {
            foreach (var restriction in restrictions)
            {
                claimsIdentity.AddClaim(CreateClaim(ClaimTypes.UserData, GetRestrictionRoleClaimValue(restriction)));
            }
        }

        private static Claim CreateClaim(string claimType, string claimValue)
            => new(claimType, claimValue, null, ClaimsIssuer);
    }
}
