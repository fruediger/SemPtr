namespace SemPtr;

/// <summary>
/// Used to dispatch auto-generated extension methods for some pointer types <typeparamref name="TPointer"/>. Do not use from user code.
/// </summary>
/// <typeparam name="TPointer">The pointer type.</typeparam>
/// <remarks>
/// <para>
/// <em>You shouldn't use this interface directly from user code nor should you ever need to implement it.</em>
/// It is just used to differentiate some overloaded extension method signatures by an unused defaulted trailing parameter.
/// </para>
/// <para>
/// You do not need to pass arguments for parameters of this interface type when calling the auto-generated extension methods.
/// </para>
/// </remarks>
public interface IPointerDispatch<TPointer>
	where TPointer : unmanaged, IPointer, allows ref struct;
