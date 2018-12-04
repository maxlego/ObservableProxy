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

        private const MethodAttributes EVENT_BG_METHOD_ATTRIBUTES = 
            MethodAttributes.Public | 
            MethodAttributes.Virtual | 
            MethodAttributes.SpecialName |
            MethodAttributes.Final | 
            MethodAttributes.HideBySig | 
            MethodAttributes.NewSlot;

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
                _moduleBuilder = ab.DefineDynamicModule($"{ASSEMBLY_NAME}.dll");
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

                    var typeProperties = subType.GetProperties(bindingFlags);

                    var newPropertyInfos = typeProperties
                        .Where(x => !propertyInfos.Contains(x));

                    propertyInfos.InsertRange(0, newPropertyInfos);
                }

                return propertyInfos.ToArray();
            }

            return type.GetProperties(bindingFlags);
        }

        private static EventInfo[] GetPublicEvents(Type type)
        {
            var propertyInfos = new List<EventInfo>();

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

                var newEventInfos = subType
                    .GetEvents()
                    .Where(x => !propertyInfos.Contains(x));

                propertyInfos.InsertRange(0, newEventInfos);
            }

            return propertyInfos.ToArray();
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

            TypeBuilder tb;
            if (baseType.IsInterface)
            {
                tb = ModuleBuilder.DefineType(proxyTypeName, TypeAttributes.Public, null, new[] {baseType});

                foreach (var eventInfo in GetPublicEvents(baseType))
                {
                    CreateEvent(tb, eventInfo);
                }
            }
            else
            {
                tb = ModuleBuilder.DefineType(proxyTypeName, TypeAttributes.Public, baseType);
            }

            foreach (var propInfo in GetPublicProperties(baseType))
            {
                var propName = propInfo.Name;
                var propType = propInfo.PropertyType;

                // create property
                PropertyBuilder prop = tb.DefineProperty(propName, PropertyAttributes.SpecialName, propType, null);

                Lazy<FieldBuilder> backingField = new Lazy<FieldBuilder>(
                    () => tb.DefineField($"_{propName}", propType, FieldAttributes.Private)
                );

                var baseGet = propInfo.GetGetMethod();
                if (baseGet != null)
                {
                    var useBaseGet = !baseType.IsInterface && !baseGet.IsAbstract;
                    var propGetter = useBaseGet
                        ? CreateGetter(propInfo, tb)
                        : CreateGetter(backingField.Value, tb, propName);

                    // assign getter
                    prop.SetGetMethod(propGetter);
                }

                var baseSet = propInfo.GetSetMethod();
                if (baseSet != null)
                {
                    var useBaseSet = !baseType.IsInterface && !baseSet.IsAbstract;
                    var propSetter = useBaseSet
                        ? CreateSetter(propInfo, tb, options.OnSet)
                        : CreateSetter(backingField.Value, tb, propName, options.OnSet);

                    // assign setter
                    prop.SetSetMethod(propSetter);
                }
            }

            // default constructor
            tb.DefineDefaultConstructor(MethodAttributes.Public);

            // Finish the type.
            var proxyType = tb.CreateType();

            ProxyTypes[baseType] = proxyType;
            return proxyType;
        }

        private static void CreateEvent(TypeBuilder tb, EventInfo eventInfo)
        {
            var eventName = eventInfo.Name;
            var eventType = eventInfo.EventHandlerType;

            var fieldBuilder = tb.DefineField(eventName, eventType, FieldAttributes.Private);
            var eventBuilder = tb.DefineEvent(eventName, EventAttributes.None, eventType);

            var addMethod = tb.DefineMethod($"add_{eventName}",
                EVENT_BG_METHOD_ATTRIBUTES,
                CallingConventions.Standard | CallingConventions.HasThis,
                typeof(void),
                new[] {eventType});

            var addGenerator = addMethod.GetILGenerator();
            var combine = typeof(Delegate).GetMethod("Combine", new[] {typeof(Delegate), typeof(Delegate)});
            addGenerator.Emit(OpCodes.Ldarg_0);
            addGenerator.Emit(OpCodes.Ldarg_0);
            addGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            addGenerator.Emit(OpCodes.Ldarg_1);
            addGenerator.Emit(OpCodes.Call, combine);
            addGenerator.Emit(OpCodes.Castclass, eventType);
            addGenerator.Emit(OpCodes.Stfld, fieldBuilder);
            addGenerator.Emit(OpCodes.Ret);
            eventBuilder.SetAddOnMethod(addMethod);

            var removeMethod = tb.DefineMethod($"remove_{eventName}",
                EVENT_BG_METHOD_ATTRIBUTES,
                CallingConventions.Standard | CallingConventions.HasThis,
                typeof(void),
                new[] {eventType});

            var remove = typeof(Delegate).GetMethod("Remove", new[] {typeof(Delegate), typeof(Delegate)});
            var removeGenerator = removeMethod.GetILGenerator();
            removeGenerator.Emit(OpCodes.Ldarg_0);
            removeGenerator.Emit(OpCodes.Ldarg_0);
            removeGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            removeGenerator.Emit(OpCodes.Ldarg_1);
            removeGenerator.Emit(OpCodes.Call, remove);
            removeGenerator.Emit(OpCodes.Castclass, eventType);
            removeGenerator.Emit(OpCodes.Stfld, fieldBuilder);
            removeGenerator.Emit(OpCodes.Ret);
            eventBuilder.SetRemoveOnMethod(removeMethod);
        }

        private static MethodBuilder CreateGetter(FieldBuilder backingField, TypeBuilder tb, string propName)
        {
            var propType = backingField.FieldType;

            MethodBuilder propGetter = tb.DefineMethod($"get_{propName}", GETTER_SETTER_METHOD_ATTRIBUTES, propType, Type.EmptyTypes);
            ILGenerator ilGet = propGetter.GetILGenerator();

            ilGet.Emit(OpCodes.Ldarg_0);
            ilGet.Emit(OpCodes.Ldfld, backingField);
            ilGet.Emit(OpCodes.Ret);

            return propGetter;
        }

        private static MethodBuilder CreateGetter(PropertyInfo propertyInfo, TypeBuilder tb)
        {
            var propName = propertyInfo.Name;
            var propType = propertyInfo.PropertyType;

            MethodBuilder propGetter = tb.DefineMethod($"get_{propName}", GETTER_SETTER_METHOD_ATTRIBUTES, propType, Type.EmptyTypes);
            ILGenerator ilGet = propGetter.GetILGenerator();

            ilGet.Emit(OpCodes.Nop);
            ilGet.Emit(OpCodes.Ldarg_0);
            ilGet.Emit(OpCodes.Call, propertyInfo.GetMethod);
            ilGet.Emit(OpCodes.Ret);

            return propGetter;
        }

        private static MethodBuilder CreateSetter(FieldBuilder backingField, TypeBuilder tb, string propName, Delegate onSet)
        {
            var propType = backingField.FieldType;

            MethodBuilder propSetter = tb.DefineMethod($"set_{propName}", GETTER_SETTER_METHOD_ATTRIBUTES, typeof(void), new[] { propType });
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

        private static MethodBuilder CreateSetter(PropertyInfo propertyInfo, TypeBuilder tb, Delegate onSet)
        {
            var propName = propertyInfo.Name;
            var propType = propertyInfo.PropertyType;

            MethodBuilder propSetter = tb.DefineMethod($"set_{propName}", GETTER_SETTER_METHOD_ATTRIBUTES, typeof(void), new[] { propType });
            ILGenerator ilSet = propSetter.GetILGenerator();

            // TPropType oldValue
            var ilTemp = ilSet.DeclareLocal(propType);

            // oldValue = base.X;
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Call, propertyInfo.GetGetMethod());
            ilSet.Emit(OpCodes.Stloc, ilTemp);

            // base.X = value
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldarg_1);
            ilSet.Emit(OpCodes.Call, propertyInfo.GetSetMethod());

            ilSet.Emit_LdInst(onSet, false);

            // onSet.Invoke(this, propName, oldValue, base.X)
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Ldstr, propName);
            ilSet.Emit(OpCodes.Ldloc, ilTemp);
            ilSet.Emit(OpCodes.Ldarg_0);
            ilSet.Emit(OpCodes.Call, propertyInfo.GetGetMethod());
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