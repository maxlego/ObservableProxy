using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace BrightSpark.ObservableProxy
{
    internal static class ILGeneratorExtensions
    {
        /// <summary>
        /// Burn an reference to the specified runtime object instance into the DynamicMethod
        /// </summary>
        public static void Emit_LdInst<TInst>(this ILGenerator il, TInst inst, bool gchFree) where TInst : class
        {
            var gch = GCHandle.Alloc(inst);

            var ptr = GCHandle.ToIntPtr(gch);

            if (IntPtr.Size == 4)
            {
                il.Emit(OpCodes.Ldc_I4, ptr.ToInt32());
            }
            else
            {
                il.Emit(OpCodes.Ldc_I8, ptr.ToInt64());
            }

            il.Emit(OpCodes.Ldobj, typeof(TInst));

            // Do this only if you can otherwise ensure that 'inst' outlives the DynamicMethod
            if (gchFree)
            {
                gch.Free();
            }
        }

        public static void CallDelegate(this ILGenerator il, Delegate action, Action<ILGenerator> setArguments)
        {
            if (!action.Method.IsPublic)
            {
                il.Emit_LdInst(action, false);
            }

            setArguments(il);

            if (action.Method.IsPublic)
            {
                il.Emit(OpCodes.Call, action.Method);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, action.GetType().GetMethod("Invoke"));
            }
        }
    }
}