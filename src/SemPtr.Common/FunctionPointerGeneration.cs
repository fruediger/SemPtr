namespace SemPtr;

/// <summary>
/// When used with a <see cref="FunctionPointerGenerationAttribute"/>, specifies for which kinds of <see langword="delegate"/> types <see langword="extension"/> members for function pointer types should be generated.
/// </summary>
public enum FunctionPointerGeneration
{
	/// <summary>
	/// <see langword="extension"/> members for function pointer types should be generated for all <see langword="delegate"/> types, regardless of annotations.
	/// </summary>
	All = 0,

	/// <summary>
	/// <see langword="extension"/> members for function pointer types should be generated only for <see langword="delegate"/> types annotated with <see cref="FunctionPointerAttribute"/> or <see langword="delegate"/> types specified by <see cref="FunctionPointerAttribute{TDelegate}"/> at assembly level.
	/// No <see langword="extension"/> members should be generated for <see langword="delegate"/> types without any annotations.
	/// </summary>
	AttributeOnly = 1,

	/// <summary>
	/// No <see langword="extension"/> members for function pointer types are generated for any <see langword="delegate"/> types,
	/// including those annotated with <see cref="FunctionPointerAttribute"/>, specified by <see cref="FunctionPointerAttribute{TDelegate}"/> at assembly level,
	/// or without any annotations.
	/// </summary>
	None = int.MaxValue,
}
