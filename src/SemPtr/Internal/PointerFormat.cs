namespace SemPtr.Internal;

internal static class PointerFormat
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
}
