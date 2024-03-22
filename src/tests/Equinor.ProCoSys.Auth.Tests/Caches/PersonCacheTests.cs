using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Caches;
using Equinor.ProCoSys.Auth.Person;
using Equinor.ProCoSys.Common.Caches;
using Equinor.ProCoSys.Common.Tests;
using Equinor.ProCoSys.Common.Time;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Equinor.ProCoSys.Auth.Tests.Caches
{
    [TestClass]
    public class PersonCacheTests
    {
        private PersonCache _dut;
        private ProCoSysPerson _person;
        private readonly Guid _currentUserOid = new("{3BFB54C7-91E2-422E-833F-951AD07FE37F}");
        private IPersonApiService _personApiServiceMock;
        private const string TestPlant = "PA";

        [TestInitialize]
        public void Setup()
        {
            TimeService.SetProvider(new ManualTimeProvider(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            _personApiServiceMock = Substitute.For<IPersonApiService>();
            _person = new ProCoSysPerson { FirstName = "Erling", LastName = "Braut Haaland"};
            _personApiServiceMock.TryGetPersonByOidAsync(_currentUserOid).Returns(_person);

            _personApiServiceMock.GetAllPersonsAsync(TestPlant, CancellationToken.None).Returns([
                new() {
                    AzureOid = "asdf-fghj-qwer-tyui",
                    Email = "test@email.com",
                    FirstName = "Ola",
                    LastName = "Hansen",
                    UserName = "oha@mail.com",
                    Id = 5
                },
                new() {
                    AzureOid = "1234-4567-6789-5432",
                    Email = "test2@email.com",
                    FirstName = "Hans",
                    LastName = "Olsen",
                    UserName = "hans@mail.com",
                    Id = 5
                }
            ]);

            var optionsMock = Substitute.For<IOptionsMonitor<CacheOptions>>();
            optionsMock.CurrentValue
                .Returns(new CacheOptions());

            _dut = new PersonCache(
                new CacheManager(),
                _personApiServiceMock,
                optionsMock);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnPersonFromPersonApiServiceFirstTime()
        {
            // Act
            var result = await _dut.GetAsync(_currentUserOid);

            // Assert
            AssertPerson(result);
            await _personApiServiceMock.Received(1).TryGetPersonByOidAsync(_currentUserOid);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnPersonsFromCacheSecondTime()
        {
            await _dut.GetAsync(_currentUserOid);

            // Act
            var result = await _dut.GetAsync(_currentUserOid);

            // Assert
            AssertPerson(result);
            // since GetAsync has been called twice, but TryGetPersonByOidAsync has been called once, the second Get uses cache
            await _personApiServiceMock.Received(1).TryGetPersonByOidAsync(_currentUserOid);
        }

        [TestMethod]
        public async Task GetAllPersons_ShouldReturnPersonListFromPersonApiServiceFirstTime()
        {
            // Act
            var result = await _dut.GetAllPersonsAsync(TestPlant, CancellationToken.None);

            // Assert
            AssertAllPersons(result);
            await _personApiServiceMock.Received(1).GetAllPersonsAsync(TestPlant, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetAllPersons_ShouldReturnPersonListsFromCacheSecondTime()
        {
            await _dut.GetAllPersonsAsync(TestPlant, CancellationToken.None);

            // Act
            var result = await _dut.GetAllPersonsAsync(TestPlant, CancellationToken.None);

            // Assert
            AssertAllPersons(result);
            // since GetCheckListAsync has been called twice, but TryGetCheckListByOidAsync has been called once, the second Get uses cache
            await _personApiServiceMock.Received(1).GetAllPersonsAsync(TestPlant, CancellationToken.None);
        }

        private void AssertPerson(ProCoSysPerson person)
        {
            Assert.AreEqual(_person.FirstName, person.FirstName);
            Assert.AreEqual(_person.LastName, person.LastName);
        }
        private static void AssertAllPersons(ICollection list) => Assert.IsTrue(list.Count == 2);
    }
}
