namespace SemPtr;

/// <summary>
/// Represents an abstract pointer type pointing to a target function.
/// </summary>
public interface IFunctionPointer : IPointer;

/// <inheritdoc cref="IFunctionPointer"/>
/// <typeparam name="TSelf">The type of the function pointer.</typeparam>
public interface IFunctionPointer<TSelf> : IFunctionPointer, IPointer<TSelf>
	where TSelf : unmanaged, IFunctionPointer<TSelf>, allows ref struct;
