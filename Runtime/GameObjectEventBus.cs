using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UNKO.EventBus
{
    /// <summary>
    /// 게임 오브젝트 범위 이벤트 버스: 컴포넌트로 추가하여 사용
    /// Parent를 GlobalEventBus.Instance로 설정
    /// </summary>
    public class GameObjectEventBus : MonoBehaviour, IUnifiedEventBus
    {
        [SerializeField]
        private EventBusLogic _eventBusLogic = new EventBusLogic(Scope.GameObject);
        [SerializeField]
        private ResponseEventBusLogic _responseEventBusLogic = new ResponseEventBusLogic(Scope.GameObject);

        public Scope Scope => Scope.GameObject;

        void OnDestroy()
        {
            Dispose();
        }

        public int Publish<T>(T evt, bool publishToUpstream = true)
        {
            return _eventBusLogic.Publish(evt, publishToUpstream);
        }

        public Task<int> PublishAsync<T>(T evt, bool publishToUpstream = true)
        {
            return _eventBusLogic.PublishAsync(evt, publishToUpstream);
        }

        public IDisposable Subscribe<T>(Action<T> handler, SubscribeOptions options = default)
        {
            return _eventBusLogic.Subscribe(handler, options);
        }

        public IDisposable Subscribe<T>(Action handler, SubscribeOptions options = default)
        {
            return _eventBusLogic.Subscribe<T>(handler, options);
        }

        public IDisposable Subscribe<T>(Func<T, Task> handler, SubscribeOptions options = default)
        {
            return _eventBusLogic.Subscribe(handler, options);
        }

        public void Dispose()
        {
            _eventBusLogic.Dispose();
            _responseEventBusLogic.Dispose();
        }

        public IReadOnlyList<TRes> Ask<TReq, TRes>(TReq req, bool publishToUpstream = true)
        {
            return _responseEventBusLogic.Ask<TReq, TRes>(req, publishToUpstream);
        }

        public Task<IReadOnlyList<TRes>> AskAsync<TReq, TRes>(TReq req, bool publishToUpstream = true)
        {
            return _responseEventBusLogic.AskAsync<TReq, TRes>(req, publishToUpstream);
        }

        public TOut Aggregate<TReq, TRes, TOut>(TReq req, Func<IReadOnlyList<TRes>, TOut> aggregator, bool publishToUpstream = true)
        {
            return _responseEventBusLogic.Aggregate<TReq, TRes, TOut>(req, aggregator, publishToUpstream);
        }

        public Task<TOut> AggregateAsync<TReq, TRes, TOut>(TReq req, Func<IReadOnlyList<TRes>, TOut> aggregator, bool publishToUpstream = true)
        {
            return _responseEventBusLogic.AggregateAsync<TReq, TRes, TOut>(req, aggregator, publishToUpstream);
        }

        public IDisposable Response<TReq, TRes>(Func<TReq, TRes> handler, SubscribeOptions options = default)
        {
            return _responseEventBusLogic.Response<TReq, TRes>(handler, options);
        }

        public IDisposable Response<TReq, TRes>(Func<TReq, Task<TRes>> handler, SubscribeOptions options = default)
        {
            return _responseEventBusLogic.Response(handler, options);
        }
    }
}