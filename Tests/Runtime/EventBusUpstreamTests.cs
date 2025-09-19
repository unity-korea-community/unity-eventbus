using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UNKO.EventBus;

namespace UNKO.EventBus.Tests
{
    public class EventBusUpstreamTests
    {
        private IEventBus globalEventBus;
        private EventBusLogic childEventBus;

        public class TestUpstreamEvent
        {
            public string Message { get; set; }
            public int Value { get; set; }
            public bool FromChild { get; set; }
        }

        public class LocalOnlyEvent
        {
            public string Content { get; set; }
        }

        [SetUp]
        public void Setup()
        {
            globalEventBus = EventBus.Global;
            childEventBus = new EventBusLogic();
        }

        [TearDown]
        public void TearDown()
        {
            globalEventBus?.Dispose();
            childEventBus?.Dispose();
        }

        [Test]
        public void 이벤트가_자식에서_부모로_전파된다()
        {
            // Arrange
            int childCallCount = 0;
            int parentCallCount = 0;
            TestUpstreamEvent receivedInParent = null;

            childEventBus.Subscribe<TestUpstreamEvent>(evt => { childCallCount++; evt.FromChild = true; });
            globalEventBus.Subscribe<TestUpstreamEvent>(evt => { parentCallCount++; receivedInParent = evt; });

            // Act
            var testEvent = new TestUpstreamEvent { Message = "Child to Parent", Value = 42 };
            int publishedCount = childEventBus.Publish(testEvent);

            // Assert
            Assert.AreEqual(1, childCallCount);
            Assert.AreEqual(1, parentCallCount);
            Assert.AreEqual(2, publishedCount); // 자식 + 부모
            Assert.AreEqual(testEvent, receivedInParent);
            Assert.IsTrue(receivedInParent.FromChild);
        }

        [Test]
        public void publishToUpstream_false일때_부모로_전파되지_않는다()
        {
            // Arrange
            int childCallCount = 0;
            int parentCallCount = 0;

            childEventBus.Subscribe<TestUpstreamEvent>(evt => childCallCount++);
            globalEventBus.Subscribe<TestUpstreamEvent>(evt => parentCallCount++);

            // Act
            int publishedCount = childEventBus.Publish(new TestUpstreamEvent { Message = "Local Only" }, publishToUpstream: false);

            // Assert
            Assert.AreEqual(1, childCallCount);
            Assert.AreEqual(0, parentCallCount);
            Assert.AreEqual(1, publishedCount); // 자식에서만 처리
        }

        [Test]
        public async Task 비동기_이벤트도_업스트림으로_전파된다()
        {
            // Arrange
            int childCallCount = 0;
            int parentCallCount = 0;
            TestUpstreamEvent receivedInParent = null;

            childEventBus.Subscribe<TestUpstreamEvent>(async evt => { await Task.Delay(1); childCallCount++; });
            globalEventBus.Subscribe<TestUpstreamEvent>(async evt => { await Task.Delay(1); parentCallCount++; receivedInParent = evt; });

            // Act
            var testEvent = new TestUpstreamEvent { Message = "Async Child to Parent", Value = 99 };
            int publishedCount = await childEventBus.PublishAsync(testEvent);

            // Assert
            Assert.AreEqual(1, childCallCount);
            Assert.AreEqual(1, parentCallCount);
            Assert.AreEqual(2, publishedCount);
            Assert.AreEqual(testEvent, receivedInParent);
        }

        [Test]
        public async Task 비동기_publishToUpstream_false일때_부모로_전파되지_않는다()
        {
            // Arrange
            int childCallCount = 0;
            int parentCallCount = 0;

            childEventBus.Subscribe<TestUpstreamEvent>(async evt => { await Task.Delay(1); childCallCount++; });
            globalEventBus.Subscribe<TestUpstreamEvent>(async evt => { await Task.Delay(1); parentCallCount++; });

            // Act
            int publishedCount = await childEventBus.PublishAsync(new TestUpstreamEvent { Message = "Async Local Only" }, publishToUpstream: false);

            // Assert
            Assert.AreEqual(1, childCallCount);
            Assert.AreEqual(0, parentCallCount);
            Assert.AreEqual(1, publishedCount);
        }

        [Test]
        public void 업스트림에_핸들러가_없어도_정상_동작한다()
        {
            // Arrange
            int childCallCount = 0;
            childEventBus.Subscribe<TestUpstreamEvent>(evt => childCallCount++);
            // 부모에는 핸들러 등록하지 않음

            // Act
            int publishedCount = childEventBus.Publish(new TestUpstreamEvent { Message = "No parent handler" });

            // Assert
            Assert.AreEqual(1, childCallCount);
            Assert.AreEqual(1, publishedCount); // 자식에서만 처리
        }

        [Test]
        public void 자식에_핸들러가_없어도_부모로_전파된다()
        {
            // Arrange
            int parentCallCount = 0;
            globalEventBus.Subscribe<TestUpstreamEvent>(evt => parentCallCount++);
            // 자식에는 핸들러 등록하지 않음

            // Act
            int publishedCount = childEventBus.Publish(new TestUpstreamEvent { Message = "No child handler" });

            // Assert
            Assert.AreEqual(1, parentCallCount);
            Assert.AreEqual(1, publishedCount); // 부모에서만 처리
        }

        [Test]
        public void GlobalEventBus에서_업스트림_전파시_중복_호출되지_않는다()
        {
            // Arrange
            int callCount = 0;
            globalEventBus.Subscribe<TestUpstreamEvent>(evt => callCount++);

            // Act
            // Global EventBus에서 이벤트를 발행하고, 업스트림 전파를 시도합니다.
            // Global EventBus는 최상위이므로, 업스트림 전파 로직이 중복 호출을 유발해서는 안 됩니다.
            int publishedCount = globalEventBus.Publish(new TestUpstreamEvent { Message = "Global event" }, publishToUpstream: true);

            // Assert
            Assert.AreEqual(1, callCount, "핸들러가 한 번만 호출되어야 합니다.");
            Assert.AreEqual(1, publishedCount, "한 개의 핸들러만 처리되어야 합니다.");
        }
    }
}