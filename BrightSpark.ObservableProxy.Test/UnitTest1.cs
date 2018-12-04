using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace BrightSpark.ObservableProxy.Test
{
    public class UnitTest1
    {
        public class Foo : INotifyPropertyChanged
        {
            private string _y;

            public bool BaseYWasSet { get; private set; }
            public bool BaseYWasGet { get; private set; }

            public virtual string X { get; set; } = "x";

            public virtual string Y
            {
                get
                {
                    BaseYWasGet = true;
                    return _y;
                }
                set
                {
                    _y = value;
                    BaseYWasSet = true;
                }
            }

            public virtual string Z { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public abstract class AbstractFoo : INotifyPropertyChanged
        {
            public virtual string X { get; set; } = "x";

            public abstract string Y { get; set; }

            public virtual string Z { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public interface IFooContract : INotifyPropertyChanged
        {
            string X { get; set; }
            string Y { get; set; }
            string Z { get; set; }
        }

        [Fact]
        public void CreateObservableRegular()
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

            proxy.X = "x"; // does not trigger property change (default value was set to "x")
            proxy.X = "x"; // does not trigger property change
            proxy.X = ""; // triggers property change

            proxy.Y = "y"; // triggers property change
            proxy.Y = "y"; // does not trigger property change
            proxy.Y = ""; // triggers property change

            Assert.Null(proxy.Z);
            Assert.Equal(3, count);

            Assert.Contains(nameof(Foo.X), changedProperties);
            Assert.Contains(nameof(Foo.Y), changedProperties);
            Assert.DoesNotContain(nameof(Foo.Z), changedProperties);
        }

        [Fact]
        public void BaseCall()
        {
            var proxy = Observable.Create<Foo>();

            Assert.False(proxy.BaseYWasGet);
            Assert.False(proxy.BaseYWasSet);

            var y = proxy.Y;
            Assert.True(proxy.BaseYWasGet);
            Assert.False(proxy.BaseYWasSet);

            proxy.Y = "y";
            Assert.True(proxy.BaseYWasSet);
        }

        [Fact]
        public void CreateObservableFromAbstract()
        {
            var count = 0;
            var changedProperties = new List<string>();

            var proxy = Observable.Create<AbstractFoo>();

            proxy.PropertyChanged += (sender, args) =>
            {
                // should be called when property value actually changes
                count++;
                changedProperties.Add(args.PropertyName);
            };

            proxy.X = "x"; // does not trigger property change (default value was set to "x")
            proxy.X = "x"; // does not trigger property change
            proxy.X = ""; // triggers property change

            proxy.Y = "y"; // triggers property change
            proxy.Y = "y"; // does not trigger property change
            proxy.Y = ""; // triggers property change

            Assert.Null(proxy.Z);
            Assert.Equal(3, count);

            Assert.Contains(nameof(AbstractFoo.X), changedProperties);
            Assert.Contains(nameof(AbstractFoo.Y), changedProperties);
            Assert.DoesNotContain(nameof(AbstractFoo.Z), changedProperties);
        }

        [Fact]
        public void CreateObservableFromInterface()
        {
            var count = 0;
            var changedProperties = new List<string>();

            var proxy = Observable.Create<IFooContract>();

            proxy.PropertyChanged += (sender, args) =>
            {
                // should be called when property value actually changes
                count++;
                changedProperties.Add(args.PropertyName);
            };

            proxy.X = "x"; // triggers property change
            Assert.Equal("x", proxy.X);

            proxy.X = "x"; // does not trigger property change
            Assert.Equal("x", proxy.X);

            proxy.X = ""; // triggers property change
            Assert.Equal("", proxy.X);

            proxy.Y = "y"; // triggers property change
            Assert.Equal("y", proxy.Y);

            proxy.Y = "y"; // does not trigger property change
            Assert.Equal("y", proxy.Y);

            proxy.Y = ""; // triggers property change
            Assert.Equal("", proxy.Y);

            Assert.Null(proxy.Z);
            Assert.Equal(4, count);

            Assert.Contains(nameof(IFooContract.X), changedProperties);
            Assert.Contains(nameof(IFooContract.Y), changedProperties);
            Assert.DoesNotContain(nameof(IFooContract.Z), changedProperties);
        }
    }
}
