# SemPtr

**SemPtr** or **Semantic Pointers** is a .NET library for overcoming the limitations of traditional pointers (i.e., raw pointers) in C#.\
It does so by *semantically naming* the pointer types it provides, categorizing them by some *semantic characteristics*, and using the strong type system of C# to enforce the correct usage of pointers at compile time as well as at runtime.

> [!NOTE]
> While this project aims to provide a more expressive and thereby safer alternative to using raw pointers in C#, there are still many scenarios where even semantic pointers can be misused or handled incorrectly. This project *can't possibly* and *won't try to* prevent all possible misuses.
> That being said, you should treat any kind of usage of pointers, even semantic pointers, as inherintly unsafe and should always be cautious when using them.

## How it works

### The inherent issue with raw pointers in C\#

C# was never designed to treat pointers as a first-class citizen, while still allowing for them to be used to enable low-level programming. While complete for almost all scenarios, the expressiveness of working with raw pointers in C# is very rudimentary and limited.

If we ignore the comprehensive function pointer syntax in C#, the only way to express a pointer type in C# is by the syntax `T*`, where `T` is either a CLR type, another pointer type, or `void`. There are no `const T*` or other variations of pointer types like in C/C++ to express the semantics of the pointer through the type system. This is a huge limitation.

These are some of the main limitations of raw pointers in C#:

- no variation or expressiveness in pointer types
- because of the former pointer, no enforcement of safety "guardrails" through the type system (e.g., no `const T*` to prevent modifications of the target)
- pointer types cant participate in all of C#'s type expressions (e.g., you can't use `T*` as a generic type argument)

> [!NOTE]
> While there are primarily limitations with C#'s pointer types, there are also some good things about them:
> For example, you're forced to use them in an `unsafe` context (although, there are some relaxations to this in the upcoming C# 15 release), which is a good thing because it forces the user to think about the implications of working with pointers.
> Sadly, this library can't replicate this enforcement, so users should always be aware when using semantic pointers and be equally cautious as they would be when using raw pointers.

### Solving the issue with semantic naming of custom pointer types

The solution to those limitations is to create a bunch of custom types that represent pointers, each with a specific semantic meaning, and let the type system enforce the correct usage. This is what **SemPtr** aims to do.

To achieve this, the library provides a set of pointer types that are *semantically named* to express the intended usage of the pointer. I.e., you can infer the semantics of the pointer type from its name, and, because of each characteristic of pointer being its own type, the strong type system of C# will do the rest.

For example, there's a `NullablePointerReadOnly<T>` type that represents a pointer that *can be `null`* and points to a *read-only* target of type *`T`*.

### Semantic characterization of pointer types

For that and for the semantic naming scheme, the semantics of the pointer types are broken down into a set of characteristics, which define the intent of the pointer type as well as its name.

Currently, **SemPtr** identifies 5 distinct characteristics of pointer types:

- **Nullability**: Whether the pointer can be `null` or not.\
  Possible manifestations:
  - **non-nullable**: The library does its best to ensure that such pointers can't be passed around as `null` at runtime.
    > Uses *no identification* in the name.
  - **nullable**: The pointer can be `null` and the user has to check for `null` before they can try to access its target.
    > Uses **`Nullable`** as an identification in the name.
- **Persistency**: Whether the target of the pointer can outlive the initial scope of the pointer or not.\
  Possible manifestations:
  - **transient**: The target is only guaranteed to be valid for the lifetime of the given pointer. The pointer can't escape its initial scope and can't be stored in a way that would allow the target to be accessed later.
    > Uses *no identification* in the name.
  - **persistent**: The target can outlive the initial scope of the pointer. The pointer can escape its initial scope and can be stored so that the target can be accessed later.
    > Uses **`Persistent`** as an identification in the name.
- **Sequencability**: Whether the pointer points to a single target or points to/into a sequence of targets.\
  Possible manifestations:
  - **object target**: The pointer points to a single object as its target. Only that target can be accessed through the pointer and pointer arithmetic is not allowed on such pointers.
    > Uses *no identification* in the name.
  - **sequence target**: The pointer points to a sequence of objects as its target, possibly at the start of such a sequence or at some target within the sequence. Other targets in the sequence can be accessed through pointer or using pointer arithmetic on such pointers.
    > Uses **`Sequence`** as an identification in the name.
