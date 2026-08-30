using System;

namespace SemPtr;

/// <summary>
/// Specifies options for generating <see langword="extension"/> members for function pointer types paired with <see langword="delegate"/> types in the current compilation.
/// </summary>
/// <param name="generation">A value specifying for which kinds of <see langword="delegate"/> types <see langword="extension"/> members for function pointer types should be generated.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class FunctionPointerGenerationAttribute(FunctionPointerGeneration generation) : Attribute
{
	/// <summary>
	/// Gets a value specifying for which kinds of <see langword="delegate"/> types <see langword="extension"/> members for function pointer types should be generated.
	/// </summary>
	/// <value>
	/// A value specifying for which kinds of <see langword="delegate"/> types <see langword="extension"/> members for function pointer types should be generated.
	/// </value>
	public FunctionPointerGeneration Generation => generation;
}
