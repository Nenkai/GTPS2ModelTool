using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace SharpTriStrip
{
	/// <summary>
	/// Provides memory set utility methods.
	/// </summary>
	public static class Utils
	{
		private static readonly Action<IntPtr, byte, int> ms_memset;

		static Utils()
		{
			var dynamicMethod = new DynamicMethod
			(
				"Memset",
				MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard,
				null,
				new[] { typeof(IntPtr), typeof(byte), typeof(int) },
				typeof(Utils),
				true
			);

			var generator = dynamicMethod.GetILGenerator();

			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Ldarg_2);
			generator.Emit(OpCodes.Initblk);
			generator.Emit(OpCodes.Ret);

			Utils.ms_memset = dynamicMethod.CreateDelegate(typeof(Action<IntPtr, byte, int>)) as Action<IntPtr, byte, int>;
		}

		/// <summary>
		/// Sets memory in the array given with value provided.
		/// </summary>
		/// <param name="array"><see cref="Array"/> of type <see cref="Byte"/> to set.</param>
		/// <param name="value">Value to initialize array with.</param>
		/// <param name="length">Length of array to set value with.</param>
		public static void Memset(byte[] array, byte value, int length)
		{
			var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
			Utils.ms_memset(handle.AddrOfPinnedObject(), value, length);
			handle.Free();
		}

		/// <summary>
		/// Sets memory in the array given with value provided.
		/// </summary>
		/// <param name="array"><see cref="Array"/> of type <see cref="Int32"/> to set.</param>
		/// <param name="value">Value to initialize array with.</param>
		/// <param name="length">Length of array to set value with.</param>
		public static void Memset(int[] array, byte value, int length)
		{
			var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
			Utils.ms_memset(handle.AddrOfPinnedObject(), value, length << 2);
			handle.Free();
		}
	}
}
