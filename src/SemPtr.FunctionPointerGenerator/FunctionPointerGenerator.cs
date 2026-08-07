using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SemPtr.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SemPtr;

[Generator(LanguageNames.CSharp)]
internal sealed partial class FunctionPointerGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var fpExtensionsOptionsApplications = context.SyntaxProvider
			.ForAttributeWithMetadataName($"{nameof(SemPtr)}{nameof(FunctionPointerExtensionsOptionsAttribute)}",
				predicate: static (node, _) => true, // Just like below, this works for assembly-level attributes, but might be a not so good idea performance-wise...
				transform: static (gasc, cancellationToken) => gasc.Attributes
					.Select(attr => attr switch
					{
						{ ConstructorArguments: [{ Kind: TypedConstantKind.Enum, Value: int generation }] } =>
						(
							generation: (FunctionPointerGenerationOption)generation,
							attributeLocation: attr.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
						),
						_ => default((FunctionPointerGenerationOption generation, Location? attributeLocation)?)
					})
					.OfType<(FunctionPointerGenerationOption generation, Location? attributeLocation)>()
			)
			.SelectMany(static (tuples, _) => tuples)
			.Collect();

		var fpDefaultExtensionsApplications = context.SyntaxProvider
			.ForAttributeWithMetadataName($"{nameof(SemPtr)}.{nameof(FunctionPointerDefaultExtensionsAttribute)}",
				predicate: (node, _) => node is ClassDeclarationSyntax,
				transform: (gasc, cancellationToken) => gasc.TargetSymbol switch
				{
					// We just collect them all now and check later if there's more than one, because we can't report diagnostics just yet
					INamedTypeSymbol type =>
					(
						type,
						attributeLocations: gasc.Attributes
							.Select(attr => attr.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation())
							.OfType<Location>()
							.ToImmutableArray()
					),
					_ => default
				}
			)
			.Where(static tuple => tuple.type is not null)
			.Collect();

		var fpExtensionsApplications = context.SyntaxProvider
			.ForAttributeWithMetadataName($"{nameof(SemPtr)}.{nameof(FunctionPointerExtensionsAttribute)}",
				predicate: static (node, _) => node is DelegateDeclarationSyntax,
				transform: static (gasc, cancellationToken) => gasc.Attributes
					.Select(attr => attr switch
					{
						{ NamedArguments: var namedArguments, ApplicationSyntaxReference: var applicationSyntaxReference } =>
							(
								delegateType: (gasc.TargetSymbol as ITypeSymbol)?.OriginalDefinition switch
								{
									INamedTypeSymbol { ConstructedFrom: { } namedDelegateType } => namedDelegateType,
									INamedTypeSymbol namedDelegateType => namedDelegateType!,
									_ => null! // null-forgiving is fine here, because we filter out nulls later on; it's just to make the compiler happy without too much effort
								}, 
								extensionsHostType: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerExtensionsBaseAttribute.ExtensionsHostType)).Value.Type,
								invokeMethodName: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerExtensionsBaseAttribute.InvokeMethodName)).Value.Value as string ?? FunctionPointerExtensionsBaseAttribute.DefaultInvokeMethodName,
								callConvs: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerExtensionsBaseAttribute.CallConvs)).Value switch
								{
									{ IsNull: false, Kind: TypedConstantKind.Array, Values: var values } => values.Select(value => value.Value).OfType<INamedTypeSymbol>().ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default),
									_ => null
								},
								attributeLocation: applicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
							),
						_ => default
					})
					.Where(static tuple => tuple.delegateType is not null)
			)
			.SelectMany(static (tuples, _) => tuples)
			.Collect();

		var fpExtensionsAssemblyApplications = context.SyntaxProvider
			.ForAttributeWithMetadataName($"{nameof(SemPtr)}.{nameof(FunctionPointerExtensionsAttribute)}`1",
				predicate: static (_, _) => true, // I mean this works, but we should find out how such a filter should look like for assembly-level attributes...
				transform: static (gasc, cancellationToken) => gasc.Attributes
					.Select(attr => attr switch
					{
						{ AttributeClass.TypeArguments: [var delegateType], NamedArguments: var namedArguments, ApplicationSyntaxReference: var applicationSyntaxReference } =>
						(
							delegateType: delegateType?.OriginalDefinition switch
							{
								INamedTypeSymbol { ConstructedFrom: { } namedDelegateType } => namedDelegateType,
								INamedTypeSymbol namedDelegateType => namedDelegateType!,
								_ => null! // null-forgiving is fine here, because we filter out nulls later on; it's just to make the compiler happy without too much effort
							},
							extensionsHostType: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerExtensionsBaseAttribute.ExtensionsHostType)).Value.Type,
							invokeMethodName: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerExtensionsBaseAttribute.InvokeMethodName)).Value.Value as string ?? FunctionPointerExtensionsBaseAttribute.DefaultInvokeMethodName,
							callConvs: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerExtensionsBaseAttribute.CallConvs)).Value switch
							{
								{ IsNull: false, Kind: TypedConstantKind.Array, Values: var values } => values.Select(value => value.Value).OfType<INamedTypeSymbol>().ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default),
								_ => null
							},
							attributeLocation: applicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
						),
						_ => default
					})
					.Where(static tuple => tuple.delegateType is not null)
			)
			.SelectMany(static (tuples, _) => tuples)
			.Collect();

		var fpFunctionPointerUsages = context.CompilationProvider
			.SelectMany(static (compilation, cancellationToken) =>
			{
				var functionPointerTypes = FunctionPointerCharacteristics.Enumerate(typeability: Typeability.Typed)
					.Select(characteristics => compilation.GetTypeByMetadataName($"{Config.PointerNamespaceName}.{characteristics.ToMetadataName()}")?.OriginalDefinition)
					.Where(static type => type is not null)
					.Select(static type => type!)
					.ToImmutableHashSet((IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default);

				return compilation.SyntaxTrees
					.SelectMany(syntaxTree =>
					{
						var semanticModel = compilation.GetSemanticModel(syntaxTree);
						var root = syntaxTree.GetRoot(cancellationToken);

						return root.DescendantNodesAndSelf()
							.Select(node => semanticModel.GetTypeInfo(node, cancellationToken).Type as INamedTypeSymbol)
							.Where(type => type is not null && functionPointerTypes.Contains(type.OriginalDefinition, SymbolEqualityComparer.Default))
							.Select(type => type?.TypeArguments[0].OriginalDefinition switch
							{
								INamedTypeSymbol { ConstructedFrom: { } namedDelegateType } => namedDelegateType,
								INamedTypeSymbol namedDelegateType => namedDelegateType!,
								_ => null! // null-forgiving is fine here, because we filter out nulls later on; it's just to make the compiler happy without too much effort
							})
							.Where(static delegateType => delegateType is not null);
					})
					.Select(static delegateType =>
					(
						delegateType,
						extensionsHostType: default(ITypeSymbol?),
						invokeMethodName: FunctionPointerExtensionsBaseAttribute.DefaultInvokeMethodName,
						callConvs: (ImmutableHashSet<INamedTypeSymbol>?)null,
						attributeLocation: default(Location?)
					));
			})
			.Collect();

		context.RegisterSourceOutput(
			source: context.CompilationProvider.Combine(fpExtensionsOptionsApplications).Combine(fpDefaultExtensionsApplications).Combine(fpExtensionsApplications).Combine(fpExtensionsAssemblyApplications).Combine(fpFunctionPointerUsages),
			action: static (spc, types) =>
			{
				var (((((compilation, fpExtensionsOptionsApplications), fpDefaultExtensionsApplications), fpExtensionsApplications), fpExtensionsAssemblyApplications), fpFunctionPointerUsages) = types;

				Generate(
					spc,
					compilation,
					fpExtensionsOptionsApplications,
					fpDefaultExtensionsApplications,
					fpExtensionsApplications,
					fpExtensionsAssemblyApplications,
					fpFunctionPointerUsages
				);
			}
		);
	}
}
