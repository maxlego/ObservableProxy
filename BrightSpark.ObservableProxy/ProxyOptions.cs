using System;

namespace BrightSpark.ObservableProxy
{
    internal class ProxyOptions
    {
        public Delegate OnSet { get; set; }
    }

    internal class ProxyOptions<T> : ProxyOptions
    {
        private Action<T, string, object, object> _onSet;

        public new Action<T, string, object, object> OnSet
        {
            get => _onSet;
            set
            {
                _onSet = value;
                base.OnSet = _onSet;
            }
        }
    }
}