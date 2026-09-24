# Changelog

## [0.8.0](https://github.com/fruediger/SemPtr/releases/tag/v0.8.0) - 2026-09-24

Added raw pointer conversion support back for semantic data pointers.\
Semantic data pointers now can be implicitly converted from raw pointer and explicitly converted to raw pointer.

There's also a new public constructor on those types that accepts a raw pointer as an argument (which will replace the static `FromRaw` method on those types in the near future).

In addition to that data pointer types also gained equality and comparison members (methods and operators) to compare against raw pointers. An involved overload resolution priority mechanism ensures that non-nullable semantic pointers can be safely *compared* against `null` values without risking a runtime exception being thrown.

Lastly, since implicit conversion from raw pointers to non-nullable semantic pointers carries the risk of an exception being thrown if the raw pointer is `null`, there is now an additional static Roslyn analyzer shipped with the NuGet package that warns the user at compile time wherever such a conversion is used.\
There's even an additional code fix provider shipped with the NuGet package that can replace such implicit conversions with an explicit call to the throwing constructor of the non-nullable  pointer type.

## [0.7.1](https://github.com/fruediger/SemPtr/releases/tag/v0.7.1) - 2026-09-14

Annotated every custom marshaller type with `[EditorBrowsable(EditorBrowsableState.Never)]`, so they don't clutter IntelliSense for typical usage scenarios and they don't overwhelm the user with implementation details anymore.

This also removes them from the official API documentation, which is a fine compromise to make in light of that the custom marshallers should really be considered an implementation detail and are not intended to be used by consumers directly.

## [0.7.0](https://github.com/fruediger/SemPtr/releases/tag/v0.7.0) - 2026-09-10

Added custom marshalling for all semantic data and function pointer types.\
This enables them to be used seamlessly in `LibraryImport` scenarios as a drop-in replacement for raw pointers.

For each semantic data pointer type and function pointer type, there's a corresponding custom marshaller type provided that converts between the semantic pointer and the raw pointer representation.

To make sure that semantic pointers also can be used as a drop-in replacement in other interop scenarios (e.g., type-punned language function pointers, `delegate* unmanaged<PointerUninitialized<int>, PointerReadOnly<byte>>`, for example), tests for ABI compatibility have been added.\
So far, ABI compatibility between raw pointers and semantic pointers has been verified for Windows x64 and arm64, Linux x64 and arm64, and macOS arm64 and x64 platforms. Technically, all other platforms supported by .Net 10 should be compatible as well.

## [0.6.0](https://github.com/fruediger/SemPtr/releases/tag/v0.6.0) - 2026-09-07

Changed the `Target` properties of read-write and read-only data pointers from `ref`/`ref readonly` returning to traditional pass-by-value properties (e.g., `{get;set;}`/`{get;}`).

This change improves performance dramatically. A write to or read from the `Target` property now essentially produces the very same JIT'd binary as a direct access to a raw pointer would.

To still make it easy to get a language reference to the same target as the pointer points to, read-write and read-only data pointers now gained a `GetPinnableReference` method, essentially returning the same reference as the `Target` property previously did.\
This also enables semantic data pointers to be used *as source* in `fixed` statements.

## [0.5.0](https://github.com/fruediger/SemPtr/releases/tag/v0.5.0) - 2026-09-05

Initial official release of SemPtr.

Containing 48 semantic data pointer types, 8 semantic function pointer types, and a source generator publicly shipped to help with dynamic code generation for "typed" function pointers.
