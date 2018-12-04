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
    }
}