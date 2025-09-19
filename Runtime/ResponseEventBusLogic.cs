using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace UNKO.EventBus
{
    /// <summary>
    /// ResponseEventBus 구현체 - EventBusLogic과 유사한 구조
    /// </summary>
    [System.Serializable]
    public partial class ResponseEventBusLogic : IResponseEventBus
    {
        [System.Serializable]
        public class ResponseCallBackInfo
        {
            public string CallBackName;
            public Delegate Callback;
            public UnityEngine.Object DebugObject;
            public int Priority;

            public ResponseCallBackInfo(Delegate callback, int priority = 0, UnityEngine.Object debugObject = null)
            {
                CallBackName = $"{callback.Target}.{callback.Method.Name}";
                Callback = callback;
                DebugObject = debugObject;
                Priority = priority;
            }
        }

        public Scope Scope { get; private set; }

        // Request-Response 타입 쌍별 핸들러 리스트 저장
        private readonly Dictionary<string, ResponseHandler> _responseHandlerByKey = new Dictionary<string, ResponseHandler>();

        [SerializeField] // Unity 에디터에서 이 필드를 노출하기 위해 사용
        List<ResponseHandler> _allResponseHandlers = new List<ResponseHandler>();

        public ResponseEventBusLogic(Scope scope = Scope.Pure)
        {
            Scope = scope;
        }

        public IReadOnlyList<TRes> Ask<TReq, TRes>(TReq req, bool publishToUpstream = true)
        {
            var key = GetResponseKey<TReq, TRes>();
            var results = new List<TRes>();

            if (_responseHandlerByKey.TryGetValue(key, out var handler))
            {
                var handlerResults = handler.InvokeResponse<TReq, TRes>(req);
                results.AddRange(handlerResults);
            }

            if (publishToUpstream && Scope != Scope.Global && EventBus.Global != null)
            {
                var upstreamResults = EventBus.Global.Ask<TReq, TRes>(req, false);
                results.AddRange(upstreamResults);
            }

            return results;
        }

        public async Task<IReadOnlyList<TRes>> AskAsync<TReq, TRes>(TReq req, bool publishToUpstream = true)
        {
            var key = GetResponseKey<TReq, TRes>();
            var results = new List<TRes>();

            if (_responseHandlerByKey.TryGetValue(key, out var handler))
            {
                var handlerResults = await handler.InvokeResponseAsync<TReq, TRes>(req);
                results.AddRange(handlerResults);
            }

            if (publishToUpstream && Scope != Scope.Global)
            {
                var upstreamResults = await EventBus.Global.AskAsync<TReq, TRes>(req, false);
                results.AddRange(upstreamResults);
            }

            return results;
        }

        public TOut Aggregate<TReq, TRes, TOut>(TReq req, Func<IReadOnlyList<TRes>, TOut> aggregator, bool publishToUpstream = true)
        {
            var responses = Ask<TReq, TRes>(req, publishToUpstream);
            return aggregator(responses);
        }

        public async Task<TOut> AggregateAsync<TReq, TRes, TOut>(TReq req, Func<IReadOnlyList<TRes>, TOut> aggregator, bool publishToUpstream = true)
        {
            var responses = await AskAsync<TReq, TRes>(req, publishToUpstream);
            return aggregator(responses);
        }

        public IDisposable Response<TReq, TRes>(Func<TReq, TRes> handler, SubscribeOptions options = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var key = GetResponseKey<TReq, TRes>();
            if (!_responseHandlerByKey.TryGetValue(key, out var responseHandler))
            {
                responseHandler = new ResponseHandler(typeof(TReq), typeof(TRes));
                _responseHandlerByKey[key] = responseHandler;
                _allResponseHandlers.Add(responseHandler);
            }
            responseHandler.Add(handler, options.Priority, options.DebugObject);

            return new ResponseUnsubscribe(() => UnSubscribe<TReq, TRes>(handler));
        }

        public IDisposable Response<TReq, TRes>(Func<TReq, Task<TRes>> handler, SubscribeOptions options = default)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var key = GetResponseKey<TReq, TRes>();
            if (!_responseHandlerByKey.TryGetValue(key, out var responseHandler))
            {
                responseHandler = new ResponseHandler(typeof(TReq), typeof(TRes));
                _responseHandlerByKey[key] = responseHandler;
                _allResponseHandlers.Add(responseHandler);
            }
            responseHandler.Add(handler, options.Priority, options.DebugObject);

            return new ResponseUnsubscribe(() => UnSubscribe<TReq, TRes>(handler));
        }

        public void UnSubscribe<TReq, TRes>(Func<TReq, TRes> handler)
        {
            _UnSubscribe<TReq, TRes>(handler);
        }

        public void UnSubscribe<TReq, TRes>(Func<TReq, Task<TRes>> handler)
        {
            _UnSubscribe<TReq, TRes>(handler);
        }

        private void _UnSubscribe<TReq, TRes>(Delegate callback)
        {
            var key = GetResponseKey<TReq, TRes>();
            if (_responseHandlerByKey.TryGetValue(key, out var handler))
            {
                handler.Remove(callback);
                if (handler.CallBackInfos.Count == 0)
                {
                    _responseHandlerByKey.Remove(key);
                    _allResponseHandlers.RemoveAll(h => h.GetKey() == key);
                }
            }
        }

        List<ResponseHandler> _handlersToRemove = new List<ResponseHandler>();
        public void UnSubscribe(UnityEngine.Object debugObject)
        {
            _handlersToRemove.Clear();
            foreach (var handler in _allResponseHandlers)
            {
                handler.Remove(debugObject);
                if (handler.CallBackInfos.Count == 0)
                {
                    _handlersToRemove.Add(handler);
                }
            }

            foreach (var handler in _handlersToRemove)
            {
                _responseHandlerByKey.Remove(handler.GetKey());
                handler.CallBackInfos.Clear();
                _allResponseHandlers.Remove(handler);
            }
        }

        void UnSubscribe(Delegate callback)
        {
            _handlersToRemove.Clear();

            foreach (var handler in _allResponseHandlers)
            {
                handler.Remove(callback);
                if (handler.CallBackInfos.Count == 0)
                {
                    _handlersToRemove.Add(handler);
                }
            }

            foreach (var handler in _handlersToRemove)
            {
                _responseHandlerByKey.Remove(handler.GetKey());
                _allResponseHandlers.Remove(handler);
            }
        }

        public void Dispose()
        {
            foreach (var handler in _allResponseHandlers)
            {
                handler.CallBackInfos.Clear();
            }
            _responseHandlerByKey.Clear();
            _allResponseHandlers.Clear();
        }

        private string GetResponseKey<TReq, TRes>()
        {
            return $"{typeof(TReq).FullName}->{typeof(TRes).FullName}";
        }

        // IDisposable 구현으로 구독 해제 지원
        private class ResponseUnsubscribe : IDisposable
        {
            private readonly Action _unsubscribe;
            public ResponseUnsubscribe(Action unsubscribe) => _unsubscribe = unsubscribe;
            public void Dispose() => _unsubscribe();
        }
    }
}