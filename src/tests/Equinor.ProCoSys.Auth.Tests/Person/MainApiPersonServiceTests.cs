using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Client;
using Equinor.ProCoSys.Auth.Person;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Equinor.ProCoSys.Auth.Tests.Person
{
    [TestClass]
    public class MainApiPersonServiceTests
    {
        private readonly Guid _azureOid = Guid.NewGuid();
        private IOptionsMonitor<MainApiOptions> _mainApiOptionsMock;
        private IMainApiClient _mainApiClientMock;
        private MainApiPersonService _dut;

        [TestInitialize]
        public void Setup()
        {
            _mainApiOptionsMock = Substitute.For<IOptionsMonitor<MainApiOptions>>();
            _mainApiOptionsMock.CurrentValue
                .Returns(new MainApiOptions { ApiVersion = "4.0", BaseAddress = "http://example.com" });
            _mainApiClientMock = Substitute.For<IMainApiClient>();

            _dut = new MainApiPersonService(_mainApiClientMock, _mainApiOptionsMock);
        }

        [TestMethod]
        public async Task TryGetPersonByOidAsync_ShouldReturnPerson()
        {
            // Arrange
            var person = new ProCoSysPerson { FirstName = "Lars", LastName = "Monsen" };
            _mainApiClientMock.TryQueryAndDeserializeAsApplicationAsync<ProCoSysPerson>(Arg.Any<string>())
                .Returns(person);

            // Act
            var result = await _dut.TryGetPersonByOidAsync(_azureOid);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(person.FirstName, result.FirstName);
            Assert.AreEqual(person.LastName, result.LastName);
        }

        [TestMethod]
        public async Task TryGetAllPersonsAsync_ShouldReturnPersons()
        {
            var plant = "APlant";
            var url = _mainApiOptionsMock.CurrentValue.BaseAddress 
                      + $"/Person/AllPersons?plantId={plant}&api-version={_mainApiOptionsMock.CurrentValue.ApiVersion}";

            // Arrange
            _mainApiClientMock.TryQueryAndDeserializeAsync<List<ProCoSysPerson>>(url)
                .Returns([
                    new()
                    {
                        AzureOid = "asdf-fghj-qwer-tyui",
                        Email = "test@email.com",
                        FirstName = "Ola",
                        LastName = "Hansen",
                        UserName = "oha@mail.com",
                        Id = 5
                    },
                    new()
                    {
                        AzureOid = "1234-4567-6789-5432",
                        Email = "test2@email.com",
                        FirstName = "Hans",
                        LastName = "Olsen",
                        UserName = "hans@mail.com",
                        Id = 5
                    }
                ]);

            // Act
            var result = await _dut.GetAllPersonsAsync(plant, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count == 2);
        }
    }
}
