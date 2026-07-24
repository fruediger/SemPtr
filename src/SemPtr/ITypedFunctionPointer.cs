using System;

namespace SemPtr;

/// <summary>
/// Represents an abstract pointer type pointing to a target function whose signature is specified by <typeparamref name="TDelegate"/>
/// </summary>
/// <typeparam name="TDelegate">The type of the <see langword="delegate"/> representing the signature of the target function the pointer points to.</typeparam>
public interface ITypedFunctionPointer<TDelegate>
	where TDelegate : notnull, Delegate
{
	/// <summary>
	/// Converts the current function pointer to a <see langword="delegate"/> of type <typeparamref name="TDelegate"/>.
	/// </summary>
	/// <returns>A <see langword="delegate"/> of type <typeparamref name="TDelegate"/> representing the target function.</returns>
	/// <remarks>
	/// <para>
	/// If the current function pointer type represents a non-<c><see langword="null"/></c>able function pointer type, then the current instance must not be a null pointer. If it is, an <see cref="InvalidOperationException"/> will be thrown.
	/// Otherwise, if the current function pointer type represents a <c><see langword="null"/></c>able function pointer type, then the current instance may be a null pointer. If it is, then the resulting <see langword="delegate"/> will be <c><see langword="null"/></c>.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">The current function pointer is a null pointer and the current function pointer type represents a non-<c><see langword="null"/></c>able function pointer type</exception>
	TDelegate? ToDelegate();
}

/// <inheritdoc cref="ITypedFunctionPointer{TDelegate}"/>
/// <inheritdoc cref="IFunctionPointer{TSelf}"/>
public interface ITypedFunctionPointer<TDelegate, TSelf> : ITypedFunctionPointer<TDelegate>, IFunctionPointer<TSelf>
	where TDelegate : notnull, Delegate
	where TSelf : unmanaged, ITypedFunctionPointer<TDelegate, TSelf>, allows ref struct
{
	/// <summary>
	/// Creates a <typeparamref name="TSelf"/> from a <see langword="delegate"/> of type <typeparamref name="TDelegate"/>.
	/// </summary>
	/// <param name="delegate">The <see langword="delegate"/> to create the function pointer from.</param>
	/// <returns>A <typeparamref name="TSelf"/> pointing to the target function represented by <paramref name="delegate"/>.</returns>
	/// <remarks>
	/// <para>
	/// If <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able function pointer type, then the <paramref name="delegate"/> must not be <c><see langword="null"/></c>. If it is, an <see cref="ArgumentNullException"/> will be thrown.
	/// Otherwise, if <typeparamref name="TSelf"/> represents a <c><see langword="null"/></c>able function pointer type, then the <paramref name="delegate"/> pointer may be <c><see langword="null"/></c>. If it is, then the resulting <typeparamref name="TSelf"/> will represent a null pointer.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="delegate"/> is <c><see langword="null"/></c> and <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able pointer type</exception>
	static abstract TSelf FromDelegate(TDelegate? @delegate);
}
