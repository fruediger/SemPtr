using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SemPtr.SourceGeneration;
using System.Collections.Immutable;
using System.Text;

namespace SemPtr;

partial class FunctionPointerGenerator
{
	private static void GeneratePerFunctionPointerType(
		SourceProductionContext spc,
		in FunctionPointerCharacteristics characteristics,
		ImmutableArray<(INamedTypeSymbol delegateType, ImmutableArray<string> typeParamConstraints, IFunctionPointerTypeSymbol rawFunctionPointerType)> delegateTypeGroups,
		StringBuilder builder
	)
	{
		builder.Clear();

		var extensionsTypeName = string.Format(Config.GenerationFunctionPointerExtensionsTypeNameFormat, characteristics.ToTypeNameWithoutTypeParameter());

		// Each function pointer extensions host type is generated in a separate file for each function pointer type.
		// Each of those extensions host types is generated with the following characteristics:
		// - in the global namespace - That's so the function pointer extension members are always available everywhere, without needing to import a namespace.
		// - `internal` - That's so the function pointer extensions are only available to the assembly where they are generated. They should never be part of the public API of the assembly. Other assemblies using the same function pointer types should have their own generated function pointer extensions.
		// - `[Embedded]` - That's so the generated code doesn't clash with similar code generated in other assemblies. That plays into that each assembly using the same function pointer types should have their own generated function pointer extensions.

		builder.Append($$"""
			#nullable enable

			[global::System.CodeDom.Compiler.GeneratedCode("{{mTool.Name}}", "{{mTool.Version}}")]
			[global::Microsoft.CodeAnalysis.Embedded]
			internal static class {{extensionsTypeName}}
			{
			""");

		foreach (var (delegateType, typeParamConstraints, rawFunctionPointerType) in delegateTypeGroups)
		{
			GeneratePerDelegateType(
				spc,
				in characteristics,
				delegateType,
				typeParamConstraints,
				rawFunctionPointerType,
				builder
			);
		}

		builder.Append("""
			}

			#nullable restore
			""");

		spc.AddSource($"{extensionsTypeName}.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
