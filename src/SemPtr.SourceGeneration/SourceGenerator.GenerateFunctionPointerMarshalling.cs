using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GenerateFunctionPointerMarshalling(IncrementalGeneratorPostInitializationContext pic, in FunctionPointerCharacteristics characteristics, StringBuilder builder)
	{
		var typeName = characteristics.ToTypeName();
		var typeNameWithoutTypeParameter = characteristics.ToTypeNameWithoutTypeParameter();
		var (typeNameWithEmptyTypeParameter, typeNameCRef) = characteristics.Typeability switch
		{
			Typeability.Typed => ($"{typeNameWithoutTypeParameter}<>", $"{typeNameWithoutTypeParameter}{{{Config.GenerationDelegateTypeParameterName}}}"),
			_ => (typeName, typeName)
		};

		var marshallerTypeNameWithoutTypeParameter = $"{typeNameWithoutTypeParameter}{Config.PointerMarshallerTypeNameSuffix}";
		var (marshallerTypeName, marshallerTypeNameWithEmptyTypeParameter, marshallerFileNamePrefix) = characteristics.Typeability switch
		{
			Typeability.Typed => ($"{marshallerTypeNameWithoutTypeParameter}<{Config.GenerationDelegateTypeParameterName}>", $"{marshallerTypeNameWithoutTypeParameter}<>", $"{marshallerTypeNameWithoutTypeParameter}_{Config.GenerationDelegateTypeParameterName}"),
			_ => (marshallerTypeNameWithoutTypeParameter, marshallerTypeNameWithoutTypeParameter, marshallerTypeNameWithoutTypeParameter)
		};

		builder.Clear();

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerMarshallingNamespaceName}};

			/// <summary>
			/// A marshaller for <see cref="{{typeNameCRef}}"/>.
			/// </summary>
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""
				/// <typeparam name="{{Config.GenerationDelegateTypeParameterName}}">The type of the <see langword="delegate"/> representing the signature of the target function that the <see cref="{{typeNameCRef}}"/> points to.</typeparam>
				""");
		}

		builder.Append($$"""

			[global::System.Runtime.InteropServices.Marshalling.CustomMarshaller(typeof({{typeNameWithEmptyTypeParameter}}), global::System.Runtime.InteropServices.Marshalling.MarshalMode.Default, typeof({{marshallerTypeNameWithEmptyTypeParameter}}))]
			public static class {{marshallerTypeName}}
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""

					where {{Config.GenerationDelegateTypeParameterName}} : notnull, global::System.Delegate
				""");
		}

		// Unlike data pointers, function pointers are always marshalled as `void*`, because we can't know the unmanaged function pointer signature at compile time,
		// as it's dependent on the delegate type argument, and we can't "fix" this with a shipped source generator and dynamically generated `extension` members in that case (like what we do for the `FromRaw`, `Raw`, and `Invoke` members).
		// And since the internal representation of our function pointer types is always just `void*`, we just marshal them as such.
		// The comment in `GenerateFunctionPointerDeclaration` already mentions that, but it's worth repeating here:
		// This relies on the assumption that for all supported platforms, function pointers and data pointers have the same size and alignment, and are ABI-wise indistinguishable (which is true for all the platforms that .NET currently supports).

		// Since typed function pointers don't expose their `FromRaw` and `Raw` members, we use a local static method to call their explicitly implemented interface methods.
		// For parity's sake and for simplicity's sake, we just do that for all function pointer types, even the untyped ones.

		builder.Append($$"""

			{
				/// <summary>
				/// Converts a raw function pointer to a <see cref="{{typeNameCRef}}"/>.
				/// </summary>
				/// <param name="raw">The raw function pointer to convert.</param>
				/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same target function as the given <paramref name="raw"/> function pointer.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static {{typeName}} ConvertToManaged(void* raw)
				{
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					static TPointer fromRaw<TPointer>(void* raw)
						where TPointer : unmanaged, {{Config.PointerInterfaceTypeName}}<TPointer>{{characteristics.Persistency switch { not Persistency.Persistent => ", allows ref struct", _ => string.Empty }}}
						=> TPointer.{{Config.PointerInterfaceTypeFromRawMethodName}}(raw);

					return fromRaw<{{typeName}}>(raw);
				}
			
				/// <summary>
				/// Converts a <see cref="{{typeNameCRef}}"/> to a raw function pointer.
				/// </summary>
				/// <param name="pointer">The <see cref="{{typeNameCRef}}"/> to convert.</param>
				/// <returns>A raw function pointer that points to the same target function as the given <paramref name="pointer"/>.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static void* ConvertToUnmanaged({{typeName}} pointer)
				{
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					static void* getRaw<TPointer>(TPointer {{Config.GenerationExtensionArgumentName}})
						where TPointer : unmanaged, {{Config.PointerInterfaceTypeName}}{{characteristics.Persistency switch { not Persistency.Persistent => ", allows ref struct", _ => string.Empty }}}
						=> {{Config.GenerationExtensionArgumentName}}.{{Config.PointerInterfaceTypeRawPointerPropertyName}};

					return getRaw(pointer);
				}
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerMarshallingNamespaceName}.{marshallerFileNamePrefix}.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Marshalling.g.cs", SourceText.From(
			text: $$"""
				#nullable enable

				namespace {{Config.PointerNamespaceName}};

				[global::System.Runtime.InteropServices.Marshalling.NativeMarshalling(typeof(global::{{Config.PointerMarshallingNamespaceName}}.{{marshallerTypeNameWithEmptyTypeParameter}}))]
				partial struct {{typeName}};

				#nullable restore
				""",
			encoding: Encoding.UTF8
		));
	}
}
