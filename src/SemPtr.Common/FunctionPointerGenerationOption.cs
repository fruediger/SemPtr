namespace SemPtr;

/// <summary>
/// When used with a <see cref="FunctionPointerExtensionsOptionsAttribute"/>, specifies for which <see langword="delegate"/> types <see langword="extension"/> members should be generated for function pointer types.
/// </summary>
public enum FunctionPointerGenerationOption
{
	/// <summary>
	/// <see langword="extension"/> members should be generated for function pointer types for all <see langword="delegate"/> types, regardless of annotations.
	/// This includes <see langword="delegate"/> types without any annotations which have their corresponding <see langword="extension"/> members generated in the default function pointer extensions type (optionally specified via <see cref="FunctionPointerDefaultExtensionsAttribute"/>).
	/// </summary>
	All = 0,

	/// <summary>
	/// <see langword="extension"/> members should be generated for function pointer types only for <see langword="delegate"/> types annotated with <see cref="FunctionPointerExtensionsAttribute"/> or <see cref="FunctionPointerExtensionsAttribute{TDelegate}"/>.
	/// No <see langword="extension"/> members should be generated for <see langword="delegate"/> types without any annotations.
	/// </summary>
	WithAttributeOnly = 1,

	/// <summary>
	/// <see langword="extension"/> members should be generated for function pointer types only for <see langword="delegate"/> types annotated with <see cref="FunctionPointerExtensionsAttribute"/> or <see cref="FunctionPointerExtensionsAttribute{TDelegate}"/>,
	/// where the <see cref="FunctionPointerExtensionsBaseAttribute.ExtensionsHostType"/> property is set to a non-<see langword="null"/> value.
	/// In other words, <see langword="extension"/> members should be generated in extension host types that are not the default extension host type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This implies <see cref="WithAttributeOnly"/>.
	/// </para>
	/// </remarks>
	ExplicitExtensionsHostTypeOnly = 2,

	/// <summary>
	/// No <see langword="extension"/> members should be generated for function pointer types for any <see langword="delegate"/> types,
	/// neither for <see langword="delegate"/> types annotated with <see cref="FunctionPointerExtensionsAttribute"/> or <see cref="FunctionPointerExtensionsAttribute{TDelegate}"/>
	/// nor for <see langword="delegate"/> types without any annotations.
	/// </summary>
	None = int.MaxValue,
}