- **Accessibility**: Whether the target of the pointer can be modified or not.\
  Possible manifestations:
  - **random**/**read-write**: The target can be read and modified through the pointer. The closest analogy in terms of C# references would be `ref`.
    > Uses *no identification* in the name.
  - **read-only**: The target can only be read through the pointer and can't be modified. The closest analogy in terms of C# references would be `ref readonly`/`in`.
    > Uses **`ReadOnly`** as an identification in the name.
  - **write-first**/**uninitialized**: The target must be written to first before it can be read. Most of the time, such targets should be written to, as the intent communicates that the target should be initialized. The closest analogy in terms of C# references would be `out`.
    > Uses **`Uninitialized`** as an identification in the name.
- **Typability**: Whether the type of the target is communicated through the pointer type or not.\
  Possible manifestations:
  - **untyped**: The type of the target is not specified. The target can't be accessed and the pointer must first be cast to a *typed* variant before trying to do so. Pointer arithmetic is also not allowed on such pointers as it wouldn't be well-defined.
    > Uses *no identification* in the name.
  - **typed**: The type of the target is specified and the target can be accessed through the pointer as a value of that type. Pointer arithmetic is well-defined on such pointers and can be used as long as the pointer is a *sequence* pointer.
    > Uses C# generic type parameters, e.g., **`<T>`** to specify the type of the target.

The combination of all possible manifestations of these characteristics then result in the semantic pointers that are provided by the library.

The naming scheme of individual pointer types is as follows:

`[Nullability][Persistency][Sequencability]Pointer[Accessibility][Typability]`

> where `[Nullability]` is either `Nullable` or empty, `[Persistency]` is either `Persistent` or empty, `[Sequencability]` is either `Sequence` or empty, `[Accessibility]` is either `ReadOnly`, `Uninitialized`, or empty, and `[Typability]` is either `<T>` (with `T` being the type of the target) or empty.

