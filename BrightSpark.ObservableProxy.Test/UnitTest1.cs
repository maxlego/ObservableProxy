using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace BrightSpark.ObservableProxy.Test
{
    public class UnitTest1
    {
        public abstract class Foo : INotifyPropertyChanged
        {
            public abstract string X { get; set; }
            public virtual string Y { get; set; }
            public virtual string Z { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        [Fact]
        public void CreateObservable()
        {
            var count = 0;
            var changedProperties = new List<string>();

            var proxy = Observable.Create<Foo>();

            proxy.PropertyChanged += (sender, args) =>
            {
                // should be called when property value actually changes
                count++;
                changedProperties.Add(args.PropertyName);
            };

            proxy.X = "x"; // triggers property change
            proxy.X = "x"; // does not trigger property change
            proxy.X = ""; // triggers property change

            proxy.Y = "y"; // triggers property change
            proxy.Y = "y"; // does not trigger property change
            proxy.Y = ""; // triggers property change

            Assert.Equal(4, count);

            Assert.Contains(nameof(Foo.X), changedProperties);
            Assert.Contains(nameof(Foo.Y), changedProperties);
            Assert.DoesNotContain(nameof(Foo.Z), changedProperties);
        }
    }
}
