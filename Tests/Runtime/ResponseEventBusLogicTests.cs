using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UNKO.EventBus.Tests
{
    /// <summary>
    /// 테스트용 이벤트들
    /// </summary>
    public class TestEvent
    {
        public string Message { get; set; }
        public int Value { get; set; }
    }

    public class TestRequest
    {
        public string Query { get; set; }
        public int Amount { get; set; }
    }

    public class TestResponse
    {
        public bool Success { get; set; }
        public string Result { get; set; }
        public int ProcessedAmount { get; set; }
    }

    public class HealthRequest
    {
        public float Amount { get; set; }
        public string PlayerId { get; set; }
    }

    public class HealthResponse
    {
        public bool Success { get; set; }
        public float ActualAmount { get; set; }
        public string Source { get; set; }
    }

    /// <summary>
    /// ResponseEventBusLogic 단위 테스트
    /// </summary>
    public class ResponseEventBusLogicTests
    {
        private ResponseEventBusLogic _responseBus;

        [SetUp]
        public void SetUp()
        {
            _responseBus = new ResponseEventBusLogic(Scope.Pure);
        }

        [TearDown]
        public void TearDown()
        {
            _responseBus?.Dispose();
        }

        [Test]
        public void Subscribe_And_Ask_ShouldReturnResponses()
        {
            // Arrange
            _responseBus.Response<TestRequest, TestResponse>(req => new TestResponse
            {
                Success = true,
                Result = $"Processed: {req.Query}",
                ProcessedAmount = req.Amount * 2
            });

            var request = new TestRequest { Query = "test", Amount = 10 };

            // Act
            var responses = _responseBus.Ask<TestRequest, TestResponse>(request);

            // Assert
            Assert.AreEqual(1, responses.Count);
            Assert.IsTrue(responses[0].Success);
            Assert.AreEqual("Processed: test", responses[0].Result);
            Assert.AreEqual(20, responses[0].ProcessedAmount);
        }

        [Test]
        public void MultipleHandlers_ShouldReturnMultipleResponses()
        {
            // Arrange
            _responseBus.Response<HealthRequest, HealthResponse>(req => new HealthResponse
            {
                Success = true,
                ActualAmount = req.Amount,
                Source = "Potion"
            });

            _responseBus.Response<HealthRequest, HealthResponse>(req => new HealthResponse
            {
                Success = true,
                ActualAmount = req.Amount * 0.5f,
                Source = "Regeneration"
            });

            var request = new HealthRequest { Amount = 100f, PlayerId = "Player1" };

            // Act
            var responses = _responseBus.Ask<HealthRequest, HealthResponse>(request);

            // Assert
            Assert.AreEqual(2, responses.Count);
            Assert.IsTrue(responses.All(r => r.Success));
            Assert.AreEqual(100f, responses[0].ActualAmount);
            Assert.AreEqual(50f, responses[1].ActualAmount);
        }

        [Test]
        public void Aggregate_ShouldCombineResponses()
        {
            // Arrange
            _responseBus.Response<HealthRequest, HealthResponse>(req => new HealthResponse
            {
                Success = true,
                ActualAmount = 30f,
                Source = "Potion"
            });

            _responseBus.Response<HealthRequest, HealthResponse>(req => new HealthResponse
            {
                Success = true,
                ActualAmount = 20f,
                Source = "Food"
            });

            var request = new HealthRequest { Amount = 50f, PlayerId = "Player1" };

            // Act - TOut을 float로 사용하여 총합 계산
            var totalHealing = _responseBus.Aggregate<HealthRequest, HealthResponse, float>(
                request,
                responses => responses.Where(r => r.Success).Sum(r => r.ActualAmount)
            );

            // Assert
            Assert.AreEqual(50f, totalHealing);
        }

        [Test]
        public void Aggregate_DifferentOutputType_ShouldWork()
        {
            // Arrange
            _responseBus.Response<HealthRequest, HealthResponse>(req => new HealthResponse
            {
                Success = true,
                ActualAmount = 30f,
                Source = "Potion"
            });

            _responseBus.Response<HealthRequest, HealthResponse>(req => new HealthResponse
            {
                Success = false,
                ActualAmount = 0f,
                Source = "EmptyBottle"
            });

            _responseBus.Response<HealthRequest, HealthResponse>(req => new HealthResponse
            {
                Success = true,
                ActualAmount = 20f,
                Source = "Food"
            });

            var request = new HealthRequest { Amount = 50f, PlayerId = "Player1" };

            // Act - TOut을 int로 사용하여 성공한 핸들러 개수 계산
            var successCount = _responseBus.Aggregate<HealthRequest, HealthResponse, int>(
                request,
                responses => responses.Count(r => r.Success)
            );

            // Act - TOut을 string으로 사용하여 소스 리스트 생성
            var sourceSummary = _responseBus.Aggregate<HealthRequest, HealthResponse, string>(
                request,
                responses => string.Join(", ", responses.Where(r => r.Success).Select(r => r.Source))
            );

            // Assert
            Assert.AreEqual(2, successCount);
            Assert.AreEqual("Potion, Food", sourceSummary);
        }

        [Test]
        public void Priority_ShouldOrderHandlers()
        {
            // Arrange
            var results = new List<string>();

            _responseBus.Response<TestRequest, TestResponse>(req =>
            {
                results.Add("Low");
                return new TestResponse { Success = true, Result = "Low" };
            }, new SubscribeOptions(false, 1));

            _responseBus.Response<TestRequest, TestResponse>(req =>
            {
                results.Add("High");
                return new TestResponse { Success = true, Result = "High" };
            }, new SubscribeOptions(false, 10));

            _responseBus.Response<TestRequest, TestResponse>(req =>
            {
                results.Add("Medium");
                return new TestResponse { Success = true, Result = "Medium" };
            }, new SubscribeOptions(false, 5));

            // Act
            var responses = _responseBus.Ask<TestRequest, TestResponse>(new TestRequest());

            // Assert
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("High", results[0]);
            Assert.AreEqual("Medium", results[1]);
            Assert.AreEqual("Low", results[2]);
        }

        [Test]
        public void Unsubscribe_ShouldRemoveHandler()
        {
            // Arrange
            var subscription = _responseBus.Response<TestRequest, TestResponse>(req => new TestResponse
            {
                Success = true,
                Result = "Test"
            });

            // 첫 번째 테스트
            var responses1 = _responseBus.Ask<TestRequest, TestResponse>(new TestRequest());
            Assert.AreEqual(1, responses1.Count);

            // Act
            subscription.Dispose();

            // Assert
            var responses2 = _responseBus.Ask<TestRequest, TestResponse>(new TestRequest());
            Assert.AreEqual(0, responses2.Count);
        }
    }

    /// <summary>
    /// CustomGlobalEventBus 통합 테스트
    /// </summary>
    public class GlobalEventBusTests
    {
        private GameObject _testGameObject;
        private GlobalEventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestEventBus");
            _eventBus = _testGameObject.AddComponent<GlobalEventBus>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_testGameObject);
            }
        }

        [Test]
        public void EventBus_ShouldHandleRegularEvents()
        {
            // Arrange
            TestEvent receivedEvent = null;
            _eventBus.Subscribe<TestEvent>(evt => receivedEvent = evt);

            var testEvent = new TestEvent { Message = "Hello", Value = 42 };

            // Act
            var handlerCount = _eventBus.Publish(testEvent);

            // Assert
            Assert.AreEqual(1, handlerCount);
            Assert.IsNotNull(receivedEvent);
            Assert.AreEqual("Hello", receivedEvent.Message);
            Assert.AreEqual(42, receivedEvent.Value);
        }

        [Test]
        public void EventBus_ShouldHandleResponseEvents()
        {
            // Arrange
            _eventBus.Response<TestRequest, TestResponse>(req => new TestResponse
            {
                Success = true,
                Result = $"Handled: {req.Query}",
                ProcessedAmount = req.Amount
            });

            var request = new TestRequest { Query = "test request", Amount = 100 };

            // Act
            var responses = _eventBus.Ask<TestRequest, TestResponse>(request);

            // Assert
            Assert.AreEqual(1, responses.Count);
            Assert.IsTrue(responses[0].Success);
            Assert.AreEqual("Handled: test request", responses[0].Result);
            Assert.AreEqual(100, responses[0].ProcessedAmount);
        }

        [Test]
        public void EventBus_ShouldHandleBothEventTypes()
        {
            // Arrange
            var eventReceived = false;
            var responseReceived = false;

            _eventBus.Subscribe<TestEvent>(evt => eventReceived = true);
            _eventBus.Response<TestRequest, TestResponse>(req =>
            {
                responseReceived = true;
                return new TestResponse { Success = true };
            });

            // Act
            _eventBus.Publish(new TestEvent { Message = "test" });
            var responses = _eventBus.Ask<TestRequest, TestResponse>(new TestRequest { Query = "test" });

            // Assert
            Assert.IsTrue(eventReceived);
            Assert.IsTrue(responseReceived);
            Assert.AreEqual(1, responses.Count);
        }
    }

    /// <summary>
    /// 비동기 테스트
    /// </summary>
    public class AsyncEventBusTests
    {
        private GameObject _testGameObject;
        private GameObjectEventBus _eventBus;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestEventBus");
            _eventBus = _testGameObject.AddComponent<GameObjectEventBus>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_testGameObject);
            }
        }

        [UnityTest]
        public IEnumerator AskAsync_ShouldHandleAsyncHandlers()
        {
            // Arrange
            _eventBus.Response<TestRequest, TestResponse>(async req =>
            {
                await Task.Delay(10);
                return new TestResponse
                {
                    Success = true,
                    Result = "Async result",
                    ProcessedAmount = req.Amount * 2
                };
            });

            var request = new TestRequest { Query = "async test", Amount = 50 };

            // Act
            var task = _eventBus.AskAsync<TestRequest, TestResponse>(request);
            
            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            var responses = task.Result;
            Assert.AreEqual(1, responses.Count);
            Assert.IsTrue(responses[0].Success);
            Assert.AreEqual("Async result", responses[0].Result);
            Assert.AreEqual(100, responses[0].ProcessedAmount);
        }

        [UnityTest]
        public IEnumerator AggregateAsync_ShouldCombineAsyncResponses()
        {
            // Arrange
            _eventBus.Response<HealthRequest, HealthResponse>(async req =>
            {
                await Task.Delay(5);
                return new HealthResponse { Success = true, ActualAmount = 30f, Source = "Async1" };
            });

            _eventBus.Response<HealthRequest, HealthResponse>(async req =>
            {
                await Task.Delay(10);
                return new HealthResponse { Success = true, ActualAmount = 20f, Source = "Async2" };
            });

            var request = new HealthRequest { Amount = 50f, PlayerId = "AsyncPlayer" };

            // Act - TOut을 float로 사용
            var task = _eventBus.AggregateAsync<HealthRequest, HealthResponse, float>(
                request,
                responses => responses.Sum(r => r.ActualAmount)
            );

            yield return new WaitUntil(() => task.IsCompleted);

            // Assert
            Assert.AreEqual(50f, task.Result);
        }
    }

    /// <summary>
    /// CustomGameObjectEventBus 테스트
    /// </summary>
    public class GameObjectEventBusTests
    {
        private GameObject _testGameObject;
        private GameObjectEventBus _gameObjectEventBus;

        [SetUp]
        public void SetUp()
        {
            _testGameObject = new GameObject("TestGameObjectEventBus");
            _gameObjectEventBus = _testGameObject.AddComponent<GameObjectEventBus>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_testGameObject);
            }
        }

        [Test]
        public void GameObjectEventBus_ShouldHaveCorrectScope()
        {
            // Assert
            Assert.AreEqual(Scope.GameObject, _gameObjectEventBus.Scope);
        }

        [Test]
        public void GameObjectEventBus_ShouldHandleBothEventTypes()
        {
            // Arrange
            var eventReceived = false;
            var responseCount = 0;

            _gameObjectEventBus.Subscribe<TestEvent>(evt => eventReceived = true);
            _gameObjectEventBus.Response<TestRequest, TestResponse>(req =>
            {
                responseCount++;
                return new TestResponse { Success = true };
            });

            // Act
            _gameObjectEventBus.Publish(new TestEvent());
            var responses = _gameObjectEventBus.Ask<TestRequest, TestResponse>(new TestRequest());

            // Assert
            Assert.IsTrue(eventReceived);
            Assert.AreEqual(1, responseCount);
            Assert.AreEqual(1, responses.Count);
        }

        [Test]
        public void Dispose_ShouldCleanupBothLogics()
        {
            // Arrange
            _gameObjectEventBus.Subscribe<TestEvent>(evt => { });
            _gameObjectEventBus.Response<TestRequest, TestResponse>(req => new TestResponse());

            // Act
            _gameObjectEventBus.Dispose();

            // Assert
            var eventCount = _gameObjectEventBus.Publish(new TestEvent());
            var responseCount = _gameObjectEventBus.Ask<TestRequest, TestResponse>(new TestRequest()).Count;

            Assert.AreEqual(0, eventCount);
            Assert.AreEqual(0, responseCount);
        }
    }
}