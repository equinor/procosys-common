using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Authentication;
using Equinor.ProCoSys.Auth.Authorization;
using Equinor.ProCoSys.Auth.Caches;
using Equinor.ProCoSys.Auth.Permission;
using Equinor.ProCoSys.Auth.Person;
using Equinor.ProCoSys.Common.Misc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Equinor.ProCoSys.Auth.Tests.Authorization
{
    [TestClass]
    public class ClaimsTransformationTests
    {
        private ClaimsTransformation _dut;
        private readonly Guid _oid = new("{0b627d64-8113-40e1-9394-60282fb6bb9f}");
        private ClaimsPrincipal _principalWithOid;
        private readonly string _plant1 = "_plant1";
        private readonly string _plant2 = "_plant2";
        private readonly string _permission1_Plant1 = "A";
        private readonly string _permission2_Plant1 = "B";
        private readonly string _permission1_Plant2 = "C";
        private readonly string _projectName1_Plant1 = "Pro1";
        private readonly Guid _projectGuid1_Plant1 = new("11111111-1111-1111-1111-111111111111");
        private readonly string _projectName2_Plant1 = "Pro2";
        private readonly Guid _projectGuid2_Plant1 = new("22222222-2222-2222-2222-222222222222");
        private readonly string _projectName1_Plant2 = "Pro3";
        private readonly Guid _projectGuid1_Plant2 = new("33333333-3333-3333-3333-333333333333");
        private readonly string _restriction1_Plant1 = "Res1";
        private readonly string _restriction2_Plant1 = "Res2";
        private readonly string _restriction1_Plant2 = "Res3";

        private ILocalPersonRepository _localPersonRepositoryMock;
        private IPersonCache _personCacheMock;
        private IPlantProvider _plantProviderMock;
        private MainApiAuthenticatorOptions _mainApiAuthenticatorOptions;

        [TestInitialize]
        public void Setup()
        {
            _localPersonRepositoryMock = Substitute.For<ILocalPersonRepository>();
            _personCacheMock = Substitute.For<IPersonCache>();

            var proCoSysPersonNotSuper = new ProCoSysPerson
            {
                Super = false,
                AzureOid = _oid.ToString()
            };
            _localPersonRepositoryMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns(proCoSysPersonNotSuper);
            _personCacheMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns(proCoSysPersonNotSuper);

            _plantProviderMock = Substitute.For<IPlantProvider>();
            _plantProviderMock.Plant.Returns(_plant1);

            var permissionCacheMock = Substitute.For<IPermissionCache>();
            permissionCacheMock.GetUserPlantPermissionDataAsync(_oid, _plant1, Arg.Any<CancellationToken>())
                .Returns(new UserPlantPermissionData(_oid, _plant1,
                    [new AccessablePlant { HasAccess = true, Id = _plant1, Title = _plant1 }], [_permission1_Plant1, _permission2_Plant1],
                    [
                        new AccessableProject
                        {
                            Name = _projectName1_Plant1,
                            ProCoSysGuid = _projectGuid1_Plant1
                        },
                        new AccessableProject
                        {
                            Name = _projectName2_Plant1,
                            ProCoSysGuid = _projectGuid2_Plant1
                        }
                    ], [_restriction1_Plant1, _restriction2_Plant1]));

            permissionCacheMock.GetUserPlantPermissionDataAsync(_oid, _plant2, Arg.Any<CancellationToken>())
                .Returns(new UserPlantPermissionData(_oid, _plant2,
                    [new AccessablePlant { HasAccess = true, Id = _plant2, Title = _plant2 }], [_permission1_Plant2],
                    [
                        new AccessableProject
                        {
                            Name = _projectName1_Plant2,
                            ProCoSysGuid = _projectGuid1_Plant2
                        }
                    ], [_restriction1_Plant2]));
            permissionCacheMock.HasUserAccessToPlantAsync(_plant1, _oid, Arg.Any<CancellationToken>()).Returns(true);
            permissionCacheMock.HasUserAccessToPlantAsync(_plant2, _oid, Arg.Any<CancellationToken>()).Returns(true);
            permissionCacheMock.GetPermissionsForUserAsync(_plant1, _oid, Arg.Any<CancellationToken>())
                .Returns(new List<string> { _permission1_Plant1, _permission2_Plant1 });
            permissionCacheMock.GetProjectsForUserAsync(_plant1, _oid, Arg.Any<CancellationToken>())
                .Returns(new List<AccessableProject>
                {
                    new()
                    {
                        Name = _projectName1_Plant1,
                        ProCoSysGuid = _projectGuid1_Plant1
                    },
                    new()
                    {
                        Name = _projectName2_Plant1,
                        ProCoSysGuid = _projectGuid2_Plant1
                    }
                });
            permissionCacheMock.GetRestrictionRolesForUserAsync(_plant1, _oid, Arg.Any<CancellationToken>())
                .Returns(new List<string> { _restriction1_Plant1, _restriction2_Plant1 });

            permissionCacheMock.GetPermissionsForUserAsync(_plant2, _oid, Arg.Any<CancellationToken>())
                .Returns(new List<string> { _permission1_Plant2 });
            permissionCacheMock.GetProjectsForUserAsync(_plant2, _oid, Arg.Any<CancellationToken>())
                .Returns(new List<AccessableProject>
                {
                    new()
                    {
                        Name = _projectName1_Plant2,
                        ProCoSysGuid = _projectGuid1_Plant2
                    }
                });
            permissionCacheMock.GetRestrictionRolesForUserAsync(_plant2, _oid, Arg.Any<CancellationToken>())
                .Returns(new List<string> { _restriction1_Plant2 });

            var loggerMock = Substitute.For<ILogger<ClaimsTransformation>>();

            _principalWithOid = new ClaimsPrincipal();
            var claimsIdentity = new ClaimsIdentity();
            claimsIdentity.AddClaim(new Claim(ClaimsExtensions.Oid, _oid.ToString()));
            _principalWithOid.AddIdentity(claimsIdentity);

            var authenticatorOptionsMock = Substitute.For<IOptionsMonitor<MainApiAuthenticatorOptions>>();
            _mainApiAuthenticatorOptions = new MainApiAuthenticatorOptions
            {
                MainApiScope = ""
            };
            authenticatorOptionsMock.CurrentValue.Returns(_mainApiAuthenticatorOptions);

            _dut = new ClaimsTransformation(
                _localPersonRepositoryMock,
                _personCacheMock,
                _plantProviderMock,
                permissionCacheMock,
                loggerMock,
                authenticatorOptionsMock);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNotAddRoleClaimsForSuper_WhenPersonNotSuper()
        {
            // Act
            var result = await _dut.TransformAsync(_principalWithOid);

            // Assert
            var roleClaims = GetRoleClaims(result.Claims);
            Assert.IsTrue(roleClaims.All(r => r.Value != ClaimsTransformation.Superuser));
        }

        [TestMethod]
        public async Task TransformAsync_ShouldAddRoleClaimsForSuper_WhenPersonIsSuper()
        {
            // Arrange 
            var proCoSysPersonSuper = new ProCoSysPerson
            {
                Super = true
            };
            _localPersonRepositoryMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns(proCoSysPersonSuper);

            // Act
            var result = await _dut.TransformAsync(_principalWithOid);

            // Assert
            var roleClaims = GetRoleClaims(result.Claims);
            Assert.IsTrue(roleClaims.Any(r => r.Value == ClaimsTransformation.Superuser));
        }

        [TestMethod]
        public async Task TransformAsync_ShouldAddRoleClaimsForSuper_WhenPersonIsSuper_AndNoPlantGiven()
        {
            // Arrange 
            _plantProviderMock.Plant.Returns((string)null);
            var proCoSysPersonSuper = new ProCoSysPerson
            {
                Super = true
            };
            _localPersonRepositoryMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns(proCoSysPersonSuper);

            // Act
            var result = await _dut.TransformAsync(_principalWithOid);

            // Assert
            var roleClaims = GetRoleClaims(result.Claims);
            Assert.AreEqual(1, roleClaims.Count);
            Assert.IsTrue(roleClaims.Any(r => r.Value == ClaimsTransformation.Superuser));
        }

        [TestMethod]
        public async Task TransformAsync_ShouldAddRoleClaimsForPermissions()
        {
            var result = await _dut.TransformAsync(_principalWithOid);

            AssertRoleClaimsForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_Twice_ShouldNotDuplicateRoleClaimsForPermissions()
        {
            await _dut.TransformAsync(_principalWithOid);
            var result = await _dut.TransformAsync(_principalWithOid);

            AssertRoleClaimsForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldAddUserDataClaimsForProjects()
        {
            var result = await _dut.TransformAsync(_principalWithOid);

            AssertProjectClaimsForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNotAddUserDataClaimsForProjects_WhenDisabled()
        {
            // Arrange
            _mainApiAuthenticatorOptions.DisableProjectUserDataClaims = true;

            // Act
            var result = await _dut.TransformAsync(_principalWithOid);

            // Assert
            var projectClaims = GetProjectClaims(result.Claims);
            Assert.AreEqual(0, projectClaims.Count);
        }

        [TestMethod]
        public async Task TransformAsync_Twice_ShouldNotDuplicateUserDataClaimsForProjects()
        {
            await _dut.TransformAsync(_principalWithOid);
            var result = await _dut.TransformAsync(_principalWithOid);

            AssertProjectClaimsForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldAddUserDataClaimsForRestrictionRole()
        {
            var result = await _dut.TransformAsync(_principalWithOid);

            AssertRestrictionRoleForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNotAddUserDataClaimsForRestrictionRole_WhenDisabled()
        {
            // Arrange
            _mainApiAuthenticatorOptions.DisableRestrictionRoleUserDataClaims = true;

            // Act
            var result = await _dut.TransformAsync(_principalWithOid);

            var restrictionRoleClaims = GetRestrictionRoleClaims(result.Claims);
            Assert.AreEqual(0, restrictionRoleClaims.Count);
        }

        [TestMethod]
        public async Task TransformAsync_Twice_ShouldNotDuplicateUserDataClaimsForRestrictionRole()
        {
            await _dut.TransformAsync(_principalWithOid);
            var result = await _dut.TransformAsync(_principalWithOid);

            AssertRestrictionRoleForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNotAddAnyClaims_ForPrincipalWithoutOid()
        {
            var result = await _dut.TransformAsync(new ClaimsPrincipal());

            Assert.AreEqual(0, GetProjectClaims(result.Claims).Count);
            Assert.AreEqual(0, GetRoleClaims(result.Claims).Count);
            Assert.AreEqual(0, GetRestrictionRoleClaims(result.Claims).Count);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNotAddAnyClaims_WhenPersonNotFoundInProCoSys()
        {
            _localPersonRepositoryMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns((ProCoSysPerson)null);
            _personCacheMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns((ProCoSysPerson)null);

            var result = await _dut.TransformAsync(_principalWithOid);

            Assert.AreEqual(0, GetProjectClaims(result.Claims).Count);
            Assert.AreEqual(0, GetRoleClaims(result.Claims).Count);
            Assert.AreEqual(0, GetRestrictionRoleClaims(result.Claims).Count);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldAddRoleClaimsForPermissions_WhenPersonFoundLocalButNotInCache()
        {
            _personCacheMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns((ProCoSysPerson)null);

            var result = await _dut.TransformAsync(_principalWithOid);

            AssertRoleClaimsForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldAddRoleClaimsForPermissions_WhenPersonNotFoundLocalButInCache()
        {
            _localPersonRepositoryMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns((ProCoSysPerson)null);

            var result = await _dut.TransformAsync(_principalWithOid);

            AssertRoleClaimsForPlant1(result.Claims);
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNotAddAnyClaims_WhenPersonIsNotSuper_AndNoPlantGiven()
        {
            _plantProviderMock.Plant.Returns((string)null);

            var result = await _dut.TransformAsync(_principalWithOid);

            Assert.AreEqual(0, GetProjectClaims(result.Claims).Count);
            Assert.AreEqual(0, GetRoleClaims(result.Claims).Count);
            Assert.AreEqual(0, GetRestrictionRoleClaims(result.Claims).Count);
        }

        [TestMethod]
        public async Task TransformAsync_OnSecondPlant_ShouldClearAllClaimsForFirstPlant()
        {
            var result = await _dut.TransformAsync(_principalWithOid);
            AssertRoleClaimsForPlant1(result.Claims);
            AssertProjectClaimsForPlant1(result.Claims);
            AssertRestrictionRoleForPlant1(result.Claims);

            _plantProviderMock.Plant.Returns(_plant2);
            result = await _dut.TransformAsync(_principalWithOid);

            var claims = GetRoleClaims(result.Claims);
            Assert.AreEqual(1, claims.Count);
            Assert.IsNotNull(claims.SingleOrDefault(r => r.Value == _permission1_Plant2));

            claims = GetProjectClaims(result.Claims);
            Assert.AreEqual(2, claims.Count);
            Assert.IsNotNull(claims.SingleOrDefault(r =>
                r.Value == ClaimsTransformation.GetProjectClaimValue(_projectName1_Plant2)));

            claims = GetRestrictionRoleClaims(result.Claims);
            Assert.AreEqual(1, claims.Count);
            Assert.IsNotNull(claims.SingleOrDefault(r =>
                r.Value == ClaimsTransformation.GetRestrictionRoleClaimValue(_restriction1_Plant2)));
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNAddExistsClaimsFoPerson_WhenPersonExists()
        {
            var result = await _dut.TransformAsync(_principalWithOid);

            Assert.IsTrue(result.Claims.PersonExistsLocally(_oid.ToString()));
        }

        [TestMethod]
        public async Task TransformAsync_ShouldNotAddExistsClaimsFoPerson_WhenPersonNotExists()
        {
            _localPersonRepositoryMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns((ProCoSysPerson)null);
            _personCacheMock.GetAsync(_oid, Arg.Any<CancellationToken>()).Returns((ProCoSysPerson)null);

            var result = await _dut.TransformAsync(_principalWithOid);

            Assert.IsFalse(result.Claims.PersonExistsLocally(_oid.ToString()));
        }

        private void AssertRoleClaimsForPlant1(IEnumerable<Claim> claims)
        {
            var roleClaims = GetRoleClaims(claims);
            Assert.AreEqual(2, roleClaims.Count);
            Assert.IsTrue(roleClaims.Any(r => r.Value == _permission1_Plant1));
            Assert.IsTrue(roleClaims.Any(r => r.Value == _permission2_Plant1));
        }

        private void AssertProjectClaimsForPlant1(IEnumerable<Claim> claims)
        {
            var projectClaims = GetProjectClaims(claims);
            Assert.AreEqual(4, projectClaims.Count);
            Assert.IsTrue(projectClaims.Any(r =>
                r.Value == ClaimsTransformation.GetProjectClaimValue(_projectName1_Plant1)));
            Assert.IsTrue(projectClaims.Any(r =>
                r.Value == ClaimsTransformation.GetProjectClaimValue(_projectGuid1_Plant1)));
            Assert.IsTrue(projectClaims.Any(r =>
                r.Value == ClaimsTransformation.GetProjectClaimValue(_projectName2_Plant1)));
            Assert.IsTrue(projectClaims.Any(r =>
                r.Value == ClaimsTransformation.GetProjectClaimValue(_projectGuid2_Plant1)));
        }

        private void AssertRestrictionRoleForPlant1(IEnumerable<Claim> claims)
        {
            var restrictionRoleClaims = GetRestrictionRoleClaims(claims);
            Assert.AreEqual(2, restrictionRoleClaims.Count);
            Assert.IsTrue(restrictionRoleClaims.Any(r =>
                r.Value == ClaimsTransformation.GetRestrictionRoleClaimValue(_restriction1_Plant1)));
            Assert.IsTrue(restrictionRoleClaims.Any(r =>
                r.Value == ClaimsTransformation.GetRestrictionRoleClaimValue(_restriction2_Plant1)));
        }

        private static List<Claim> GetRestrictionRoleClaims(IEnumerable<Claim> claims)
            => claims
                .Where(c => c.Type == ClaimTypes.UserData &&
                            c.Value.StartsWith(ClaimsTransformation.RestrictionRolePrefix))
                .ToList();

        private static List<Claim> GetRoleClaims(IEnumerable<Claim> claims)
            => claims.Where(c => c.Type == ClaimTypes.Role).ToList();

        private static List<Claim> GetProjectClaims(IEnumerable<Claim> claims)
            => claims.Where(
                    c => c.Type == ClaimTypes.UserData && c.Value.StartsWith(ClaimsTransformation.ProjectPrefix))
                .ToList();
    }
}
