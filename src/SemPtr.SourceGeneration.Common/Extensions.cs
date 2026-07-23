using System;
using System.Collections.Generic;

namespace SemPtr.SourceGeneration;

internal static class Extensions
{
	private static class EnumCache<TEnum>
		where TEnum : struct, Enum
	{
		public static readonly TEnum[] Values = (TEnum[])Enum.GetValues(typeof(TEnum));
	}

	extension<TEnum>(TEnum)
		where TEnum : struct, Enum
	{
		public static IReadOnlyList<TEnum> Values => EnumCache<TEnum>.Values;
	}

	extension(bool value)
	{
		public bool Implies(bool other) => !value || other;
	}
}
