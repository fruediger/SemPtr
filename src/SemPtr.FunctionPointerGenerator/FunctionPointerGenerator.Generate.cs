using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SemPtr;

partial class FunctionPointerGenerator
{
	private static void Generate(
		SourceProductionContext spc,
		Compilation compilation,
		ImmutableArray<(FunctionPointerGenerationOption generation, Location? attributeLocation)> fpExtensionsOptionsApplications,
		ImmutableArray<(INamedTypeSymbol type, ImmutableArray<Location> attributeLocations)> fpDefaultExtensionsApplications,
		ImmutableArray<(INamedTypeSymbol delegateType, ITypeSymbol? extensionsHostType, string invokeMethodName, ImmutableHashSet<INamedTypeSymbol>? callConvs, Location? attributeLocation)> fpExtensionsApplications,
		ImmutableArray<(INamedTypeSymbol delegateType, ITypeSymbol? extensionsHostType, string invokeMethodName, ImmutableHashSet<INamedTypeSymbol>? callConvs, Location? attributeLocation)> fpExtensionsAssemblyApplications,
		ImmutableArray<(INamedTypeSymbol delegateType, ITypeSymbol? extensionsHostType, string invokeMethodName, ImmutableHashSet<INamedTypeSymbol>? callConvs, Location? attributeLocation)> fpFunctionPointerUsages
	)
	{
		FunctionPointerGenerationOption generation;
		switch (fpExtensionsOptionsApplications)
		{
			case { IsDefaultOrEmpty: true }:
				{
					// FunctionPointerGenerationOption.All is the default
					generation = FunctionPointerGenerationOption.All;
				}
				break;

			case [var (generationOption, _)]:
				{
					generation = generationOption;
				}
				break;

			default: // more than one FunctionPointerExtensionsOptionsAttribute specified
					 // (Note that it should be impossible to specify the attribute multiple times because of `AllowMultiple = false`, but we still check for it just in case.)
				{
					// TODO: report diagnostic
				}
				return; // We have to fail early in this case, because we can't really know what to do.
		}

		if (generation is FunctionPointerGenerationOption.None)
		{
			// There's nothing for us to do here, so we can just return early.
			return;
		}

		(INamedTypeSymbol? type, bool isValid) defaultExtensionsHostType;
		switch (fpDefaultExtensionsApplications)
		{
			case { IsDefaultOrEmpty: true }:
				{
					defaultExtensionsHostType = (type: null, isValid: true);
				}
				break;

			case [var (type, attributeLocations)]:
				{
					if (attributeLocations.Length is > 1)
					{
						// We still count this as multiple default extensions host type specified,
						// we go to the default case below.
						// (Note that it should be impossible to specify the attribute multiple times on the same type because of `AllowMultiple = false`, but we still check for it just in case.)

						goto default;
					}

					defaultExtensionsHostType = (type, isValid: true);
				}
				break;

			default: // more than one default extensions host type specified
				{
					foreach (var (_, attributeLocations) in fpDefaultExtensionsApplications)
					{
						foreach (var attributeLocation in attributeLocations)
						{
							// TODO: report diagnostic for each attribute location
						}
					}

					defaultExtensionsHostType = (type: default, isValid: false);
				}
				break; // We don't fail early in this case, because there's a chance that we don't need the default extensions host type at all, if the user opted to generate only explicit function pointer extensions.
		}

		var fpExtensionsSources = fpExtensionsApplications.Concat(fpExtensionsAssemblyApplications);

		if (generation is < FunctionPointerGenerationOption.WithAttributeOnly)
		{
			fpExtensionsSources = fpExtensionsSources.Concat(fpFunctionPointerUsages);
		}

		var extensionHostTypeGroups = fpExtensionsSources
			.GroupBy(
				keySelector: static tuple => tuple.extensionsHostType,
				elementSelector: static tuple => (tuple.delegateType, tuple.callConvs, tuple.invokeMethodName, tuple.attributeLocation),
				resultSelector: static (extensionsHostType, tuples) => (extensionsHostType, delegateTypeGroups: tuples
					.GroupBy(
						keySelector: static tuple => tuple.delegateType,
						elementSelector: static tuple => (tuple.callConvs, tuple.invokeMethodName, tuple.attributeLocation),
						resultSelector: static (delegateType, tuples) => (delegateType, invokeMethodNameGroups: tuples
							.GroupBy(
								keySelector: static tuple => tuple.invokeMethodName,
								elementSelector: static tuple => (tuple.callConvs, tuple.attributeLocation),
								resultSelector: static (invokeMethodName, tuples) => (invokeMethodName, callConvsGroups: tuples
									.GroupBy(
										keySelector: static tuple => tuple.callConvs,
										elementSelector: static tuple => tuple.attributeLocation,
										resultSelector: static (callConvs, attributeLocations) => (callConvs, attributeLocations: attributeLocations
											.OfType<Location>()
											.Distinct()
											.ToImmutableArray()
										),
										comparer: (IEqualityComparer<ImmutableHashSet<INamedTypeSymbol>?>)SetEqualityComparer<INamedTypeSymbol>.Instance
									)
								),
								comparer: StringComparer.Ordinal
							)
						),
						comparer: (IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default
					)
					.OrderBy(static tuple => tuple.delegateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
				),
				comparer: (IEqualityComparer<ITypeSymbol?>)SymbolEqualityComparer.Default
			);

		var unmanagedFunctionPointerAttributeType = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute");
		var primaryBuilder = new StringBuilder();
		var secondaryBuilder = new StringBuilder();

		foreach (var (extensionsHostType, delegateTypeGroups) in extensionHostTypeGroups)
		{
			primaryBuilder.Clear();

			if (TryGeneratePerExtensionsHostType(
				spc,
				compilation,
				extensionsHostType,
				delegateTypeGroups,
				generation,
				defaultExtensionsHostType,
				unmanagedFunctionPointerAttributeType,
				primaryBuilder,
				secondaryBuilder,
				out var sourceHintName
			))
			{
				spc.AddSource(sourceHintName, SourceText.From(
					text: primaryBuilder.ToString(),
					encoding: Encoding.UTF8
				));
			}
		}
	}
}
