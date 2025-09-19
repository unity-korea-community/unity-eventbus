using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace UNKO.EventBus.Tests
{
    public class EventBusInterfaceTests
    {
        public interface ITestEvent { }
        public class TestEvent : ITestEvent { }

        [Test]
        public void Subscribe_Interface_ShouldReceiveEvent()
        {
            // Arrange
            var eventBus = new EventBusLogic();
            bool wasCalled = false;
            Action<ITestEvent> listener = (evt) => wasCalled = true;

            // Act
            eventBus.Subscribe<ITestEvent>(listener);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.IsTrue(wasCalled);
        }

        [Test]
        public async Task Subscribe_Interface_ShouldReceiveEvent_Async()
        {
            // Arrange
            var eventBus = new EventBusLogic();
            bool wasCalled = false;
            Func<ITestEvent, Task> listener = (evt) =>
            {
                wasCalled = true;
                return Task.CompletedTask;
            };

            // Act
            eventBus.Subscribe<ITestEvent>(listener);
            await eventBus.PublishAsync(new TestEvent());

            // Assert
            Assert.IsTrue(wasCalled);
        }

        [Test]
        public void Unsubscribe_Interface_ShouldNotReceiveEvent()
        {
            // Arrange
            var eventBus = new EventBusLogic();
            bool wasCalled = false;
            Action<ITestEvent> listener = (evt) => wasCalled = true;

            // Act
            var unsubscriber = eventBus.Subscribe<ITestEvent>(listener);
            unsubscriber.Dispose();
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.IsFalse(wasCalled);
        }
    }
}
