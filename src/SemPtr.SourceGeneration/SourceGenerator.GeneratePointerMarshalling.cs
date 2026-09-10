using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerMarshalling(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		var typeName = characteristics.ToTypeName();
		var typeNameWithoutTypeParameter = characteristics.ToTypeNameWithoutTypeParameter();
		var (typeNameWithEmptyTypeParameter, typeNameCRef) = characteristics.Typeability switch
		{
			Typeability.Typed => ($"{typeNameWithoutTypeParameter}<>", $"{typeNameWithoutTypeParameter}{{{Config.GenerationTypeParameterName}}}"),
			_ => (typeName, typeName)
		};

		var marshallerTypeNameWithoutTypeParameter = $"{typeNameWithoutTypeParameter}{Config.PointerMarshallerTypeNameSuffix}";
		var (marshallerTypeName, marshallerTypeNameWithEmptyTypeParameter, marshallerFileNamePrefix) = characteristics.Typeability switch
		{
			Typeability.Typed => ($"{marshallerTypeNameWithoutTypeParameter}<{Config.GenerationTypeParameterName}>", $"{marshallerTypeNameWithoutTypeParameter}<>", $"{marshallerTypeNameWithoutTypeParameter}_{Config.GenerationTypeParameterName}"),
			_ => (marshallerTypeNameWithoutTypeParameter, marshallerTypeNameWithoutTypeParameter, marshallerTypeNameWithoutTypeParameter)
		};

		var rawPointerType = characteristics.Typeability switch
		{
			Typeability.Typed => $"{Config.GenerationTypeParameterName}*",
			_ => "void*"
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

				/// <typeparam name="{{Config.GenerationTypeParameterName}}">The type of the target that the <see cref="{{typeNameCRef}}"/> points to.</typeparam>
				""");
		}

		builder.Append($$"""

			[global::System.Runtime.InteropServices.Marshalling.CustomMarshaller(typeof({{typeNameWithEmptyTypeParameter}}), global::System.Runtime.InteropServices.Marshalling.MarshalMode.Default, typeof({{marshallerTypeNameWithEmptyTypeParameter}}))]
			public static class {{marshallerTypeName}}
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""

					where {{Config.GenerationTypeParameterName}} : unmanaged{{characteristics switch { { Persistency: not Persistency.Persistent } => ", allows ref struct", _ => string.Empty }}}
				""");
		}

		builder.Append($$"""

			{
				/// <summary>
				/// Converts a raw pointer to a <see cref="{{typeNameCRef}}"/>.
				/// </summary>
				/// <param name="raw">The raw pointer to convert.</param>
				/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same target as the given <paramref name="raw"/> pointer.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static {{typeName}} ConvertToManaged({{rawPointerType}} raw) => {{typeName}}.{{Config.PointerInterfaceTypeFromRawMethodName}}(raw);
			
				/// <summary>
				/// Converts a <see cref="{{typeNameCRef}}"/> to a raw pointer.
				/// </summary>
				/// <param name="pointer">The <see cref="{{typeNameCRef}}"/> to convert.</param>
				/// <returns>A raw pointer that points to the same target as the given <paramref name="pointer"/>.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static {{rawPointerType}} ConvertToUnmanaged({{typeName}} pointer) => pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
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
