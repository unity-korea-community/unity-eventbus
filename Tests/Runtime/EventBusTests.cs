using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UNKO.EventBus;

namespace UNKO.EventBus.Tests
{
    public class EventBusTests
    {
        private EventBusLogic eventBus;

        public class TestEvent
        {
            public string Message { get; set; }
            public int Value { get; set; }
        }

        public class AnotherTestEvent
        {
            public float Number { get; set; }
        }

        [SetUp]
        public void Setup()
        {
            eventBus = new EventBusLogic();
        }

        [TearDown]
        public void TearDown()
        {
            eventBus?.Dispose();
        }

        [Test]
        public void Action타입_핸들러_구독_및_해제가_동작한다()
        {
            // Arrange
            int callCount = 0;
            Action handler = () => callCount++;
            eventBus.Subscribe<TestEvent>(handler);

            // Act
            eventBus.Publish(new TestEvent());
            Assert.AreEqual(1, callCount);
            eventBus.UnSubscribe<TestEvent>(handler);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(1, callCount); // 해제 후 호출되지 않음
        }

        [Test]
        public void Action매개변수타입_핸들러_구독_및_해제가_동작한다()
        {
            // Arrange
            int callCount = 0;
            TestEvent receivedEvent = null;
            Action<TestEvent> handler = evt => { callCount++; receivedEvent = evt; };
            eventBus.Subscribe<TestEvent>(handler);

            // Act
            var testEvent = new TestEvent { Message = "Test", Value = 42 };
            eventBus.Publish(testEvent);
            Assert.AreEqual(1, callCount);
            Assert.AreEqual(testEvent, receivedEvent);
            eventBus.UnSubscribe<TestEvent>(handler);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(1, callCount); // 해제 후 호출되지 않음
        }

        [Test]
        public async Task Func비동기타입_핸들러_구독_및_해제가_동작한다()
        {
            // Arrange
            int callCount = 0;
            TestEvent receivedEvent = null;
            Func<TestEvent, Task> handler = async evt =>
            {
                await Task.Delay(1);
                callCount++;
                receivedEvent = evt;
            };
            eventBus.Subscribe<TestEvent>(handler);

            // Act
            var testEvent = new TestEvent { Message = "AsyncTest", Value = 99 };
            await eventBus.PublishAsync(testEvent);
            Assert.AreEqual(1, callCount);
            Assert.AreEqual(testEvent, receivedEvent);
            eventBus.UnSubscribe<TestEvent>(handler);
            await eventBus.PublishAsync(new TestEvent());

            // Assert
            Assert.AreEqual(1, callCount); // 해제 후 호출되지 않음
        }

        [Test]
        public async Task 혼합된_핸들러_타입에서_특정_타입만_해제된다()
        {
            // Arrange
            int actionCallCount = 0;
            int actionTCallCount = 0;
            int funcCallCount = 0;

            Action actionHandler = () => actionCallCount++;
            Action<TestEvent> actionTHandler = evt => actionTCallCount++;
            Func<TestEvent, Task> funcHandler = async evt => { await Task.Delay(1); funcCallCount++; };

            eventBus.Subscribe<TestEvent>(actionHandler);
            eventBus.Subscribe<TestEvent>(actionTHandler);
            eventBus.Subscribe<TestEvent>(funcHandler);

            // Act - 모든 핸들러 실행 확인
            await eventBus.PublishAsync(new TestEvent());
            Assert.AreEqual(1, actionCallCount);
            Assert.AreEqual(1, actionTCallCount);
            Assert.AreEqual(1, funcCallCount);

            // Act - Action 타입만 해제
            eventBus.UnSubscribe<TestEvent>(actionHandler);
            await eventBus.PublishAsync(new TestEvent());

            // Assert - Action은 호출되지 않고 나머지는 호출됨
            Assert.AreEqual(1, actionCallCount); // 증가하지 않음
            Assert.AreEqual(2, actionTCallCount); // 증가함
            Assert.AreEqual(2, funcCallCount); // 증가함
        }

        [Test]
        public void 이벤트_구독_및_발행이_정상_동작한다()
        {
            // Arrange
            bool eventReceived = false;
            TestEvent receivedEvent = null;
            eventBus.Subscribe<TestEvent>(evt =>
            {
                eventReceived = true;
                receivedEvent = evt;
            });
            var testEvent = new TestEvent { Message = "Test", Value = 42 };

            // Act
            int publishCount = eventBus.Publish(testEvent);

            // Assert
            Assert.IsTrue(eventReceived);
            Assert.AreEqual(testEvent, receivedEvent);
            Assert.AreEqual(1, publishCount);
        }

