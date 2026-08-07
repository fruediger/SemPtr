using System;

namespace SemPtr;

/// <summary>
/// Indicates that the source generator should generate <see langword="extension"/> members for all function pointer types for the specified <see langword="delegate"/> type,
/// using the specified <see cref="FunctionPointerExtensionsBaseAttribute.ExtensionsHostType"/> as the host type for the <see langword="extension"/> members
/// and the specified <see cref="FunctionPointerExtensionsBaseAttribute.InvokeMethodName"/> as the name of the method that will be generated for invoking the function pointer.
/// </summary>
/// <remarks>
/// <para>
/// Forces the source generator to generate <see langword="extension"/> members for all function pointer types for the specified <see langword="delegate"/> type.
/// </para>
/// <para>
/// You can specify the <see cref="FunctionPointerExtensionsBaseAttribute.ExtensionsHostType"/> property to indicate the type where the <see langword="extension"/> members should be generated.
/// If you don't specify this property, the <see langword="extension"/> members will be generated in the default extensions host type for function pointers,
/// which is either an <see langword="internal"/> type in the <see langword="global"/> <see langword="namespace"/> or the type in the compilation that is annotated with the <see cref="FunctionPointerDefaultExtensionsAttribute"/> attribute.
/// </para>
/// <para>
/// You can specify the <see cref="FunctionPointerExtensionsBaseAttribute.InvokeMethodName"/> property to indicate the name of the method that will be generated for invoking the function pointer.
/// If not specified, the name of the method will default to <see cref="FunctionPointerExtensionsBaseAttribute.DefaultInvokeMethodName"/> (<c>"Invoke"</c>).
/// </para>
/// <para>
/// You can use specify this attribute multiple times for the same <see langword="delegate"/> type to generate <see langword="extension"/> members in different host types or with different invoke method names.
/// </para>
/// <para>
/// Note that the target <see langword="delegate"/> type must be accessible from the host type for the <see langword="extension"/> members, otherwise it will result in a compilation error.
/// </para>
/// <para>
/// You can specify this attribute on <see langword="delegate"/> type declarations.
/// If you don't control the source code of the <see langword="delegate"/> type, you can alternatively specify a <see cref="FunctionPointerExtensionsAttribute{TDelegate}"/> attribute at the assembly level
/// to achieve the same effect with externally defined <see langword="delegate"/> types.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Delegate, AllowMultiple = true)]
public sealed class FunctionPointerExtensionsAttribute : FunctionPointerExtensionsBaseAttribute;

/// <summary>
/// Indicates that the source generator should generate <see langword="extension"/> members for all function pointer types for the specified <see langword="delegate"/> type,
/// using the specified <see cref="FunctionPointerExtensionsBaseAttribute.ExtensionsHostType"/> as the host type for the <see langword="extension"/> members
/// and the specified <see cref="FunctionPointerExtensionsBaseAttribute.InvokeMethodName"/> as the name of the method that will be generated for invoking the function pointer.
/// </summary>
/// <typeparam name="TDelegate">The type of the <see langword="delegate"/> for which the source generator should generate <see langword="extension"/> members for.</typeparam>
/// <remarks>
/// <para>
/// Forces the source generator to generate <see langword="extension"/> members for all function pointer types for the specified <typeparamref name="TDelegate"/>.
/// </para>
/// <para>
/// You can specify the <see cref="FunctionPointerExtensionsBaseAttribute.ExtensionsHostType"/> property to indicate the type where the <see langword="extension"/> members should be generated.
/// If you don't specify this property, the <see langword="extension"/> members will be generated in the default extensions host type for function pointers,
/// which is either an <see langword="internal"/> type in the <see langword="global"/> <see langword="namespace"/> or the type in the compilation that is annotated with the <see cref="FunctionPointerDefaultExtensionsAttribute"/> attribute.
/// </para>
/// <para>
/// You can specify the <see cref="FunctionPointerExtensionsBaseAttribute.InvokeMethodName"/> property to indicate the name of the method that will be generated for invoking the function pointer.
/// If not specified, the name of the method will default to <see cref="FunctionPointerExtensionsBaseAttribute.DefaultInvokeMethodName"/> (<c>"Invoke"</c>).
/// </para>
/// <para>
/// You can use specify this attribute multiple times with the same <typeparamref name="TDelegate"/> to generate <see langword="extension"/> members in different host types or with different invoke method names.
/// </para>
/// <para>
/// Note that the target <typeparamref name="TDelegate"/> must be accessible from the host type for the <see langword="extension"/> members, otherwise it will result in a compilation error.
/// </para>
/// <para>
/// You can specify this attribute with externally defined <see langword="delegate"/> types by applying it at the assembly level and as long as the <typeparamref name="TDelegate"/> is accessible from the assembly where the attribute is applied.
/// If you control the source code of the <typeparamref name="TDelegate"/>, you can alternatively specify a <see cref="FunctionPointerExtensionsAttribute"/> attribute on the <see langword="delegate"/> type declaration itself to achieve the same effect.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class FunctionPointerExtensionsAttribute<TDelegate> : FunctionPointerExtensionsBaseAttribute
	where TDelegate : notnull, Delegate;