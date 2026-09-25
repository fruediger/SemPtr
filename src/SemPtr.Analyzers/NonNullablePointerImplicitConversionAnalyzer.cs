using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using SemPtr.Analyzers;
using System.Collections.Immutable;
using System.Linq;

namespace SemPtr;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NonNullablePointerImplicitConversionAnalyzer : DiagnosticAnalyzer
{
	private const string DiagnosticDescriptorCategory = $"{nameof(SemPtr)}.{nameof(NonNullablePointerImplicitConversionAnalyzer)}";

	private static readonly DiagnosticDescriptor mImplicitConversionFromRawPointerToNonNullPointerDiagnosticDescriptor = new(
		id: Diagnostics.ImplicitConversionFromRawPointerToNonNullPointerDiagnosticId,
		title: "Implicit conversion from raw pointer to non-nullable semantic pointer",
		messageFormat: "Implicit conversion from a raw pointer to a non-nullable semantic pointer of type '{0}' is not recommended, as this may lead to an ArgumentNullException being thrown at runtime." +
			" If you are certain about the raw pointer being non-null, you should use a constructor call to create the semantic pointer instead of relying on an implicit conversion, as this will make the intent more explicit.",
		category: DiagnosticDescriptorCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
	[
		mImplicitConversionFromRawPointerToNonNullPointerDiagnosticDescriptor
	];

	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static csac =>
		{
			var pointerConversions = PointerCharacteristics.Enumerate(nullability: Nullability.NonNull)
				.Select(characteristic => csac.Compilation.GetTypeByMetadataName($"{Config.PointerNamespaceName}.{characteristic.ToMetadataName()}")?.OriginalDefinition!)
				.Concat(FunctionPointerCharacteristics.Enumerate(nullability: Nullability.NonNull)
					.Select(characteristic => csac.Compilation.GetTypeByMetadataName($"{Config.PointerNamespaceName}.{characteristic.ToMetadataName()}")?.OriginalDefinition!)
				)
				.Where(static type => type is not null)
				.Select(static type =>
				(
					type,
					conversionOperator: type
						.GetMembers(WellKnownMemberNames.ImplicitConversionName)
						.OfType<IMethodSymbol>()
						.Select(static @operator => @operator.OriginalDefinition)
						.FirstOrDefault(@operator => @operator is { Parameters: [{ Type: IPointerTypeSymbol }], ReturnType.OriginalDefinition: var returnType } && returnType.Equals(type, SymbolEqualityComparer.Default))!
				))
				.Where(static tuple => tuple.conversionOperator is not null)
				.ToImmutableArray();

			if (pointerConversions.IsDefaultOrEmpty)
			{
				return;
			}

			csac.RegisterOperationAction(oac =>
			{
				// For the time being, we don't check for `IsImplicit: true` to also capture operations where the implicit conversion operator is used explicitly (e.g., a cast).
				// If we change our mind about that, and we truly only want to capture real implicit conversions, we can just uncomment the `IsImplicit: true` check below.

				if (oac.Operation is IConversionOperation { Conversion: { IsUserDefined: true, /*IsImplicit: true,*/ MethodSymbol.OriginalDefinition: var @operator }, Syntax: var syntax }
					&& pointerConversions.FirstOrDefault(tuple => tuple.conversionOperator.Equals(@operator, SymbolEqualityComparer.Default)) is (not null and var pointerType, _))
				{
					oac.ReportDiagnostic(Diagnostic.Create(
						mImplicitConversionFromRawPointerToNonNullPointerDiagnosticDescriptor,
						syntax.GetLocation(),
						pointerType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)
					));
				}
			}, OperationKind.Conversion);
		});
	}
}
