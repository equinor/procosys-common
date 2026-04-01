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
        private IMainApiClientForApplication _mainApiClientMock;
        private MainApiPersonService _dut;

        [TestInitialize]
        public void Setup()
        {
            _mainApiOptionsMock = Substitute.For<IOptionsMonitor<MainApiOptions>>();
            _mainApiOptionsMock.CurrentValue
                .Returns(new MainApiOptions { ApiVersion = "4.0", BaseAddress = "http://example.com" });
            _mainApiClientMock = Substitute.For<IMainApiClientForApplication>();

            _dut = new MainApiPersonService(_mainApiClientMock, _mainApiOptionsMock);
        }

        [TestMethod]
        public async Task TryGetPersonByOidAsync_ShouldReturnPerson()
        {
            // Arrange
            var person = new ProCoSysPerson { FirstName = "Lars", LastName = "Monsen" };
            _mainApiClientMock.TryQueryAndDeserializeAsync<ProCoSysPerson>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(person);

            // Act
            var result = await _dut.TryGetPersonByOidAsync(_azureOid, false, CancellationToken.None);

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
            ProCoSysPerson person1 = new()
            {
                AzureOid = "asdf-fghj-qwer-tyui",
                Email = "test@email.com",
                FirstName = "Ola",
                LastName = "Hansen",
                UserName = "oha@mail.com"
            };
            ProCoSysPerson person2 = new()
            {
                AzureOid = "1234-4567-6789-5432",
                Email = "test2@email.com",
                FirstName = "Hans",
                LastName = "Olsen",
                UserName = "hans@mail.com"
            };

            // Arrange
            _mainApiClientMock.TryQueryAndDeserializeAsync<List<ProCoSysPerson>>(url, Arg.Any<CancellationToken>())
                .Returns([
                    person1,
                    person2
                ]);

            // Act
            var result = await _dut.GetAllPersonsAsync(plant, CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count == 2);
            CollectionAssert.Contains(result, person1);
            CollectionAssert.Contains(result, person2);
        }
    }
}
