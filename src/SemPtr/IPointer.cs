using System.Runtime.CompilerServices;

namespace SemPtr;

internal interface IPointer
{
	internal static string DefaultFormatString
	{
		get
		{
			unsafe
			{
				return sizeof(void*) switch
				{
					4 => "X8",
					8 => "X16",
					_ => "X32" // should be good enough for now, even for near-future architectures, but we can always extend this if needed
				};
			}
		}
	}

	unsafe void* Raw { get; }
}

internal interface IPointer<T> : IPointer
	where T : unmanaged, allows ref struct
{
	unsafe new T* Raw { get; }

	unsafe void* IPointer.Raw { [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)] get => Raw; }
}
