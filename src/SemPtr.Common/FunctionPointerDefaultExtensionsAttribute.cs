using System;

namespace SemPtr;

/// <summary>
/// Indicates that the annotated type is the default host type for <see langword="extension"/> members generated for function pointer types.
/// </summary>
/// <remarks>
/// <para>
/// <see langword="extension"/> members for <see langword="delegate"/> types used with function pointer types that don't explicitly specify a host type for their <see langword="extension"/> members will be generated in the type annotated with this attribute.
/// </para>
/// <para>
/// The type annotated with this attribute must be a valid type for hosting <see langword="extension"/> members (i.e., it must be a top-level <see langword="static"/> <see langword="class"/>)
/// and it must be declared <see langword="partial"/>. Otherwise, it will result in a compilation error.
/// </para>
/// <para>
/// Specifying this attribute on a type in a compilation will prevent the default <see langword="extension"/> host type for function pointers (an <see langword="internal"/> type in the <see langword="global"/> <see langword="namespace"/>) from being generated for that compilation.
/// </para>
/// <para>
/// Only one type in a compilation can be annotated with this attribute. If multiple types are annotated with this attribute, it will result in a compilation error.
/// </para>
/// <para>
/// The type annotated with this attribute will only be used as the default host type for <see langword="extension"/> members generated for function pointer types in the current compilation.
/// It will not be used as such in other compilations even if they reference the current one.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FunctionPointerDefaultExtensionsAttribute : Attribute;
