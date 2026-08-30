using System;
using System.Runtime.InteropServices;

namespace SemPtr;

/// <summary>
/// Indicates that the source generator should generate <see langword="extension"/> members for all function pointer types for the specified <see langword="delegate"/> type,
/// using the optionally specified <see cref="CallConvs"/> as the calling conventions of the target function that is invoked by the generated method.
/// </summary>
/// <remarks>
/// <para>
/// Forces the source generator to generate <see langword="extension"/> members for all function pointer types for the specified <see langword="delegate"/> type.
/// </para>
/// <para>
/// You can specify the <see cref="CallConvs"/> property to indicate the calling conventions of the target function that is invoked by the generated method.
/// Specifying this property takes priority over any <see cref="UnmanagedFunctionPointerAttribute"/> that may be applied to the target <see langword="delegate"/> type.
/// </para>
/// <para>
/// Note that the target <see langword="delegate"/> type must be at least <see langword="internal"/>ly accessible.
/// </para>
/// <para>
/// You can specify this attribute on <see langword="delegate"/> type declarations.
/// If you don't control the source code of the <see langword="delegate"/> type, you can alternatively specify a <see cref="FunctionPointerAttribute{TDelegate}"/> attribute at the assembly level
/// to achieve the same effect with externally defined <see langword="delegate"/> types.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Delegate, AllowMultiple = false)]
public sealed class FunctionPointerAttribute : Attribute
{
	/// <summary>
	/// Gets or initializes the calling conventions that should be used for the generated <see langword="extension"/> members for invoking the function pointer.
	/// </summary>
	/// <value>
	/// The calling conventions that should be used for the generated <see langword="extension"/> members for invoking the function pointer,
	/// or <c><see langword="null"/></c> to use the calling conventions specified by an <see cref="UnmanagedFunctionPointerAttribute"/> on the target <see langword="delegate"/> type,
	/// or the default calling convention for the platform if none is specified.
	/// </value>
	/// <remarks>
	/// <para>
	/// If the value of this property is <c><see langword="null"/></c>, the calling conventions specified by an <see cref="UnmanagedFunctionPointerAttribute"/> on the target <see langword="delegate"/> type will be used,
	/// or the default calling convention for the platform if none is specified.
	/// </para>
	/// <para>
	/// The values specified in this property must be valid calling conventions types (i.e., <c>CallConv*</c> types from the <see cref="System.Runtime.CompilerServices"/> namespace).
	/// </para>
	/// </remarks>
	public Type[]? CallConvs { get; init; } = default;
}

/// <summary>
/// Indicates that the source generator should generate <see langword="extension"/> members for all function pointer types for the specified <see langword="delegate"/> type,
/// using the optionally specified <see cref="CallConvs"/> as the calling conventions of the target function that is invoked by the generated method.
/// </summary>
/// <typeparam name="TDelegate">The type of the <see langword="delegate"/> for which the source generator should generate <see langword="extension"/> members for.</typeparam>
/// <remarks>
/// <para>
/// Forces the source generator to generate <see langword="extension"/> members for all function pointer types for the specified <typeparamref name="TDelegate"/>.
/// </para>
/// <para>
/// You can specify the <see cref="CallConvs"/> property to indicate the calling conventions of the target function that is invoked by the generated method.
/// Specifying this property takes priority over any <see cref="UnmanagedFunctionPointerAttribute"/> that may be applied to the target <see langword="delegate"/> type.
/// </para>
/// <para>
/// This attribute can be applied at the assembly level multiple times, though each application must specify a different concrete <typeparamref name="TDelegate"/> type.
/// </para>
/// <para>
/// Note that the target <typeparamref name="TDelegate"/> type must be at least <see langword="internal"/>ly accessible.
/// </para>
/// <para>
/// You can specify this attribute with externally defined <see langword="delegate"/> types by applying it at the assembly level and as long as the <typeparamref name="TDelegate"/> is accessible from the assembly where the attribute is applied.
/// If you control the source code of the <typeparamref name="TDelegate"/>, you can alternatively specify a <see cref="FunctionPointerAttribute"/> attribute on the <see langword="delegate"/> type declaration itself to achieve the same effect.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FunctionPointerAttribute<TDelegate> : Attribute
	where TDelegate : notnull, Delegate
{
	/// <summary>
	/// Gets or initializes the calling conventions that should be used for the generated <see langword="extension"/> members for invoking the function pointer.
	/// </summary>
	/// <value>
	/// The calling conventions that should be used for the generated <see langword="extension"/> members for invoking the function pointer,
	/// or <c><see langword="null"/></c> to use the calling conventions specified by an <see cref="UnmanagedFunctionPointerAttribute"/> on the target <see langword="delegate"/> type,
	/// or the default calling convention for the platform if none is specified.
	/// </value>
	/// <remarks>
	/// <para>
	/// If the value of this property is <c><see langword="null"/></c>, the calling conventions specified by an <see cref="UnmanagedFunctionPointerAttribute"/> on the target <see langword="delegate"/> type will be used,
	/// or the default calling convention for the platform if none is specified.
	/// </para>
	/// <para>
	/// The values specified in this property must be valid calling conventions types (i.e., <c>CallConv*</c> types from the <see cref="System.Runtime.CompilerServices"/> namespace).
	/// </para>
	/// </remarks>
	public Type[]? CallConvs { get; init; } = default;
}