        [Test]
        public void 매개변수_없는_핸들러가_정상_동작한다()
        {
            // Arrange
            bool eventReceived = false;
            eventBus.Subscribe<TestEvent>(() =>
            {
                eventReceived = true;
            });
            var testEvent = new TestEvent { Message = "Test", Value = 42 };

            // Act
            int publishCount = eventBus.Publish(testEvent);

            // Assert
            Assert.IsTrue(eventReceived);
            Assert.AreEqual(1, publishCount);
        }

        [Test]
        public void 여러_구독자가_모두_이벤트를_받는다()
        {
            // Arrange
            int callCount = 0;
            string[] receivedMessages = new string[3];
            eventBus.Subscribe<TestEvent>(evt => { receivedMessages[0] = evt.Message; callCount++; });
            eventBus.Subscribe<TestEvent>(evt => { receivedMessages[1] = evt.Message; callCount++; });
            eventBus.Subscribe<TestEvent>(evt => { receivedMessages[2] = evt.Message; callCount++; });
            var testEvent = new TestEvent { Message = "Broadcast", Value = 123 };

            // Act
            int publishCount = eventBus.Publish(testEvent);

            // Assert
            Assert.AreEqual(3, callCount);
            Assert.AreEqual(3, publishCount);
            Assert.AreEqual("Broadcast", receivedMessages[0]);
            Assert.AreEqual("Broadcast", receivedMessages[1]);
            Assert.AreEqual("Broadcast", receivedMessages[2]);
        }

