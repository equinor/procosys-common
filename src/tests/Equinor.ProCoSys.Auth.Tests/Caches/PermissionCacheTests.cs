using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Caches;
using Equinor.ProCoSys.Auth.Permission;
using Equinor.ProCoSys.Common.Caches;
using Equinor.ProCoSys.Common.Misc;
using Equinor.ProCoSys.Common.Tests;
using Equinor.ProCoSys.Common.Time;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Equinor.ProCoSys.Auth.Tests.Caches
{
    [TestClass]
    public class PermissionCacheTests
    {
        private PermissionCache _dut;
        private readonly Guid _currentUserOid = new("{3BFB54C7-91E2-422E-833F-951AD07FE37F}");
        private IPermissionApiService _permissionApiServiceMock;
        private ICurrentUserProvider _currentUserProviderMock;
        private readonly string _plant1IdWithAccess = "P1";
        private readonly string _plant2IdWithAccess = "P2";
        private readonly string _plantIdWithoutAccess = "P3";
        private readonly string _plant1TitleWithAccess = "P1 Title";
        private readonly string _plant2TitleWithAccess = "P2 Title";
        private readonly string _plantTitleWithoutAccess = "P3 Title";
        private readonly string _permission1 = "A";
        private readonly string _permission2 = "B";
        private readonly string _project1WithAccess = "P1";
        private readonly string _project2WithAccess = "P2";
        private readonly string _projectWithoutAccess = "P3";
        private readonly string _restriction1 = "R1";
        private readonly string _restriction2 = "R2";

        [TestInitialize]
        public void Setup()
        {
            TimeService.SetProvider(new ManualTimeProvider(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            _permissionApiServiceMock = Substitute.For<IPermissionApiService>();
            _permissionApiServiceMock.GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None)
                .Returns(
                [
                    new()
                    {
                        Id = _plant1IdWithAccess,
                        Title = _plant1TitleWithAccess,
                        HasAccess = true
                    },

                    new()
                    {
                        Id = _plant2IdWithAccess,
                        Title = _plant2TitleWithAccess,
                        HasAccess = true
                    },

                    new()
                    {
                        Id = _plantIdWithoutAccess,
                        Title = _plantTitleWithoutAccess
                    }
                ]);
            _permissionApiServiceMock.GetAllOpenProjectsForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None)
                .Returns([
                    new() { Name = _project1WithAccess, HasAccess = true },
                    new() { Name = _project2WithAccess, HasAccess = true },
                    new() { Name = _projectWithoutAccess }
                ]);
            _permissionApiServiceMock.GetPermissionsForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None)
                .Returns([_permission1, _permission2]);
            _permissionApiServiceMock.GetRestrictionRolesForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None)
                .Returns([_restriction1, _restriction2]);

            var optionsMock = Substitute.For<IOptionsMonitor<CacheOptions>>();
            optionsMock.CurrentValue.Returns(new CacheOptions());

            _currentUserProviderMock = Substitute.For<ICurrentUserProvider>();
            _currentUserProviderMock.GetCurrentUserOid().Returns(_currentUserOid);

            OptionsWrapper<MemoryDistributedCacheOptions> _options = new(new MemoryDistributedCacheOptions());
            _dut = new PermissionCache(
                new DistributedCacheManager(new MemoryDistributedCache(_options), Substitute.For<ILogger<DistributedCacheManager>>()),
                _currentUserProviderMock,
                _permissionApiServiceMock,
                optionsMock);
        }

        [TestMethod]
        public async Task GetPlantIdsWithAccessForUserAsync_ShouldReturnPlantIdsFromPlantApiServiceFirstTime()
        {
            // Act
            var result = await _dut.GetPlantIdsWithAccessForUserAsync(_currentUserOid, CancellationToken.None);

            // Assert
            AssertPlants(result);
            await _permissionApiServiceMock.Received(1).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetPlantIdsWithAccessForUserAsync_ShouldReturnPlantsFromCacheSecondTime()
        {
            await _dut.GetPlantIdsWithAccessForUserAsync(_currentUserOid, CancellationToken.None);

            // Act
            var result = await _dut.GetPlantIdsWithAccessForUserAsync(_currentUserOid, CancellationToken.None);

            // Assert
            AssertPlants(result);
            // since GetPlantIdsWithAccessForUserAsyncAsync has been called twice, but GetAllPlantsAsync has been called once, the second Get uses cache
            await _permissionApiServiceMock.Received(1).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);
        }

        [TestMethod]
        public async Task HasCurrentUserAccessToPlantAsync_ShouldReturnTrue_WhenKnownPlant()
        {
            // Act
            var result = await _dut.HasCurrentUserAccessToPlantAsync(_plant2IdWithAccess, CancellationToken.None);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HasCurrentUserAccessToPlantAsync_ShouldReturnFalse_WhenUnknownPlant()
        {
            // Act
            var result = await _dut.HasCurrentUserAccessToPlantAsync("XYZ", CancellationToken.None);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HasCurrentUserAccessToPlantAsync_ShouldReturnPlantIdsFromPlantApiServiceFirstTime()
        {
            // Act
            await _dut.HasCurrentUserAccessToPlantAsync(_plant2IdWithAccess, CancellationToken.None);

            // Assert
            await _permissionApiServiceMock.Received(1).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);
        }

        [TestMethod]
        public async Task HasCurrentUserAccessToPlantAsync_ShouldReturnPlantsFromCacheSecondTime()
        {
            await _dut.HasCurrentUserAccessToPlantAsync("XYZ", CancellationToken.None);
            // Act
            await _dut.HasCurrentUserAccessToPlantAsync(_plant2IdWithAccess, CancellationToken.None);

            // Assert
            await _permissionApiServiceMock.Received(1).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);
        }

        [TestMethod]
        public async Task HasUserAccessToPlantAsync_ShouldReturnTrue_WhenKnownPlant()
        {
            // Act
            var result = await _dut.HasUserAccessToPlantAsync(_plant2IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task HasUserAccessToPlantAsync_ShouldReturnFalse_WhenUnknownPlant()
        {
            // Act
            var result = await _dut.HasUserAccessToPlantAsync("XYZ", _currentUserOid, CancellationToken.None);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task HasUserAccessToPlantAsync_ShouldReturnPlantIdsFromPlantApiServiceFirstTime()
        {
            // Act
            await _dut.HasUserAccessToPlantAsync(_plant2IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            await _permissionApiServiceMock.Received(1).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);
        }

        [TestMethod]
        public async Task HasUserAccessToPlantAsync_ShouldReturnPlantsFromCache()
        {
            await _dut.HasUserAccessToPlantAsync("ABC", _currentUserOid, CancellationToken.None);
            // Act
            await _dut.HasUserAccessToPlantAsync(_plant2IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            await _permissionApiServiceMock.Received(1).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);
        }

        [TestMethod]
        public async Task IsAValidPlantForCurrentUserAsync_ShouldReturnTrue_WhenKnownPlantWithAccess()
        {
            // Act
            var result = await _dut.IsAValidPlantForCurrentUserAsync(_plant2IdWithAccess, CancellationToken.None);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task IsAValidPlantForCurrentUserAsync_ShouldReturnTrue_WhenKnownPlantWithoutAccess()
        {
            // Act
            var result = await _dut.IsAValidPlantForCurrentUserAsync(_plantIdWithoutAccess, CancellationToken.None);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task IsAValidPlantForCurrentUserAsync_ShouldReturnFalse_WhenUnknownPlant()
        {
            // Act
            var result = await _dut.IsAValidPlantForCurrentUserAsync("XYZ", CancellationToken.None);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetPlantTitleForCurrentUserAsync_ShouldReturnPlant_WhenKnownPlantWithAccess()
        {
            // Act
            var result = await _dut.GetPlantTitleForCurrentUserAsync(_plant2IdWithAccess, CancellationToken.None);

            // Assert
            Assert.AreEqual(_plant2TitleWithAccess, result);
        }

        [TestMethod]
        public async Task GetPlantTitleForCurrentUserAsync_ShouldReturnPlant_WhenKnownPlantWithoutAccess()
        {
            // Act
            var result = await _dut.GetPlantTitleForCurrentUserAsync(_plantIdWithoutAccess, CancellationToken.None);

            // Assert
            Assert.AreEqual(_plantTitleWithoutAccess, result);
        }

        [TestMethod]
        public async Task GetPlantTitleForCurrentUserAsync_ShouldReturnNull_WhenUnknownPlant()
        {
            // Act
            var result = await _dut.GetPlantTitleForCurrentUserAsync("XYZ", CancellationToken.None);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task Clear_ShouldForceGettingPlantsFromApiServiceAgain()
        {
            // Arrange
            var result = await _dut.HasUserAccessToPlantAsync(_plant2IdWithAccess, _currentUserOid, CancellationToken.None);
            Assert.IsTrue(result);
            await _permissionApiServiceMock.Received(1).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);

            // Act
            await _dut.ClearAllAsync(_plant2IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            result = await _dut.HasUserAccessToPlantAsync(_plant2IdWithAccess, _currentUserOid, CancellationToken.None);
            Assert.IsTrue(result);
            await _permissionApiServiceMock.Received(2).GetAllPlantsForUserAsync(_currentUserOid, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetPermissionsForUserAsync_ShouldReturnPermissionsFromPermissionApiServiceFirstTime()
        {
            // Act
            var result = await _dut.GetPermissionsForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            AssertPermissions(result);
            await _permissionApiServiceMock.Received(1).GetPermissionsForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetPermissionsForUserAsync_ShouldReturnPermissionsFromCacheSecondTime()
        {
            await _dut.GetPermissionsForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);
            // Act
            var result = await _dut.GetPermissionsForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            AssertPermissions(result);
            // since GetPermissionsForUserAsync has been called twice, but GetPermissionsAsync has been called once, the second Get uses cache
            await _permissionApiServiceMock.Received(1).GetPermissionsForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetProjectNamesForUserAsync_ShouldReturnProjectsFromPermissionApiServiceFirstTime()
        {
            // Act
            var result = await _dut.GetProjectNamesForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            AssertProjects(result);
            await _permissionApiServiceMock.GetAllOpenProjectsForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetProjectNamesForUserAsync_ShouldReturnProjectsFromCacheSecondTime()
        {
            await _dut.GetProjectNamesForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);
            // Act
            var result = await _dut.GetProjectNamesForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            AssertProjects(result);
            // since GetProjectNamesForUserAsync has been called twice, but GetProjectsAsync has been called once, the second Get uses cache
            await _permissionApiServiceMock.Received(1).GetAllOpenProjectsForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetRestrictionRolesForUserAsync_ShouldReturnPermissionsFromPermissionApiServiceFirstTime()
        {
            // Act
            var result = await _dut.GetRestrictionRolesForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            AssertRestrictions(result);
            await _permissionApiServiceMock.Received(1).GetRestrictionRolesForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetRestrictionRolesForUserAsync_ShouldReturnPermissionsFromCacheSecondTime()
        {
            await _dut.GetRestrictionRolesForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);
            // Act
            var result = await _dut.GetRestrictionRolesForUserAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            AssertRestrictions(result);
            // since GetRestrictionRolesForUserAsync has been called twice, but GetRestrictionRolesAsync has been called once, the second Get uses cache
            await _permissionApiServiceMock.Received(1).GetRestrictionRolesForCurrentUserAsync(_plant1IdWithAccess, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetPermissionsForUserAsync_ShouldThrowExceptionWhenOidIsEmpty()
            => await Assert.ThrowsExceptionAsync<Exception>(() => _dut.GetPermissionsForUserAsync(_plant1IdWithAccess, Guid.Empty, CancellationToken.None));

        [TestMethod]
        public async Task GetProjectNamesForUserAsync_ShouldThrowExceptionWhenOidIsEmpty()
            => await Assert.ThrowsExceptionAsync<Exception>(() => _dut.GetProjectNamesForUserAsync(_plant1IdWithAccess, Guid.Empty, CancellationToken.None));

        [TestMethod]
        public async Task GetRestrictionRolesForUserAsync_ShouldThrowExceptionWhenOidIsEmpty()
            => await Assert.ThrowsExceptionAsync<Exception>(() => _dut.GetRestrictionRolesForUserAsync(_plant1IdWithAccess, Guid.Empty, CancellationToken.None));

        [TestMethod]
        public async Task ClearAll_ShouldClearAllPermissionCaches()
        {
            // Arrange
            var cacheManagerMock = Substitute.For<ICacheManager>();
            var dut = new PermissionCache(
                cacheManagerMock,
                _currentUserProviderMock,
                _permissionApiServiceMock,
                Substitute.For<IOptionsMonitor<CacheOptions>>());

            // Act
            await dut.ClearAllAsync(_plant1IdWithAccess, _currentUserOid, CancellationToken.None);

            // Assert
            await cacheManagerMock.Received(5).RemoveAsync(Arg.Any<string>(), CancellationToken.None);
        }

        private void AssertPlants(IList<string> result)
        {
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(_plant1IdWithAccess, result.First());
            Assert.AreEqual(_plant2IdWithAccess, result.Last());
        }

        private void AssertPermissions(IList<string> result)
        {
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(_permission1, result.First());
            Assert.AreEqual(_permission2, result.Last());
        }

        private void AssertProjects(IList<string> result)
        {
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(_project1WithAccess, result.First());
            Assert.AreEqual(_project2WithAccess, result.Last());
        }

        private void AssertRestrictions(IList<string> result)
        {
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(_restriction1, result.First());
            Assert.AreEqual(_restriction2, result.Last());
        }
    }
}
