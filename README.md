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

### Function pointers

Function pointers are currently not supported, but they're currently being worked on and will be added in a future release.\
They will be following the same general idea of semantic naming and characterization as data pointers, but with their own set of characteristics that are specific to function pointers.

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

#### Reading, writing, and initializing targets

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

#### Nullable pointers

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

#### Sequence pointers and pointer arithmetic

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

#### Typed and untyped pointers

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

## A note on AI usage

In the spirit of transparency, and in line with the [contributing guidelines](CONTRIBUTING.md), here is an overview of how AI was used in this project:

- **Documentation.** AI was used to help write and improve documentation. The content itself comes from the author, but AI was used to clarify and clean up the writing.
- **Infrastructural documents.** AI was used to help write project documents such as this [README.md](README.md), the [CONTRIBUTING.md](CONTRIBUTING.md), and the [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
- **Tests.** AI was used to help write some of the tests for the library, but the author has verified that all tests are correct and meaningful.
- **Code review.** AI was used to review some of the author's code, and occasionally this turned out to be fruitful, catching bugs that might otherwise have been overlooked.
- **Functional code.** No AI was used to write any functional code. All code in this project was written by the author.

## License

SemPtr is licensed under the [MIT License](./LICENSE.md).
