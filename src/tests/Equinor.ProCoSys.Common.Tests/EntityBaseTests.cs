using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace Equinor.ProCoSys.Common.Tests
{
    [TestClass]
    public class EntityBaseTests
    {
        private readonly byte[] _convertedRowVersion = { 0, 0, 0, 0, 0, 0, 0, 16 };
        private TestableEntityBase _dut;
        private IDomainEvent _domainEvent;
        private IPostSaveDomainEvent _postSaveEventMock;
        private const string RowVersion = "AAAAAAAAABA=";

        [TestInitialize]
        public void SetUp()
        {
            // Arrange
            _dut = new TestableEntityBase();
            _domainEvent = new TestableDomainEvent();
            _postSaveEventMock = Substitute.For<IPostSaveDomainEvent>();
        }

        [TestMethod]
        public void DomainEventsLists_Are_Empty_After_Construct()
        {
            Assert.IsNotNull(_dut.DomainEvents);
            Assert.IsNotNull(_dut.PostSaveDomainEvents);
            Assert.AreEqual(0, _dut.DomainEvents.Count);
            Assert.AreEqual(0, _dut.PostSaveDomainEvents.Count);
        }

        [TestMethod]
        public void AddDomainEvent_Should_AddToDomainEvents()
        {
            // Act
            _dut.AddDomainEvent(_domainEvent);

            Assert.IsTrue(_dut.DomainEvents.Contains(_domainEvent));
            Assert.AreEqual(0, _dut.PostSaveDomainEvents.Count);
        }

        [TestMethod]
        public void AddPostSaveDomainEvent_Should_AddToPostSaveDomainEvents()
        {
            // Act
            _dut.AddPostSaveDomainEvent(_postSaveEventMock);

            // Assert
            Assert.IsTrue(_dut.PostSaveDomainEvents.Contains(_postSaveEventMock));
            Assert.AreEqual(0, _dut.DomainEvents.Count);
        }

        [TestMethod]
        public void RemoveDomainEvent_Should_RemoveFromDomainEvents()
        {
            // Arrange
            _dut.AddDomainEvent(_domainEvent);

            // Act
            _dut.RemoveDomainEvent(_domainEvent);

            // Assert
            Assert.AreEqual(0, _dut.DomainEvents.Count);
            Assert.AreEqual(0, _dut.PostSaveDomainEvents.Count);
        }

        [TestMethod]
        public void RemovePostSaveDomainEvent_Should_RemoveFromPostSaveDomainEvents()
        {
            // Arrange
            _dut.AddPostSaveDomainEvent(_postSaveEventMock);

            // Act
            _dut.RemovePostSaveDomainEvent(_postSaveEventMock);

            // Assert
            Assert.AreEqual(0, _dut.DomainEvents.Count);
            Assert.AreEqual(0, _dut.PostSaveDomainEvents.Count);
        }

        [TestMethod]
        public void ClearDomainEvents_Should_ClearDomainEvents()
        {
            // Arrange
            var domainMock1 = new TestableDomainEvent();
            _dut.AddDomainEvent(domainMock1);
            var postSaveEventMock1 = Substitute.For<IPostSaveDomainEvent>();
            _dut.AddPostSaveDomainEvent(postSaveEventMock1);

            var domainMock2 = new TestableDomainEvent();
            _dut.AddDomainEvent(domainMock2);
            var postSaveEventMock2 = Substitute.For<IPostSaveDomainEvent>();
            _dut.AddPostSaveDomainEvent(postSaveEventMock2);

            // Act
            _dut.ClearDomainEvents();

            // Assert
            Assert.AreEqual(0, _dut.DomainEvents.Count);
            Assert.AreEqual(2, _dut.PostSaveDomainEvents.Count);
        }

        [TestMethod]
        public void ClearPostSaveDomainEvents_Should_ClearPostSaveDomainEvents()
        {
            // Arrange
            var domainMock1 = new TestableDomainEvent();
            _dut.AddDomainEvent(domainMock1);
            var postSaveEventMock1 = Substitute.For<IPostSaveDomainEvent>();
            _dut.AddPostSaveDomainEvent(postSaveEventMock1);

            var domainMock2 = new TestableDomainEvent();
            _dut.AddDomainEvent(domainMock2);
            var postSaveEventMock2 = Substitute.For<IPostSaveDomainEvent>();
            _dut.AddPostSaveDomainEvent(postSaveEventMock2);

            // Act
            _dut.ClearPostSaveDomainEvents();

            // Assert
            Assert.AreEqual(2, _dut.DomainEvents.Count);
            Assert.AreEqual(0, _dut.PostSaveDomainEvents.Count);
        }

        [TestMethod]
        public void GetRowVersion_Should_ReturnLastSetRowVersion()
        {
            Assert.IsNotNull(_dut.RowVersion);
            _dut.SetRowVersion(RowVersion);
            Assert.IsTrue(_dut.RowVersion.SequenceEqual(_convertedRowVersion));
        }

        private class TestableEntityBase : EntityBase
        {
            // The base class is abstract, therefor a sub class is needed to test it.
        }
    }

    internal class TestableDomainEvent : IDomainEvent
    {
    }
}
