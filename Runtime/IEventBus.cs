using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework.Internal;

namespace UNKO.EventBus
{
    public enum Scope
    {
        Pure,

        /// <summary>
        /// 이벤트 버스가 게임 오브젝트 범위로 설정됨
        /// </summary>
        GameObject,

        /// <summary>
        /// 이벤트 버스가 전역 범위로 설정됨
        /// </summary>
        Global,
    }

    public readonly struct SubscribeOptions
    {
        /// <summary>
        /// 이미 지나간 마지막으로 발행되었던 이벤트를 구독후 받을지 여부
        /// </summary>
        public bool Sticky { get; }
        public int Priority { get; }
        public UnityEngine.Object DebugObject { get; }

        public SubscribeOptions(bool sticky = false, int priority = 0, UnityEngine.Object debugObject = null)
        {
            Sticky = sticky;
            Priority = priority;
            DebugObject = debugObject;
        }
    }

    public interface IEventBus : IDisposable
    {
        Scope Scope { get; }

        /// <summary>
        /// 이벤트 퍼블리시: 처리한 핸들러 수를 반환
        /// </summary>
        int Publish<T>(T evt, bool publishToUpstream = true);
        Task<int> PublishAsync<T>(T evt, bool publishToUpstream = true);

        IDisposable Subscribe<T>(Action handler, SubscribeOptions options = default);
        IDisposable Subscribe<T>(Action<T> handler, SubscribeOptions options = default);
        IDisposable Subscribe<T>(Func<T, Task> handler, SubscribeOptions options = default);
    }

    public interface IResponseEventBus : IDisposable
    {
        Scope Scope { get; }

        IReadOnlyList<TRes> Ask<TReq, TRes>(TReq req, bool publishToUpstream = true);
        Task<IReadOnlyList<TRes>> AskAsync<TReq, TRes>(TReq req, bool publishToUpstream = true);

        TOut Aggregate<TReq, TRes, TOut>(TReq req, Func<IReadOnlyList<TRes>, TOut> aggregator, bool publishToUpstream = true);
        Task<TOut> AggregateAsync<TReq, TRes, TOut>(TReq req, Func<IReadOnlyList<TRes>, TOut> aggregator, bool publishToUpstream = true);

        IDisposable Response<TReq, TRes>(Func<TReq, TRes> handler, SubscribeOptions options = default);
        IDisposable Response<TReq, TRes>(Func<TReq, Task<TRes>> handler, SubscribeOptions options = default);
    }

    public interface IUnifiedEventBus : IEventBus, IResponseEventBus
    {
    }

    public static class EventBusExtensions
    {
        /// <summary>
        /// 이벤트 구독 해제: IDisposable 토큰을 반환하여 구독 해제 가능
        /// </summary>
        public static IDisposable Subscribe<T>(this IEventBus bus, Action<T> handler, UnityEngine.Object debugObject = null)
        {
            return bus.Subscribe(handler, new SubscribeOptions(false, 0, debugObject));
        }

        /// <summary>
        /// 이벤트 구독 해제: IDisposable 토큰을 반환하여 구독 해제 가능
        /// </summary>
        public static IDisposable Subscribe<T>(this IEventBus bus, Action handler, UnityEngine.Object debugObject = null)
        {
            return bus.Subscribe<T>(handler, new SubscribeOptions(false, 0, debugObject));
        }

        public static IDisposable Subscribe<T>(this IEventBus bus, Action<T> handler)
        {
            return bus.Subscribe(handler, new SubscribeOptions(false, 0, null));
        }

        public static IDisposable Subscribe<T>(this IEventBus bus, Action handler)
        {
            return bus.Subscribe<T>(handler, new SubscribeOptions(false, 0, null));
        }

        public static async Task<T> SubscribeOnceAsync<T>(this IEventBus bus, UnityEngine.Object debugObject = null)
        {
            var tcs = new TaskCompletionSource<T>();
            IDisposable subscription = null;
            subscription = bus.Subscribe<T>(evt =>
            {
                tcs.SetResult(evt);
                subscription.Dispose();
            }, new SubscribeOptions(false, 0, debugObject));

            return await tcs.Task;
        }

        public static IDisposable SubscribeSticky<T>(this IEventBus bus, Action<T> handler)
        {
            return bus.Subscribe(handler, new SubscribeOptions(true, 0, null));
        }

        public static IDisposable SubscribeSticky<T>(this IEventBus bus, Action<T> handler, UnityEngine.Object debugObject)
        {
            return bus.Subscribe(handler, new SubscribeOptions(true, 0, debugObject));
        }

        public static IDisposable SubscribeSticky<T>(this IEventBus bus, Action handler)
        {
            return bus.Subscribe<T>(handler, new SubscribeOptions(true, 0, null));
        }

        public static int PublishNotUpstream<T>(this IEventBus bus, T evt)
        {
            return bus.Publish(evt, false);
        }

        public static TRes Aggregate<TReq, TRes>(this IResponseEventBus bus, TReq req, Func<IReadOnlyList<TRes>, TRes> aggregator, bool publishToUpstream = true)
        {
            return bus.Aggregate(req, aggregator, publishToUpstream);
        }

        public static Task<TRes> AggregateAsync<TReq, TRes>(this IResponseEventBus bus, TReq req, Func<IReadOnlyList<TRes>, TRes> aggregator, bool publishToUpstream = true)
        {
            return bus.AggregateAsync(req, aggregator, publishToUpstream);
        }
    }
}