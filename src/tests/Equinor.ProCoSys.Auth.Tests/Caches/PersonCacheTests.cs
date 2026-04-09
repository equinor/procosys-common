using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Caches;
using Equinor.ProCoSys.Auth.Person;
using Equinor.ProCoSys.Common.Caches;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        private readonly ProCoSysPerson _person1 = new()
        {
            AzureOid = "asdf-fghj-qwer-tyui",
            Email = "test@email.com",
            FirstName = "Ola",
            LastName = "Hansen",
            UserName = "oha@mail.com"
        };
        private readonly ProCoSysPerson _person2 = new()
        {
            AzureOid = "1234-4567-6789-5432",
            Email = "test2@email.com",
            FirstName = "Hans",
            LastName = "Olsen",
            UserName = "hans@mail.com"
        };

        [TestInitialize]
        public void Setup()
        {
            _personApiServiceMock = Substitute.For<IPersonApiService>();
            _person = new ProCoSysPerson { FirstName = "Erling", LastName = "Braut Haaland" };
            _personApiServiceMock.TryGetPersonByOidAsync(_currentUserOid, true, CancellationToken.None).Returns(_person);

            _personApiServiceMock.GetAllPersonsAsync(TestPlant, CancellationToken.None).Returns([
                _person1,
                _person2
            ]);

            var optionsMock = Substitute.For<IOptionsMonitor<CacheOptions>>();
            optionsMock.CurrentValue
                .Returns(new CacheOptions());

            OptionsWrapper<MemoryDistributedCacheOptions> _options = new(new MemoryDistributedCacheOptions());
            _dut = new PersonCache(
                new DistributedCacheManager(new MemoryDistributedCache(_options), Substitute.For<ILogger<DistributedCacheManager>>()),
                _personApiServiceMock,
                optionsMock);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnPersonFromPersonApiServiceFirstTime()
        {
            // Act
            var result = await _dut.GetAsync(_currentUserOid, CancellationToken.None, true);

            // Assert
            AssertPerson(_person, result);
            await _personApiServiceMock.Received(1).TryGetPersonByOidAsync(_currentUserOid, true, CancellationToken.None);
        }

        [TestMethod]
        public async Task GetAsync_ShouldReturnPersonsFromCacheSecondTime()
        {
            await _dut.GetAsync(_currentUserOid, CancellationToken.None, true);

            // Act
            var result = await _dut.GetAsync(_currentUserOid, CancellationToken.None, true);

            // Assert
            AssertPerson(_person, result);
            // since GetAsync has been called twice, but TryGetPersonByOidAsync has been called once, the second Get uses cache
            await _personApiServiceMock.Received(1).TryGetPersonByOidAsync(_currentUserOid, true, CancellationToken.None);
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

        private void AssertPerson(ProCoSysPerson exptected, ProCoSysPerson actual)
        {
            Assert.AreEqual(exptected.FirstName, actual.FirstName);
            Assert.AreEqual(exptected.LastName, actual.LastName);
        }

        private void AssertAllPersons(IList<ProCoSysPerson> list)
        {
            Assert.IsTrue(list.Count == 2);
            AssertPerson(list.First(), _person1);
            AssertPerson(list.Last(), _person2);
        }
    }
}
