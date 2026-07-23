using Microsoft.CodeAnalysis;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerDeclaration(IncrementalGeneratorPostInitializationContext pic, in FunctionPointerCharacteristics characteristics, StringBuilder builder)
	{
		// TODO: Continue implementing once the new Pointer API if finalized. That includes fixing the tests before continuing with the implementation.

		/*
		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var typeNameWithoutTypeParameter = characteristics.ToTypeNameWithoutTypeParameter();

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]
			public readonly {{characteristics.Persistency switch { not Persistency.Persistent => "ref ", _ => string.Empty }}}partial struct {{typeName}} : {{Config.PointerInterfaceTypeName}}
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""

					where {{Config.GenerationDelegateTypeParameterName}} : notnull, global::System.Delegate
				""");
		}

		// We store the function pointer as a raw void* pointer, because we can't declare the field as delegate*, lacking the parameter and return types.
		// That means we need to be sure that void* can represent function pointers on all platforms that .NET/Mono (because of MAUI/Xamarin) supports.
		// This should be true at the time of writing, but if .NET/Mono ever expands their support for new platforms, this should be re-evaluated.

		builder.Append($$"""

			{
				private unsafe readonly void* {{Config.GenerationRawPointerFieldName}};
			""");
		*/

	}
}
