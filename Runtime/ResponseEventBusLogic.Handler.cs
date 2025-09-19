using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UNKO.EventBus
{
    public partial class ResponseEventBusLogic
    {
        [System.Serializable]
        public class ResponseHandler
        {
            public string TypeName;
            public Type RequestType;
            public Type ResponseType;
            public List<ResponseCallBackInfo> CallBackInfos = new List<ResponseCallBackInfo>();
            bool _requireSort = false;

            public ResponseHandler(Type requestType, Type responseType)
            {
                TypeName = $"{requestType.Name} -> {responseType.Name}";
                RequestType = requestType;
                ResponseType = responseType;
            }

            public string GetKey()
            {
                return $"{RequestType.FullName}->{ResponseType.FullName}";
            }

            public void Add(Delegate handler, int priority = 0, UnityEngine.Object debugObject = null)
            {
                if (handler == null) return;
                CallBackInfos.Add(new ResponseCallBackInfo(handler, priority, debugObject));
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

            public List<TRes> InvokeResponse<TReq, TRes>(TReq req)
            {
                if (_requireSort)
                {
                    CallBackInfos.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                    _requireSort = false;
                }

                var results = new List<TRes>();
                for (int i = 0; i < CallBackInfos.Count; i++)
                {
                    var info = CallBackInfos[i];
                    
                    if (info.Callback is Func<TReq, TRes> func)
                    {
                        try
                        {
                            var result = func(req);
                            results.Add(result);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in response handler {info.CallBackName}: {ex.Message}");
                        }
                    }
                }

                return results;
            }

            public async Task<List<TRes>> InvokeResponseAsync<TReq, TRes>(TReq req)
            {
                if (_requireSort)
                {
                    CallBackInfos.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                    _requireSort = false;
                }

                var tasks = new List<Task<TRes>>();
                var syncResults = new List<TRes>();
                
                for (int i = 0; i < CallBackInfos.Count; i++)
                {
                    var info = CallBackInfos[i];
                    
                    if (info.Callback is Func<TReq, Task<TRes>> asyncFunc)
                    {
                        tasks.Add(asyncFunc(req));
                    }
                    else if (info.Callback is Func<TReq, TRes> func)
                    {
                        try
                        {
                            var result = func(req);
                            syncResults.Add(result);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error in response handler {info.CallBackName}: {ex.Message}");
                        }
                    }
                }

                if (tasks.Count > 0)
                {
                    try
                    {
                        var asyncResults = await Task.WhenAll(tasks);
                        syncResults.AddRange(asyncResults);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error in async response handlers: {ex.Message}");
                    }
                }

                return syncResults;
            }
        }
    }
}