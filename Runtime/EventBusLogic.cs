using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace UNKO.EventBus
{
    /// <summary>
    /// 기본 EventBus 구현체
    /// </summary>
    [System.Serializable]
    public partial class EventBusLogic : IEventBus
    {
        [System.Serializable]
        public class CallBackInfo
        {
            public string CallBackName;
            public Delegate Callback;
            public UnityEngine.Object DebugObject;
            public int Priority;

            public CallBackInfo(Delegate callback, int priority = 0, UnityEngine.Object debugObject = null)
            {
                CallBackName = $"{callback.Target}.{callback.Method.Name}";
                Callback = callback;
                DebugObject = debugObject;
                Priority = priority;
            }
        }

        public Scope Scope { get; private set; }

        // 이벤트 타입별 핸들러 리스트 저장
        private readonly Dictionary<Type, Handler> _handlerByCallBackType = new Dictionary<Type, Handler>();

        [SerializeField] // NOTE: Unity 에디터에서 이 필드를 노출하기 위해 사용합니다.
        List<Handler> _allHandlers = new List<Handler>();

        HashSet<Type> _typesToPublish = new HashSet<Type>();

        Dictionary<Type, object> _lastValueByCallBackType = new Dictionary<Type, object>();

        public EventBusLogic(Scope scope = Scope.Pure)
        {
            Scope = scope;
        }

        public int Publish<T>(T evt, bool publishToUpstream = true)
        {
            var eventType = typeof(T);
            _typesToPublish.Clear();
            _typesToPublish.UnionWith(eventType.GetInterfaces());
            _typesToPublish.Add(eventType);

            int listenerCount = 0;
            foreach (var type in _typesToPublish.ToArray())
            {
                if (_handlerByCallBackType.TryGetValue(type, out var handler))
                {
                    listenerCount += handler.Invoke(evt);
                }
                _lastValueByCallBackType[type] = evt;
            }

            if (publishToUpstream && Scope != Scope.Global)
            {
                listenerCount += EventBus.Global.Publish(evt, false);
            }

            return listenerCount;
        }

        public async Task<int> PublishAsync<T>(T evt, bool publishToUpstream = true)
        {
            var eventType = typeof(T);
            _typesToPublish.Clear();
            _typesToPublish.UnionWith(eventType.GetInterfaces());
            _typesToPublish.Add(eventType);

            int listenerCount = 0;
            foreach (var type in _typesToPublish.ToArray())
            {
                if (_handlerByCallBackType.TryGetValue(type, out var handler))
                {
                    listenerCount += await handler.InvokeAsync(evt);
                }
            }

            if (publishToUpstream && Scope != Scope.Global)
            {
                listenerCount += await EventBus.Global.PublishAsync(evt, false);
            }

            _lastValueByCallBackType[eventType] = evt;
            return listenerCount;
        }

        /// <summary>
        /// 구독 등록: IDisposable 반환
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> callback, SubscribeOptions options = default)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            var key = typeof(T);
            if (!_handlerByCallBackType.TryGetValue(key, out var handler))
            {
                handler = new Handler(key);
                _handlerByCallBackType[key] = handler;
                _allHandlers.Add(handler);
            }
            handler.Add(callback, options.Priority, options.DebugObject);

            if (options.Sticky && _lastValueByCallBackType.TryGetValue(key, out var lastEvent))
            {
                callback((T)lastEvent);
            }

            return new Unsubscribe(() => UnSubscribe(callback));
        }

        public IDisposable Subscribe<T>(Action handler, SubscribeOptions options = default)
        {
            var key = typeof(T);
            if (!_handlerByCallBackType.TryGetValue(key, out var eventHandler))
            {
                eventHandler = new Handler(key);
                _handlerByCallBackType[key] = eventHandler;
                _allHandlers.Add(eventHandler);
            }
            eventHandler.Add(handler, options.Priority, options.DebugObject);

            if (options.Sticky && _lastValueByCallBackType.TryGetValue(key, out var lastEvent))
            {
                handler();
            }

            return new Unsubscribe(() => UnSubscribe(handler));
        }

        public IDisposable Subscribe<T>(Func<T, Task> handler, SubscribeOptions options = default)
        {
            var key = typeof(T);
            if (!_handlerByCallBackType.TryGetValue(key, out var eventHandler))
            {
                eventHandler = new Handler(key);
                _handlerByCallBackType[key] = eventHandler;
                _allHandlers.Add(eventHandler);
            }
            eventHandler.Add(handler, options.Priority, options.DebugObject);

            if (options.Sticky && _lastValueByCallBackType.TryGetValue(key, out var lastEvent))
            {
                handler((T)lastEvent);
            }

            return new Unsubscribe(() => UnSubscribe(handler));
        }

        public void UnSubscribe<T>(Action<T> handler)
        {
            _UnSubscribe<T>(handler);
        }

        public void UnSubscribe<T>(Action handler)
        {
            _UnSubscribe<T>(handler);
        }

        public void UnSubscribe<T>(Func<T, Task> handler)
        {
            _UnSubscribe<T>(handler);
        }

        private void _UnSubscribe<T>(Delegate callback)
        {
            var key = typeof(T);
            if (_handlerByCallBackType.TryGetValue(key, out var handler))
            {
                handler.Remove(callback);
                if (handler.CallBackInfos.Count == 0)
                {
                    _handlerByCallBackType.Remove(key);
                    _allHandlers.RemoveAll(h => h.EventType == key);
                }
            }
        }

        List<Handler> _handlersToRemove = new List<Handler>();
        public void UnSubscribe(UnityEngine.Object debugObject)
        {
            _handlersToRemove.Clear();
            foreach (var handler in _allHandlers)
            {
                handler.Remove(debugObject);
                if (handler.CallBackInfos.Count == 0)
                {
                    _handlersToRemove.Add(handler);
                }
            }

            foreach (var handler in _handlersToRemove)
            {
                _handlerByCallBackType.Remove(handler.EventType);
                handler.CallBackInfos.Clear();
                _allHandlers.Remove(handler);
            }
        }

        void UnSubscribe(Delegate callback)
        {
            _handlersToRemove.Clear();

            foreach (var handler in _allHandlers)
            {
                handler.Remove(callback);
                if (handler.CallBackInfos.Count == 0)
                {
                    _handlersToRemove.Add(handler);
                }
            }

            foreach (var handler in _handlersToRemove)
            {
                _handlerByCallBackType.Remove(handler.EventType);
                _allHandlers.Remove(handler);
            }
        }

        public void Dispose()
        {
            foreach (var handler in _allHandlers)
            {
                handler.CallBackInfos.Clear();
            }
            _handlerByCallBackType.Clear();
            _allHandlers.Clear();
        }

        // IDisposable 구현으로 구독 해제 지원
        private class Unsubscribe : IDisposable
        {
            private readonly Action _unsubscribe;
            public Unsubscribe(Action unsubscribe) => _unsubscribe = unsubscribe;
            public void Dispose() => _unsubscribe();
        }
    }
}