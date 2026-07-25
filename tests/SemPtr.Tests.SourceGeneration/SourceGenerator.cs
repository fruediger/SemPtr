// DISCLAIMER: This source file was generated with the help of AI (GitHub Copilot using Claude Sonnet 5)
using Microsoft.CodeAnalysis;
using SemPtr.SourceGeneration;
using System.Text;

namespace SemPtr.Tests.SourceGeneration;

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
				GenerateDeclarationTests(pic, in characteristics, builder);
				GenerateNullabilityTests(pic, in characteristics, builder);
				GenerateEquatabilityTests(pic, in characteristics, builder);
				GenerateComparabilityTests(pic, in characteristics, builder);
				GenerateArithmeticTests(pic, in characteristics, builder);
				GenerateAccessTests(pic, in characteristics, builder);
				GenerateFormattingTests(pic, in characteristics, builder);
				GenerateConversionTests(pic, in characteristics, builder);
			}

			foreach (var characteristics in FunctionPointerCharacteristics.Enumerate())
			{
				GenerateFunctionPointerDeclarationTests(pic, in characteristics, builder);
				GenerateFunctionPointerNullabilityTests(pic, in characteristics, builder);
				GenerateFunctionPointerEquatabilityTests(pic, in characteristics, builder);
				GenerateFunctionPointerFormattingTests(pic, in characteristics, builder);
			}
		});
	}
}


