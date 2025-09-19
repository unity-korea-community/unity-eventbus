using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UNKO.EventBus
{
    public partial class EventBusLogic
    {
        [System.Serializable]
        public class Handler
        {
            public string TypeName;
            public Type EventType;
            public List<CallBackInfo> CallBackInfos = new List<CallBackInfo>();
            bool _requireSort = false;

            public Handler(Type eventType)
            {
                TypeName = eventType.Name;
                EventType = eventType;
            }

            public void Add(Delegate handler, int priority = 0, UnityEngine.Object debugObject = null)
            {
                if (handler == null) return;
                CallBackInfos.Add(new CallBackInfo(handler, priority, debugObject));
                _requireSort = true;
            }

            public void Remove(Delegate handler)
            {
                if (handler == null) return;
                CallBackInfos.RemoveAll(h => h.Callback == handler);
            }

            public void Remove(UnityEngine.Object debugObject)
            {
                CallBackInfos.RemoveAll(h => h.DebugObject == debugObject);
            }

            public int Invoke<T>(T evt)
            {
                if (_requireSort)
                {
                    CallBackInfos.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                    _requireSort = false;
                }

                var count = 0;
                for (int i = 0; i < CallBackInfos.Count; i++)
                {
                    var info = CallBackInfos[i];
                    if (info.Callback is Action<T> action)
                    {
                        action(evt);
                        count++;
                    }
                    else if (info.Callback is Action noParamAction)
                    {
                        noParamAction();
                        count++;
                    }
                    else if (info.Callback is Func<T, Task> asyncFunc)
                    {
                        asyncFunc(evt);
                        count++;
                    }
                }

                return count;
            }

            List<Task> _tasks = new List<Task>();
            public async Task<int> InvokeAsync<T>(T evt)
            {
                if (_requireSort)
                {
                    CallBackInfos.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                    _requireSort = false;
                }

                _tasks.Clear();
                var syncCount = 0;
                for (int i = 0; i < CallBackInfos.Count; i++)
                {
                    var info = CallBackInfos[i];
                    if (info.Callback is Func<T, Task> asyncFunc)
                    {
                        _tasks.Add(asyncFunc(evt));
                    }
                    if (info.Callback is Action<T> action)
                    {
                        action(evt);
                        syncCount++;
                    }
                    else if (info.Callback is Action noParamAction)
                    {
                        noParamAction();
                        syncCount++;
                    }
                }

                if (_tasks.Count > 0)
                {
                    await Task.WhenAll(_tasks);
                }

                return syncCount + _tasks.Count;
            }
        }
    }
}