        [Test]
        public void 우선순위가_높은_순서로_실행된다()
        {
            // Arrange
            var executionOrder = new System.Collections.Generic.List<int>();
            eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(1), new SubscribeOptions(false, 1));
            eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(3), new SubscribeOptions(false, 3));
            eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(2), new SubscribeOptions(false, 2));
            eventBus.Subscribe<TestEvent>(evt => executionOrder.Add(0), new SubscribeOptions(false, 0));

            // Act
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(4, executionOrder.Count);
            Assert.AreEqual(3, executionOrder[0]);
            Assert.AreEqual(2, executionOrder[1]);
            Assert.AreEqual(1, executionOrder[2]);
            Assert.AreEqual(0, executionOrder[3]);
        }

        [Test]
        public void 구독_해제가_핸들러를_제거한다()
        {
            // Arrange
            bool eventReceived = false;
            Action<TestEvent> handler = evt => eventReceived = true;
            eventBus.Subscribe<TestEvent>(handler);

            // Act
            eventBus.Publish(new TestEvent());
            Assert.IsTrue(eventReceived);
            eventReceived = false;
            eventBus.UnSubscribe<TestEvent>(handler);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.IsFalse(eventReceived);
        }

        [Test]
        public void IDisposable을_통한_구독_해제가_동작한다()
        {
            // Arrange
            bool eventReceived = false;
            var subscription = eventBus.Subscribe<TestEvent>(evt => eventReceived = true);

            // Act
            eventBus.Publish(new TestEvent());
            Assert.IsTrue(eventReceived);
            eventReceived = false;
            subscription.Dispose();
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.IsFalse(eventReceived);
        }

        [Test]
        public void DebugObject로_모든_핸들러를_일괄_해제한다()
        {
            // Arrange
            var testObject = new GameObject("TestObject");
            int callCount = 0;
            eventBus.Subscribe<TestEvent>(evt => callCount++, debugObject: testObject);
            eventBus.Subscribe<AnotherTestEvent>(evt => callCount++, debugObject: testObject);

            // Act
            eventBus.Publish(new TestEvent());
            eventBus.Publish(new AnotherTestEvent());
            Assert.AreEqual(2, callCount);
            callCount = 0;
            eventBus.UnSubscribe(testObject);
            eventBus.Publish(new TestEvent());
            eventBus.Publish(new AnotherTestEvent());

            // Assert
            Assert.AreEqual(0, callCount);
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public void 다른_이벤트_타입들이_서로_격리된다()
        {
            // Arrange
            bool testEventReceived = false;
            bool anotherEventReceived = false;
            eventBus.Subscribe<TestEvent>(evt => testEventReceived = true);
            eventBus.Subscribe<AnotherTestEvent>(evt => anotherEventReceived = true);

            // Act
            eventBus.Publish(new TestEvent());
            Assert.IsTrue(testEventReceived);
            Assert.IsFalse(anotherEventReceived);
            testEventReceived = false;
            eventBus.Publish(new AnotherTestEvent());

            // Assert
            Assert.IsFalse(testEventReceived);
            Assert.IsTrue(anotherEventReceived);
        }

        [Test]
        public void 구독자가_없으면_0을_반환한다()
        {
            // Arrange

            // Act
            int publishCount = eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(0, publishCount);
        }

        [Test]
        public async Task 비동기_이벤트_발행이_정상_동작한다()
        {
            // Arrange
            bool eventReceived = false;
            TestEvent receivedEvent = null;
            eventBus.Subscribe<TestEvent>(async evt =>
            {
                await Task.Delay(1);
                eventReceived = true;
                receivedEvent = evt;
            });
            var testEvent = new TestEvent { Message = "AsyncTest", Value = 99 };

            // Act
            int publishCount = await eventBus.PublishAsync(testEvent);

            // Assert
            Assert.IsTrue(eventReceived);
            Assert.AreEqual(testEvent, receivedEvent);
            Assert.AreEqual(1, publishCount);
        }

        [Test]
        public async Task 동기_비동기_핸들러가_함께_동작한다()
        {
            // Arrange
            int syncCallCount = 0;
            int asyncCallCount = 0;
            eventBus.Subscribe<TestEvent>(evt => syncCallCount++);
            eventBus.Subscribe<TestEvent>(async evt =>
            {
                await Task.Delay(1);
                asyncCallCount++;
            });
            eventBus.Subscribe<TestEvent>(() => syncCallCount++);

            // Act
            int publishCount = await eventBus.PublishAsync(new TestEvent());

            // Assert
            Assert.AreEqual(2, syncCallCount);
            Assert.AreEqual(1, asyncCallCount);
            Assert.AreEqual(3, publishCount);
        }

        // [Test]
        // public void 핸들러_예외가_포착되고_로그에_기록된다()
        // {
        //     // Arrange
        //     bool otherHandlerCalled = false;
        //     eventBus.Subscribe<TestEvent>((System.Action<TestEvent>)(evt => throw new InvalidOperationException("Test exception")));
        //     eventBus.Subscribe<TestEvent>(evt => otherHandlerCalled = true);

        //     LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Test exception"));

        //     // Act
        //     eventBus.Publish(new TestEvent());

        //     // Assert
        //     Assert.IsTrue(otherHandlerCalled); // 다른 핸들러는 정상 실행되어야 함
        //     LogAssert.NoUnexpectedReceived();
        // }

        [Test]
        public void 전역_EventBus가_정상_동작한다()
        {
            // Arrange
            bool eventReceived = false;
            EventBus.Global.Subscribe<TestEvent>(evt => eventReceived = true);

            // Act
            EventBus.Global.Publish(new TestEvent());

            // Assert
            Assert.IsTrue(eventReceived);
            EventBus.Global.Dispose();
        }

        [Test]
        public void Dispose가_모든_핸들러를_정리한다()
        {
            // Arrange
            bool eventReceived = false;
            eventBus.Subscribe<TestEvent>(evt => eventReceived = true);
            eventBus.Subscribe<AnotherTestEvent>(evt => eventReceived = true);

            // Act
            eventBus.Dispose();
            eventBus.Publish(new TestEvent());
            eventBus.Publish(new AnotherTestEvent());

            // Assert
            Assert.IsFalse(eventReceived);
        }

        [Test]
        public void 같은_핸들러를_여러번_구독하면_여러번_호출된다()
        {
            // Arrange
            int callCount = 0;
            Action<TestEvent> handler = evt => callCount++;
            eventBus.Subscribe<TestEvent>(handler);
            eventBus.Subscribe<TestEvent>(handler);
            eventBus.Subscribe<TestEvent>(handler);

            // Act
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(3, callCount);
        }

        [Test]
        public void 구독_해제가_일치하는_핸들러만_제거한다()
        {
            // Arrange
            int callCount = 0;
            Action<TestEvent> handler1 = evt => callCount++;
            Action<TestEvent> handler2 = evt => callCount++;
            eventBus.Subscribe<TestEvent>(handler1);
            eventBus.Subscribe<TestEvent>(handler2);

            // Act
            eventBus.Publish(new TestEvent());
            Assert.AreEqual(2, callCount);
            callCount = 0;
            eventBus.UnSubscribe<TestEvent>(handler1);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.AreEqual(1, callCount);
        }
    }
}