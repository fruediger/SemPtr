using System.Collections.Generic;

namespace SemPtr;

internal sealed class SetEqualityComparer<T> : IEqualityComparer<ISet<T>?>
{
	public static readonly SetEqualityComparer<T> Instance = new();

	private SetEqualityComparer() { }

	public bool Equals(ISet<T>? x, ISet<T>? y) => (x, y) switch
	{
		(null, null) => true,
		(not null, not null) => x.SetEquals(y),
		_ => false
	};

	int IEqualityComparer<ISet<T>?>.GetHashCode(ISet<T>? obj) => 0; // Always return 0 to force comparisons by the `Equals` method only.
																    // This is necessary, because there's no good way to compute a hash code for a generic set
																	// (except for ordered sets, but we won't treat them specially here).
}
