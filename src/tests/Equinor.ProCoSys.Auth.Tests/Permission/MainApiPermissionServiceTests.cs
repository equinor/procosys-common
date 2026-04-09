using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Client;
using Equinor.ProCoSys.Auth.Permission;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Equinor.ProCoSys.Auth.Tests.Permission
{
    [TestClass]
    public class MainApiPermissionServiceTests
    {
        private readonly Guid _azureOid = Guid.NewGuid();
        private readonly string _plant = "PCS$TESTPLANT";
        private IMainApiClientForApplication _mainApiClientForApplicationMock;
        private IMainApiClientForUser _mainApiClientForUserMock;
        private MainApiPermissionService _dut;
        private readonly MainApiOptions _mainApiOptions = new() { ApiVersion = "4.0", BaseAddress = "http://example.com/api/" };

        [TestInitialize]
        public void Setup()
        {
            var mainApiOptionsMock = Substitute.For<IOptionsMonitor<MainApiOptions>>();
            mainApiOptionsMock.CurrentValue
                .Returns(_mainApiOptions);
            _mainApiClientForApplicationMock = Substitute.For<IMainApiClientForApplication>();
            _mainApiClientForUserMock = Substitute.For<IMainApiClientForUser>();

            _dut = new MainApiPermissionService(_mainApiClientForApplicationMock, _mainApiClientForUserMock, mainApiOptionsMock);
        }

        [TestMethod]
        public async Task GetAllPlants_ShouldUseClientForApplication()
        {
            // Act
            await _dut.GetAllPlantsForUserAsync(_azureOid, CancellationToken.None);

            // Assert
            var url = $"{_mainApiOptions.BaseAddress}Plants/ForUser" +
                      $"?azureOid={_azureOid:D}" +
                      "&includePlantsWithoutAccess=true" +
                      $"&api-version={_mainApiOptions.ApiVersion}";
            await _mainApiClientForApplicationMock.Received(1)
                .QueryAndDeserializeAsync<List<AccessablePlant>>(url, CancellationToken.None);
            await _mainApiClientForUserMock.Received(0)
                .QueryAndDeserializeAsync<List<AccessablePlant>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public async Task GetPermissions_ShouldUseClientForUser()
        {
            // Act
            await _dut.GetPermissionsForCurrentUserAsync(_plant, CancellationToken.None);

            // Assert
            var url = $"{_mainApiOptions.BaseAddress}Permissions" +
                      $"?plantId={_plant}" +
                      $"&api-version={_mainApiOptions.ApiVersion}";
            await _mainApiClientForApplicationMock.Received(0)
                .QueryAndDeserializeAsync<List<string>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await _mainApiClientForUserMock.Received(1)
                .QueryAndDeserializeAsync<List<string>>(url, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetAllOpenProjectsAsync_ShouldUseClientForUser()
        {
            // Act
            await _dut.GetAllOpenProjectsForCurrentUserAsync(_plant, CancellationToken.None);

            // Assert
            var url = $"{_mainApiOptions.BaseAddress}Projects" +
                      $"?plantId={_plant}" +
                      "&withCommPkgsOnly=false" +
                      "&includeProjectsWithoutAccess=true" +
                      $"&api-version={_mainApiOptions.ApiVersion}";
            await _mainApiClientForApplicationMock.Received(0)
                .QueryAndDeserializeAsync<List<AccessableProject>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await _mainApiClientForUserMock.Received(1)
                .QueryAndDeserializeAsync<List<AccessableProject>>(url, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetAllOpenProjectsAsync_ShouldTracePlantForUser()
        {
            // Act
            await _dut.GetAllOpenProjectsForCurrentUserAsync(_plant, CancellationToken.None);

            // Assert
            var url = $"{_mainApiOptions.BaseAddress}Me/TracePlant" +
                      $"?plantId={_plant}" +
                      $"&api-version={_mainApiOptions.ApiVersion}";
            await _mainApiClientForApplicationMock.Received(0)
                .PostAsync(Arg.Any<string>(), Arg.Any<HttpContent>(), Arg.Any<CancellationToken>());
            await _mainApiClientForUserMock.Received(1)
                .PostAsync(url, Arg.Any<HttpContent>(), CancellationToken.None);
        }

        [TestMethod]
        public async Task GetRestrictionRolesAsync_ShouldUseClientForUser()
        {
            // Act
            await _dut.GetRestrictionRolesForCurrentUserAsync(_plant, CancellationToken.None);

            // Assert
            var url = $"{_mainApiOptions.BaseAddress}ContentRestrictions" +
                      $"?plantId={_plant}" +
                      $"&api-version={_mainApiOptions.ApiVersion}";
            await _mainApiClientForApplicationMock.Received(0)
                .QueryAndDeserializeAsync<List<string>>(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await _mainApiClientForUserMock.Received(1)
                .QueryAndDeserializeAsync<List<string>>(url, CancellationToken.None);
        }
    }
}
