using System;
using System.ComponentModel;
using System.Reflection;

namespace BrightSpark.ObservableProxy
{
    public class Observable
    {
        public static T Create<T>() where T : INotifyPropertyChanged
        {
            var options = new ProxyOptions<T>
            {
                OnSet = (o, propertyName, prevValue, value) =>
                {
                    if (Equals(prevValue, value))
                    {
                        return;
                    }

                    // trigger via reflection

                    var fieldInfo = o.GetType().BaseType.GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                    var eventDelegate = (MulticastDelegate)fieldInfo.GetValue(o);
                    if (eventDelegate != null)
                    {
                        foreach (var handler in eventDelegate.GetInvocationList())
                        {
                            handler.Method.Invoke(handler.Target, new object[] { o, new PropertyChangedEventArgs(propertyName) });
                        }
                    }
                }
            };

            return ProxyBuilder.CreateProxy<T>(options);
        }
    }
}