See the [data pointer usage examples](#data-pointer-usage-examples) section for more information on how to use those pointers in practice.

> [!NOTE]
> Please note that **lifetime management** and **ownership** are deliberately not part of the semantic characterization of pointer types.
>
> While it's true that those would make for good candidates for additional characteristics of pointer types, they would also be hard to achieve, if at all possible.
> That is why **SemPtr** intentionally does not implement those characteristics, at least not for now.
>
> **Lifetime management** is near close to impossible to achieve by language means alone. It is more of something that needs to be enforced at runtime, where it's still hard to do correctly.\
> Note that the **Persistency** characteristic of pointer types already handles some aspects of lifetime management and can be used to distinguish between short-lived and long-lived targets, which might be already sufficient for a good number of use cases.
>
> **Ownership** is also hard to achieve, at least in C#. While there would be some ways to implement ownership concepts in API, there's an equal amount of ways to bypass them, even accidentally. That's why introducing such a characteristic without the backing of the language itself would not really be worth it.

### Function pointers

Function pointers are supported in a very similar way to data pointers, although their implementation and usage is a bit different.

First and foremost, function pointers are also semantically named and categorized by some characteristics.

Currently, **SemPtr** identifies 3 distinct characteristics of function pointer types:
- **Nullability**: Whether the function pointer can be `null` or not.\
  *This is similar to the **nullability** characteristic of data pointers.*\
  Possible manifestations:
  - **non-nullable**: The library does its best to ensure that such function pointers can't be passed around as `null` at runtime.
    > Uses *no identification* in the name.
  - **nullable**: The function pointer can be `null` and the user has to check for `null` before they can try to invoke it.
    > Uses **`Nullable`** as an identification in the name.
- **Persistency**: Whether the function pointer can outlive the initial scope of the pointer or not.\
  *This is similar to the **persistency** characteristic of data pointers.*\
  Possible manifestations:
  - **transient**: The function pointer is only guaranteed to be valid for the lifetime of the given pointer. The pointer can't escape its initial scope and can't be stored in a way that would allow the target function to be invoked later.
    > Uses *no identification* in the name.
  - **persistent**: The function pointer can outlive the initial scope of the pointer. The pointer can escape its initial scope and can be stored so that the target function can be invoked later.
    > Uses **`Persistent`** as an identification in the name.
- **Typability**: Whether the signature of the function is communicated through the function pointer type or not.\
  *This is analogous to the **typability** characteristic of data pointers. Instead of providing the call signature of the target function directly as part of the function pointer type signature, function pointer types can accept a generic `delegate` type parameter that specifies the target function's signature.*\
  Possible manifestations:
  - **untyped**: The call signature of the target function is not specified. The function can't be invoked and the pointer must first be cast to a *typed* variant before trying to do so.
    > Uses *no identification* in the name.
  - **typed**: The call signature of the target function is specified by a `delegate` type argument and the target function can be invoked in the way specified by that signature, given the function pointer type is non-nullable.
    > Uses C# generic type parameters, e.g., **`<TDelegate>`** to specify the signature of the target function.

The combination of all possible manifestations of these characteristics then result in the semantic function pointers that are provided by the library.

The naming scheme of individual function pointer types is as follows:

`[Nullability][Persistency]FunctionPointer[Typability]`

> where `[Nullability]` is either `Nullable` or empty, `[Persistency]` is either `Persistent` or empty, and `[Typability]` is either `<TDelegate>` (with `TDelegate` being a `delegate` type that specifies the signature of the target function) or empty.

One could assume that there should be more characteristics of function pointers, especially regarding the granularity of the target function's call signature.
E.g., specifying the calling convention of the target function.\
However, that's entirely handled by the `delegate` type that is used in the function pointer type.
For example, you can specify the calling convention by annotating the `delegate` with an [`UnmanagedFunctionPointerAttribute`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedfunctionpointerattribute) or,
in the recommended way, by using [`FunctionPointerAttribute`](src/SemPtr.Common/FunctionPointerAttribute.cs#L27-L48) or [`FunctionPointerAttribute<TDelegate>`](src/SemPtr.Common/FunctionPointerAttribute.cs#L74-L96).\
See the [function pointer usage examples](#function-pointer-usage-examples) section for more information on how to use those attributes to configure the call signatures of function pointers and their target functions.

### What makes function pointers different from data pointers

Aside from the obvious semantic differences between data and function pointers, data pointers pointing to, well, data and function pointers pointing to executable code,
this project also handles them slightly differently in terms of their implementations.

While data pointers have their members (e.g., `Target`, `Raw`, `FromRaw`, etc.) implemented directly as members of their respective types, function pointers, at least *typed* ones, can't have this kind of easy-to-do implementation.

*Typed* function pointers have their target function's signature specified by a `delegate` type argument passed for their generic `TDelegate` type parameter.\
Therefore, their `Raw` and `FromRaw` members should have a signature based on the corresponding C# raw function pointer type, which is derived from the given `delegate` type argument.
For example, a `[FunctionPointer(CallConvs = [typeof(CallConvCdecl)])] delegate void MyFunction(int x, int y)` definition should result in a `delegate* unmanaged[Cdecl]<int, int, void>` raw function pointer type used as the return type/parameter type of the `Raw`/`FromRaw` members.\
Not to mention the `Invoke` member, which not only has to take the original signature of the `delegate` type into account, but must also correctly call the target function based on the calling convention specified for the `delegate` type by attributes like [`FunctionPointerAttribute`](src/SemPtr.Common/FunctionPointerAttribute.cs#L27-L48).\
Because of the nature of those things, this is not easily achievable in a "static" sense by the means of C#'s type system alone.

**SemPtr** solves this issue by shipping a source generator alongside the main library that handles exactly this in a "dynamic" way by producing the correct members and their implementations as `extension` members at design time.\
For that, the source generator scans the whole code for usages of `delegate`s and generates the correct `Raw`, `FromRaw`, and `Invoke` members for all function pointer types that use those `delegate`s.
Users don't even have to do anything to make this work, it just works out of the box *(well, sometimes, depending on the development environment you use, you need to save the source file containing such a usage to trigger the source generator to run)*.\
Sadly, this inheritly comes with some performance implications at design time. That's why **SemPtr** allows you to exactly specify for which kinds of usages the source generator should generate the `extension` members, mitigating some of the performance drawbacks.
See [`FunctionPointerGenerationAttribute`](src/SemPtr.Common/FunctionPointerGenerationAttribute.cs) for more information on how to configure the source generator.

Users of the NuGet package don't have to do anything in particular to make all of this work, as the source generator is included in the NuGet package and will be automatically installed when referencing the package.

## How to use

### Requirements

- .NET 10 or later
- C# 14 or later

### Installation

The library is available as a NuGet package and can be installed via various methods.

#### Using your IDE

*(Example for Visual Studio 2026)*

Open the NuGet Package Manager and search for `SemPtr`. As long as the library is in prerelease, make sure to check the "Include prerelease" checkbox. Then install the latest version.

#### Using the `.csproj` file of your project

Add the following `<PackageReference>` to an `<ItemGroup>` section of your `.csproj` file:

```xml
<PackageReference Include="SemPtr" Version="*-*" />
```

#### Using file-based C# apps

Add the following directive to the top of your `.cs` file:

```csharp
#:package SemPtr@*-*
```

#### Using the .NET CLI

Run the following command in your terminal:

```bash
dotnet add package SemPtr --version *-*
```

### Examples

This is by no means a comprehensive section of examples, but it should give you a general idea of how to use the library.

#### Data pointer usage examples

##### Reading, writing, and initializing targets

```csharp

// A random-access pointer to a target of type int.
Pointer<int> ptr = ...;

// Write to the target.
ptr.Target = 42;

// Read from the target.
var value = ptr.Target;

```

```csharp

// A read-only pointer to a target of type int.
PointerReadOnly<int> readOnlyPtr = ...;

// Read from the target.
var value = readOnlyPtr.Target;

```

```csharp

// An uninitialized pointer to a target of type int.
UninitializedPointer<int> uninitializedPtr = ...;

// Initialize the target and get a random-access pointer to it.
var ptr = uninitializedPtr.InitializeTarget(42);

// The random-access pointer can now be used to read the target...
var value = ptr.Target;

// ...or to write to the target again.
ptr.Target = 43;

```

##### Nullable pointers

```csharp

// A pointer that's possibly null and points to a target of type int.
NullablePointer<int> nullablePtr = ...;

// Check if the pointer is not null and get a non-nullable pointer to the same target if so.
if (nullablePtr.TryGetNonNull(out var ptr))
{
    // You can now use the non-nullable pointer to access the target.
    ptr.Target = 42;
}

```

##### Sequence pointers and pointer arithmetic

```csharp

// A pointer to a sequence of targets of type int.
SequencePointer<int> ptr = ...;

// You can access the target in the sequence that the pointer points to.
ptr.Target = 42;

// You can also access other targets adjacent to the current target by an offset.
ptr[4] = 43;

// Sequence pointers do not have to point to the start of a sequence, but can also point to a target within the sequence.
// In that case, you can also access targets before the current target by a negative offset.
ptr[-4] = 41;

```

```csharp

// A pointer to a sequence of targets of type int.
SequencePointer<int> ptr = ...;

// Moves the pointer 4 targets forward (4 ints in this case).
var aheadPtr = ptr + 4; // aheadPtr is a `SequencePointer<int>`

// Moves the pointer 4 targets backward (4 ints in this case).
var beforePtr = ptr - 4; // beforePtr is a `SequencePointer<int>`

// Calculate the number of targets between the two pointers (8 in this case). 
var offset = aheadPtr - beforePtr; // offset is a `nint`

```

```csharp

// A pointer to the start of a sequence of targets of type int and a pointer to the end of that sequence.
SequencePointer<int> start = ...;
ReadOnlyPointer<int> end = ...;

// Use the two pointers to iterate over the sequence of targets in a loop.
// Pointer arithmetic allows you to compare two pointers (address-wise) and to increment the iteration pointer to the next target.
for (var ptr = start; ptr < end; ptr++)
{
    // Do something with the current target in the sequence that the pointer points to.  
}

```

##### Typed and untyped pointers

```csharp

// An untyped pointer.
Pointer ptr = ...;

// You can't access the target of an untyped pointer (that would be not well-defined)...

// ...instead, you have to cast it to a typed pointer first, so that the type of the target becomes known.
var typedPtr = (Pointer<int>)ptr;

// Now you can access the target of the typed pointer.
typedPtr.Target = 42; 

```

```csharp

// A typed pointer to a target of type int.
Pointer<int> ptr = ...;

// The pointer accepts int values as the value for its target.
ptr.Target = 42;

// Type punning is technically possible by using an untyped pointer as an intermediate step.
// But you should be very careful when doing this, as it can easily lead to undefined behavior;
var floatPtr = (Pointer<float>)(Pointer)ptr;

// This is not 42, but some other value that is the result of interpreting the bit pattern of 42 as a float.
var floatValue = floatPtr.Target;

```

#### Function pointer usage examples

##### Simple function pointer usage

```csharp

// A delegate type specifying the signature of the target function.
delegate int MyFunction(int x, int y);

// A function pointer to a function matching the delegate type.
FunctionPointer<MyFunction> funcPtr = ...;

// You can invoke the target function with the correct signature.
// That's out-of-the-box, no additional information aside from the delegate type is needed.
var result = funcPtr.Invoke(1, 2);

```

##### Specifying the calling convention of the target function

The following two examples show the recommended way of specifying the calling convention of the target function
using [`FunctionPointerAttribute`](src/SemPtr.Common/FunctionPointerAttribute.cs#L27-L48) or [`FunctionPointerAttribute<TDelegate>`](src/SemPtr.Common/FunctionPointerAttribute.cs#L74-L96).

```csharp

// A delegate type specifying signature and calling convention of the target function.
[FunctionPointer(CallConvs = [typeof(CallConvCdecl)])]
delegate int MyFunction(int x, int y);

// A function pointer to a function matching the delegate type.
FunctionPointer<MyFunction> funcPtr = ...;

// That target function will be invoked with cdecl calling convention.
var result = funcPtr.Invoke(1, 2);

```

Sometimes, you might not control the definition of the `delegate` type and therefore can't specify the calling convention with an attribute applied to the `delegate` type definition.
For that case , you can use [`FunctionPointerAttribute<TDelegate>`](src/SemPtr.Common/FunctionPointerAttribute.cs#L74-L96) to specify the calling convention of the target function for a given `TDelegate` at assembly level.

```csharp

// Suppose an external dependencies defines a delegate type like this:
// delegate int MyFunction(int x, int y);

// You can specify the calling convention of the target function for that delegate type at assembly level,
// without the need to control or modify the delegate type's original definition.
[assembly: FunctionPointer<MyFunction>(CallConvs = [typeof(CallConvCdecl)])]

// A function pointer to a function matching the delegate type.
FunctionPointer<MyFunction> funcPtr = ...;

// That target function will be invoked with cdecl calling convention.
var result = funcPtr.Invoke(1, 2);

```

You can also use [`UnmanagedFunctionPointerAttribute`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedfunctionpointerattribute) to specify the calling convention of the target function,
although this is not the recommended way of doing so. This option exists primarily for existing `delegate` types that are already annotated with that attribute.\
Note that specifying [`FunctionPointerAttribute`](src/SemPtr.Common/FunctionPointerAttribute.cs#L27-L48) or [`FunctionPointerAttribute<TDelegate>`](src/SemPtr.Common/FunctionPointerAttribute.cs#L74-L96) for such a `delegate` type will override the calling convention specified by [`UnmanagedFunctionPointerAttribute`](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedfunctionpointerattribute).

```csharp

// A delegate type specifying signature and calling convention of the target function
// using an UnmanagedFunctionPointerAttribute (not recommended).
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int MyFunction(int x, int y);

// A function pointer to a function matching the delegate type.
FunctionPointer<MyFunction> funcPtr = ...;

// That target function will be invoked with cdecl calling convention.
var result = funcPtr.Invoke(1, 2);

```

##### Complex target function signatures

```csharp

// A delegate type with a more complex signature, including scoped ref parameters, default valued parameters, and ref-return.
delegate ref readonly int MyFunction(scoped ref int x, in int y, out int z, bool flag = true);

// Variable arguments declarations work too, although, like in C#, the target function must accept the `params` argument as a single parameter.
delegate void MyVarArgFunction(params ReadOnlySpan<int> args);

// A function pointer to a function matching the complex signature delegate type.
FunctionPointer<MyFunction> funcPtr = ...;

// A function pointer to a function matching the variable arguments delegate type.
FunctionPointer<MyVarArgFunction> varArgFuncPtr = ...;

// Parameter and return value modifiers are inherited from the delegate type as-is,
// so you can invoke the function pointer the same way you would invoke a delegate of that type.
ref readonly var refResult = ref funcPtr.Invoke(ref x, in y, out z);

// Variable arguments work as expected too.
// In this case, they will be passed as a single `ReadOnlySpan<int>` argument to the target function.
varArgFuncPtr.Invoke(1, 2, 3, 4, 5);

```

## A note on AI usage

In the spirit of transparency, and in line with the [contributing guidelines](CONTRIBUTING.md), here is an overview of how AI was used in this project:

- **Documentation.** AI was used to help write and improve documentation. The content itself comes from the author, but AI was used to clarify and clean up the writing.
- **Infrastructural documents.** AI was used to help write project documents such as this [README.md](README.md), the [CONTRIBUTING.md](CONTRIBUTING.md), and the [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
- **Tests.** AI was used to help write some of the tests for the library, but the author has verified that all tests are correct and meaningful.
- **Code review.** AI was used to review some of the author's code, and occasionally this turned out to be fruitful, catching bugs that might otherwise have been overlooked.
- **Functional code.** No AI was used to write any functional code. All code in this project was written by the author.

## License

SemPtr is licensed under the [MIT License](./LICENSE.md).
