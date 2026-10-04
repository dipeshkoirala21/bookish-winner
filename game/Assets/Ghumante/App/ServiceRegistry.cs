using System;
using System.Collections.Generic;

namespace Ghumante.App
{
    /// <summary>
    /// Tiny typed service locator owned by <see cref="Bootstrap"/>. Gameplay assemblies never see it: they
    /// receive the Core interfaces they need from App at wiring time (ARCHITECTURE.md 7.1).
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _services[typeof(T)] = instance;
        }

        public T Get<T>() where T : class
        {
            object service;
            if (_services.TryGetValue(typeof(T), out service)) return (T)service;
            throw new InvalidOperationException("Service not registered: " + typeof(T).FullName);
        }

        public bool TryGet<T>(out T instance) where T : class
        {
            object service;
            if (_services.TryGetValue(typeof(T), out service))
            {
                instance = (T)service;
                return true;
            }
            instance = null;
            return false;
        }

        public int Count
        {
            get { return _services.Count; }
        }
    }
}
