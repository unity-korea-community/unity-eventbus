using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace UNKO.EventBus
{
    /// <summary>
    /// EventBus 파사드
    /// </summary>
    [System.Serializable]
    public static class EventBus
    {
        static IUnifiedEventBus _global;
        public static IUnifiedEventBus Global
        {
            get
            {
                if (_global == null)
                {
                    var go = new GameObject(nameof(GlobalEventBus));
                    _global = go.AddComponent<GlobalEventBus>();
                }

                return _global;
            }
        }

        public static IUnifiedEventBus GameObjectOf(Component component)
        {
            if (component == null)
            {
                Debug.LogError("EventBus.GameObjectOf: Component is null.");
                return Global;
            }

            var eventBus = component.GetComponent<IUnifiedEventBus>();
            if (eventBus != null)
            {
                return eventBus;
            }

            eventBus = component.GetComponentInParent<IUnifiedEventBus>();
            return eventBus;
        }

        public static IUnifiedEventBus GameObjectOf(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Debug.LogError("EventBus.GameObjectOf: GameObject is null.");
                return Global;
            }

            var eventBus = gameObject.GetComponent<IUnifiedEventBus>();
            if (eventBus != null)
            {
                return eventBus;
            }

            eventBus = gameObject.GetComponentInParent<IUnifiedEventBus>();
            return eventBus;
        }
    }
}