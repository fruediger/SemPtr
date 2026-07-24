using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GenerateFunctionPointerDebuggerDisplay(IncrementalGeneratorPostInitializationContext pic, in FunctionPointerCharacteristics characteristics, StringBuilder builder)
	{
		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.DebuggerDisplay.g.cs", SourceText.From(
			text: $$$$"""
				#nullable enable

				using global::{{{{Config.InternalNamespaceName}}}};

				namespace {{{{Config.PointerNamespaceName}}}};

				[global::System.Diagnostics.DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
				partial struct {{{{characteristics.ToTypeName()}}}}
				{				
					[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
					[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
					private readonly string DebuggerDisplay => ToString(formatProvider: global::System.Globalization.CultureInfo.InvariantCulture);
				}

				#nullable restore
				""",
			encoding: Encoding.UTF8
		));
	}
}
