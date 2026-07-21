using Microsoft.CodeAnalysis;
using System.Text;

namespace SemPtr.SourceGeneration;

[Generator(LanguageNames.CSharp)]
internal sealed partial class SourceGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(static pic =>
		{
			var builder = new StringBuilder();

			foreach (var characteristics in PointerCharacteristics.Enumerate())
			{
				GeneratePointerDeclaration(pic, in characteristics, builder);
				GeneratePointerAccess(pic, in characteristics, builder);
				GeneratePointerArithmetic(pic, in characteristics, builder);
				GeneratePointerNullability(pic, in characteristics, builder);
				GeneratePointerEquatability(pic, in characteristics, builder);
				GeneratePointerComparability(pic, in characteristics, builder);
				GeneratePointerConversions(pic, in characteristics, builder);
				GeneratePointerFormatting(pic, in characteristics, builder);
				GeneratePointerDebuggerDisplay(pic, in characteristics, builder);
			}
		});
	}
}
