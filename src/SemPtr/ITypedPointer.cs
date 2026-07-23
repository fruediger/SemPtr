using System.Runtime.CompilerServices;

namespace SemPtr;

/// <summary>
/// Represents an abstract pointer type pointing to a target of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the target that the pointer points to.</typeparam>
public interface ITypedPointer<T> : IPointer
	where T : unmanaged, allows ref struct
{
	/// <inheritdoc cref="IPointer.Raw"/>
	unsafe new T* Raw { get; }

	unsafe void* IPointer.Raw { [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)] get => Raw; }
}

/// <inheritdoc cref="ITypedPointer{T}"/>
/// <inheritdoc cref="IPointer{TSelf}"/>
public interface ITypedPointer<T, TSelf> : ITypedPointer<T>, IPointer<TSelf>
	where T : unmanaged, allows ref struct
	where TSelf : unmanaged, ITypedPointer<T, TSelf>, allows ref struct
{
	/// <inheritdoc cref="IPointer{TSelf}.FromRaw(void*)"/>
	unsafe static abstract TSelf FromRaw(T* raw);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	unsafe static TSelf IPointer<TSelf>.FromRaw(void* raw) { return TSelf.FromRaw(unchecked((T*)raw)); }
}
