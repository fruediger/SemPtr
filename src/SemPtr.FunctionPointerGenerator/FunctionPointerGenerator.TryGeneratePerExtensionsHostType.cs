using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace SemPtr;

partial class FunctionPointerGenerator
{
	private static readonly SymbolDisplayFormat mNamespaceFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
	);

	private static readonly SymbolDisplayFormat mExtensionHostTypeFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
		/* genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters, */ // extension types can't be generic, so we don't need this
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
	);

	private static readonly SymbolDisplayFormat mHintNameTypeFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		/* genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters, */ // extension types (which are the foundation for the hint name) can't be generic, so we don't need this (and if they were generic, we'd need to replace the '<' and '>' characters)
		kindOptions: SymbolDisplayKindOptions.None
	);

	private static bool TryGeneratePerExtensionsHostType(
		SourceProductionContext spc,
		Compilation compilation,
		ITypeSymbol? extensionsHostType,
		IEnumerable<(INamedTypeSymbol delegateType, IEnumerable<(string invokeMethodName, IEnumerable<(ImmutableHashSet<INamedTypeSymbol>? callConvs, ImmutableArray<Location> attributeLocations)> callConvsGroups)> invokeMethodNameGroups)> delegateTypeGroups,
		FunctionPointerGenerationOption generation,
		(ITypeSymbol? type, bool isValid) defaultExtensionsHostType,
		INamedTypeSymbol? unmanagedFunctionPointerAttributeType,
		StringBuilder primaryBuilder,
		StringBuilder secondaryBuilder,
		[NotNullWhen(true)] out string? sourceHintName
	)
	{
		const string defaultExtensionsHostTypeName = "FunctionPointerExtensions";

		primaryBuilder.Append($$"""
			#nullable enable

			""");

		static bool tryGenerateSpecifiedExtensionsHostTypeDeclaration(SourceProductionContext spc, ITypeSymbol extensionsHostType, StringBuilder builder)
		{
			extensionsHostType = extensionsHostType.OriginalDefinition;

			if (extensionsHostType is not INamedTypeSymbol { IsStatic: true, IsReferenceType: true, IsRecord: false, ContainingType: null, IsGenericType: false }
				|| !extensionsHostType.DeclaringSyntaxReferences.Any(syntaxRef => (syntaxRef.GetSyntax(spc.CancellationToken) as BaseTypeDeclarationSyntax)?.Modifiers.Any(SyntaxKind.PartialKeyword) is true)
			)
			{
				// TODO: report diagnostic
				return false;
			}

			if (extensionsHostType.ContainingNamespace is { IsGlobalNamespace: false } @namespace)
			{
				builder.Append($$"""

					namespace {{@namespace.ToDisplayString(mNamespaceFormat)}}					

					""");
			}

			builder.Append($$"""

				partial class {{extensionsHostType.ToDisplayString(mExtensionHostTypeFormat)}}
				{
				""");

			return true;
		}

		if (extensionsHostType is null)
		{
			// This means that we should generate the extension methods in the default extensions host type.

			if (generation is not < FunctionPointerGenerationOption.ExplicitExtensionsHostTypeOnly)
			{
				// In this case we should not generate extension methods in the default extensions host type, so we return early.
				sourceHintName = null;
				return false;
			}

			if (defaultExtensionsHostType.isValid is false)
			{
				// We already reported a diagnostic for this case, so we just return early.
				sourceHintName = null;
				return false;
			}

			if (defaultExtensionsHostType.type is null)
			{
				// This means that we should generate the default default extensions host type.

				// The default default extensions host type is an internal class in the global namespace.

				primaryBuilder.Append($$"""

					/// <summary>
					/// Provides extension methods and properties for function pointer types.
					/// </summary>
					internal static partial class {{defaultExtensionsHostTypeName}}
					{
					""");
			}
			else
			{
				// We should generate the specified default extensions host type.

				if (!tryGenerateSpecifiedExtensionsHostTypeDeclaration(spc, defaultExtensionsHostType.type, primaryBuilder))
				{
					// We already reported a diagnostic, just return.
					sourceHintName = null;
					return false;
				}
			}
		}
		else
		{
			// We should generate the specified extensions host type.

			if (!tryGenerateSpecifiedExtensionsHostTypeDeclaration(spc, extensionsHostType, primaryBuilder))
			{
				// We already reported a diagnostic, just return.
				sourceHintName = null;
				return false;
			}
		}

		var result = false;
		foreach (var (delegateType, invokeMethodNameGroups) in delegateTypeGroups)
		{
			result |= TryGeneratePerDelegateType(
				spc,
				compilation,
				delegateType,
				invokeMethodNameGroups,
				unmanagedFunctionPointerAttributeType,
				primaryBuilder,
				secondaryBuilder
			);
		}

		if (result)
		{
			primaryBuilder.Append("""
				}

				#nullable restore
				""");

			sourceHintName = $"{extensionsHostType?.ToDisplayString(mHintNameTypeFormat) ?? defaultExtensionsHostTypeName}.g.cs"; // shouldn't contain any invalid characters for a file name, as the type is a top-level non-generic type
			return true;
		}

		sourceHintName = null;
		return false;
	}
}
