using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace BrightSpark.ObservableProxy
{
    public class Observable
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo> PropertyChangedFieldInfoByType = new ConcurrentDictionary<Type, FieldInfo>();

        public static T Create<T>() where T : INotifyPropertyChanged
        {
            var options = new ProxyOptions<T>
            {
                OnSet = OnSet
            };

            return ProxyBuilder.CreateProxy(options);
        }

        public static void OnSet<T>(T o, string propertyName, object prevValue, object value)
        {
            if (Equals(prevValue, value))
            {
                return;
            }

            // trigger via reflection

            var fieldInfo = GetPropertyChangedFieldInfo(o.GetType());
            var eventDelegate = (MulticastDelegate)fieldInfo.GetValue(o);
            if (eventDelegate == null)
            {
                return;
            }

            foreach (var handler in eventDelegate.GetInvocationList())
            {
                handler.Method.Invoke(handler.Target, new object[] { o, new PropertyChangedEventArgs(propertyName) });
            }
        }

        private static FieldInfo GetPropertyChangedFieldInfo(Type type)
        {
            if (!PropertyChangedFieldInfoByType.ContainsKey(type))
            {
                PropertyChangedFieldInfoByType[type] = null;

                var t = type;
                while (t != null && t != typeof(object))
                {
                    var fi = t.GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (fi != null)
                    {
                        PropertyChangedFieldInfoByType[type] = fi;
                        break;
                    }

                    t = t.BaseType;
                }
            }

            return PropertyChangedFieldInfoByType.TryGetValue(type, out var fieldInfo) 
                ? fieldInfo 
                : null;
        }
    }
}
