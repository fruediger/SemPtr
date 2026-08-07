using System;

namespace SemPtr;

/// <summary>
/// Specifies options for generating <see langword="extension"/> members for function pointer types paired with <see langword="delegate"/> types in the current compilation.
/// </summary>
/// <param name="generation">A value specifying for which <see langword="delegate"/> types <see langword="extension"/> members should be generated for function pointer types.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class FunctionPointerExtensionsOptionsAttribute(FunctionPointerGenerationOption generation) : Attribute
{
	/// <summary>
	/// Gets a value specifying for which <see langword="delegate"/> types <see langword="extension"/> members should be generated for function pointer types.
	/// </summary>
	/// <value>
	/// A value specifying for which <see langword="delegate"/> types <see langword="extension"/> members should be generated for function pointer types.
	/// </value>
	public FunctionPointerGenerationOption Generation => generation;
}
