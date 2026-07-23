using System;
using System.Runtime.CompilerServices;

namespace SemPtr;

/// <summary>
/// Represents an abstract pointer type.
/// </summary>
public interface IPointer
{
	/// <summary>
	/// Gets a raw pointer pointing to the same target as the current pointer.
	/// </summary>
	/// <value>
	/// A raw pointer pointing to the same target as the current pointer.
	/// </value>
	unsafe void* Raw { get; }

	/// <summary>
	/// Gets an <see cref="IntPtr"/> that represents the same memory address as the current pointer.
	/// </summary>
	/// <value>
	/// An <see cref="IntPtr"/> that represents the same memory address as the current pointer.
	/// </value>
	IntPtr AsIntPtr { [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)] get { unsafe { return unchecked((IntPtr)Raw); } } }

	/// <summary>
	/// Gets an <see cref="UIntPtr"/> that represents the same memory address as the current pointer.
	/// </summary>
	/// <value>
	/// An <see cref="UIntPtr"/> that represents the same memory address as the current pointer.
	/// </value>
	UIntPtr AsUIntPtr { [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)] get { unsafe { return unchecked((UIntPtr)Raw); } } }
}

/// <inheritdoc cref="IPointer"/>
/// <typeparam name="TSelf">The type of the pointer.</typeparam>
public interface IPointer<TSelf> : IPointer
	where TSelf : unmanaged, IPointer<TSelf>, allows ref struct
{
	/// <summary>
	/// Creates a <typeparamref name="TSelf"/> from a <paramref name="raw"/> pointer.
	/// </summary>
	/// <param name="raw">The raw pointer specifying the target that the resulting <typeparamref name="TSelf"/> will point to.</param>
	/// <returns>A <typeparamref name="TSelf"/> that points to the same target as the specified <paramref name="raw"/> pointer.</returns>
	/// <remarks>
	/// <para>
	/// If <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able pointer type, then the <paramref name="raw"/> pointer must not be <c><see langword="null"/></c>. If it is, an <see cref="ArgumentNullException"/> will be thrown.
	/// Otherwise, if <typeparamref name="TSelf"/> represents a <c><see langword="null"/></c>able pointer type, then the <paramref name="raw"/> pointer may be <c><see langword="null"/></c>. If it is, then the resulting <typeparamref name="TSelf"/> will represent a null pointer.
	/// </para>
	/// <para>
	/// The resulting <typeparamref name="TSelf"/> will point to the same target as the specified <paramref name="raw"/> pointer.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="raw"/> is <c><see langword="null"/></c> and <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able pointer type</exception>
	unsafe static abstract TSelf FromRaw(void* raw);

	/// <summary>
	/// Creates a <typeparamref name="TSelf"/> from a memory address given as an <see cref="IntPtr"/>.
	/// </summary>
	/// <param name="value">The memory address to create a <typeparamref name="TSelf"/> from.</param>
	/// <returns>A <typeparamref name="TSelf"/> that points to the same memory address as the specified <paramref name="value"/>.</returns>
	/// <remarks> 
	/// <para>
	/// If <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able pointer type, then the <paramref name="value"/> pointer must not be <c>0</c>. Otherwise, an <see cref="ArgumentNullException"/> will be thrown.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>0</c> and <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able pointer type</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	static virtual TSelf FromIntPtr(IntPtr value) { unsafe { return TSelf.FromRaw(unchecked((void*)value)); } }

	/// <summary>
	/// Creates a <typeparamref name="TSelf"/> from a memory address given as an <see cref="IntPtr"/>.
	/// </summary>
	/// <param name="value">The memory address to create a <typeparamref name="TSelf"/> from.</param>
	/// <returns>A <typeparamref name="TSelf"/> that points to the same memory address as the specified <paramref name="value"/>.</returns>
	/// <remarks> 
	/// <para>
	/// If <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able pointer type, then the <paramref name="value"/> pointer must not be <c>0</c>. Otherwise, an <see cref="ArgumentNullException"/> will be thrown.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>0</c> and <typeparamref name="TSelf"/> represents a non-<c><see langword="null"/></c>able pointer type</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	static virtual TSelf FromUIntPtr(UIntPtr value) { unsafe { return TSelf.FromRaw(unchecked((void*)value)); } }
}
