using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using SemPtr.Analyzers;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

namespace SemPtr;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NonNullablePointerImplicitConversionCodeFixProvider)), Shared]
internal sealed class NonNullablePointerImplicitConversionCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds =>
	[
		Diagnostics.ImplicitConversionFromRawPointerToNonNullPointerDiagnosticId
	];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var document = context.Document;

		if (await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not { } root
			|| context.Diagnostics is not [var diagnostic, ..])
		{
			return;
		}

		var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

		context.RegisterCodeFix(CodeAction.Create(
			title: "Use explicit constructor call instead of implicitly converting from a raw pointer to a non-nullable semantic pointer",
			createChangedDocument: async cancellationToken =>
			{
				if (await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } model
					|| node is not ExpressionSyntax operandExpr)
				{
					return document;
				}

				// If we at some point decide to no longer apply the diagnostic to "explicit" casts as well (see `NonNullablePointerImplicitConversionAnalyzer.cs`),
				// we should remove the block below, and instead just return early when the operand is a `CastExpressionSyntax` (i.e., the implicit conversion operator is used explicitly).

				if (operandExpr is CastExpressionSyntax castExpr)
				{
					// If the operand so far is a cast expression, this means that the implicit conversion is applied as an explicit cast,
					// and since it's not implicit, we need to take the inner expression of the cast as the operand.

					operandExpr = castExpr.Expression;
				}

				// Make sure to get the actual target type of the conversion.

				if ((model.GetTypeInfo(node, cancellationToken).Type ?? model.GetTypeInfo(operandExpr, cancellationToken).ConvertedType) is not { } targetType)
				{
					// Can this even fail? Well, it cannot hurt to be safe...

					return document;
				}

				var generator = SyntaxGenerator.GetGenerator(document);

				return document.WithSyntaxRoot(
					root.ReplaceNode(
						node,
						generator.ObjectCreationExpression(
							generator.TypeExpression(targetType),
							operandExpr.WithoutTrivia()
						).WithTriviaFrom(node)
					)
				);
			},
			equivalenceKey: nameof(NonNullablePointerImplicitConversionCodeFixProvider)
		), diagnostic);
	}
}
