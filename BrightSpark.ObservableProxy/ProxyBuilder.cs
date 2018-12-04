using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace BrightSpark.ObservableProxy
{
    internal class ProxyBuilder
    {
        private const MethodAttributes GETTER_SETTER_METHOD_ATTRIBUTES =
            MethodAttributes.Public |
            MethodAttributes.SpecialName |
            MethodAttributes.HideBySig |
            MethodAttributes.Virtual;

        private const string ASSEMBLY_NAME = "BrightSpark.ObservableProxy";

        private static readonly Dictionary<Type, Type> ProxyTypes = new Dictionary<Type, Type>();

        private static ModuleBuilder _moduleBuilder;
        private static ModuleBuilder ModuleBuilder
        {
            get
            {
                if (_moduleBuilder != null)
                {
                    return _moduleBuilder;
                }

                var ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(ASSEMBLY_NAME), AssemblyBuilderAccess.Run);
                _moduleBuilder = ab.DefineDynamicModule(ASSEMBLY_NAME + ".dll");
                return _moduleBuilder;
            }
        }

        private static PropertyInfo[] GetPublicProperties(Type type)
        {
            var bindingFlags = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance;

            if (type.IsInterface)
            {
                var propertyInfos = new List<PropertyInfo>();

                var considered = new List<Type>();
                var queue = new Queue<Type>();
                considered.Add(type);
                queue.Enqueue(type);
                while (queue.Count > 0)
                {
                    var subType = queue.Dequeue();
                    foreach (var subInterface in subType.GetInterfaces())
                    {
                        if (considered.Contains(subInterface))
                        {
                            continue;
                        }

                        considered.Add(subInterface);
                        queue.Enqueue(subInterface);
                    }

                    var typeProperties = subType.GetProperties(
                        bindingFlags);

                    var newPropertyInfos = typeProperties
                        .Where(x => !propertyInfos.Contains(x));

                    propertyInfos.InsertRange(0, newPropertyInfos);
                }

                return propertyInfos.ToArray();
            }

            return type.GetProperties(bindingFlags);
        }

        /// <summary>
        /// Creates proxy type which overrides only properties
        /// </summary>
        /// <returns></returns>
        private static Type GetProxyType(Type baseType, ProxyOptions options)
        {
            if (ProxyTypes.ContainsKey(baseType))
            {
                return ProxyTypes[baseType];
            }

            var proxyTypeName = $"{baseType.Name}_Proxy_{Guid.NewGuid():N}";

            // proxy is public class inherited from baseType
            TypeBuilder tb = ModuleBuilder.DefineType(proxyTypeName, TypeAttributes.Public, baseType);

            foreach (var propInfo in GetPublicProperties(baseType))
            {
                var propName = propInfo.Name;
                var propType = propInfo.PropertyType;

                //create the backing field
                FieldBuilder backingField = tb.DefineField("_" + propName, propType, FieldAttributes.Private);

                //create property
                PropertyBuilder prop = tb.DefineProperty(propName, PropertyAttributes.SpecialName, propType, null);

                var propSetter = CreateSetter(backingField, tb, propName, options.OnSet);
                var propGetter = CreateGetter(backingField, tb, propName);

                //assign getter and setter
                prop.SetGetMethod(propGetter);
                prop.SetSetMethod(propSetter);
            }

            // default constructor
            tb.DefineDefaultConstructor(MethodAttributes.Public);

            // Finish the type.
            var proxyType = tb.CreateType();

            ProxyTypes[baseType] = proxyType;
            return proxyType;
        }

        private static MethodBuilder CreateGetter(FieldBuilder backingField, TypeBuilder tb, string propName)
        {
            var propType = backingField.FieldType;

            MethodBuilder propGetter = tb.DefineMethod("get_" + propName, GETTER_SETTER_METHOD_ATTRIBUTES, propType, Type.EmptyTypes);
            ILGenerator ilGet = propGetter.GetILGenerator();

            ilGet.Emit(OpCodes.Ldarg_0);
            ilGet.Emit(OpCodes.Ldfld, backingField);
            ilGet.Emit(OpCodes.Ret);

            return propGetter;
        }

        private static MethodBuilder CreateSetter(FieldBuilder backingField, TypeBuilder tb, string propName, Delegate onSet)
        {
            var propType = backingField.FieldType;

            MethodBuilder propSetter = tb.DefineMethod("set_" + propName, GETTER_SETTER_METHOD_ATTRIBUTES, typeof(void), new[] { propType });
            ILGenerator ilSet = propSetter.GetILGenerator();

            // TPropType oldValue
            var ilTemp = ilSet.DeclareLocal(propType);

            // oldValue = this._x;
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldfld, backingField);
            ilSet.Emit(OpCodes.Stloc, ilTemp);

            // this._x = value
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldarg_1);
            ilSet.Emit(OpCodes.Stfld, backingField);

            ilSet.Emit_LdInst(onSet, false);

            // onSet.Invoke(this, propName, oldValue, this._x)
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldstr, propName);
            ilSet.Emit(OpCodes.Ldloc, ilTemp);
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldfld, backingField);
            ilSet.Emit(OpCodes.Callvirt, onSet.GetType().GetMethod("Invoke"));

            // return
            ilSet.Emit(OpCodes.Nop);
            ilSet.Emit(OpCodes.Ret);

            return propSetter;
        }

        /// <summary>
        /// Creates proxy instance
        /// </summary>
        /// <param name="baseType"></param>
        /// <returns></returns>
        public static object CreateProxy(Type baseType, ProxyOptions options)
        {
            var proxyType = GetProxyType(baseType, options);
            var proxy = Activator.CreateInstance(proxyType);
            return proxy;
        }

        /// <summary>
        /// Creates proxy instance
        /// </summary>
        /// <typeparam name="T">base type</typeparam>
        /// <returns></returns>
        public static T CreateProxy<T>(ProxyOptions<T> options)
        {
            var baseType = typeof(T);
            return (T)CreateProxy(baseType, options);
        }
    }
}