using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SemPtr.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SemPtr;

[Generator(LanguageNames.CSharp)]
internal sealed partial class FunctionPointerGenerator : IIncrementalGenerator
{
	private const string DiagnosticDescriptorIdPrefix = "SEMPTRFP";
	private const string DiagnosticDescriptorCategory = $"{nameof(SemPtr)}.{nameof(FunctionPointerGenerator)}";

	private static readonly (string Name, string Version) mTool = typeof(FunctionPointerGenerator).Assembly.GetName() switch { var assemblyName => (assemblyName?.Name ?? $"{nameof(SemPtr)}.{nameof(FunctionPointerGenerator)}", assemblyName?.Version?.ToString(3) ?? "0.0.0" ) };

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		context.RegisterPostInitializationOutput(static pic => pic.AddEmbeddedAttributeDefinition());

		var functionPointerGenerationAttributeApplications = context.SyntaxProvider
			.ForAttributeWithMetadataName($"{nameof(SemPtr)}.{nameof(FunctionPointerGenerationAttribute)}",
				predicate: static (node, _) => true, // Just like below, this works for assembly-level attributes, but might be a not so good idea performance-wise...
				transform: static (gasc, cancellationToken) => gasc.Attributes
					.Select(attr => attr switch
					{
						{ ConstructorArguments: [{ Kind: TypedConstantKind.Enum, Value: int generation }] } =>
						(
							generation: (FunctionPointerGeneration)generation,
							location: attr.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
						),
						_ => default((FunctionPointerGeneration generation, Location? location)?)
					})
					.OfType<(FunctionPointerGeneration generation, Location? location)>()
			)
			.SelectMany(static (tuples, _) => tuples)
			.Collect();

		var functionPointerAttributeApplications = context.SyntaxProvider
			.ForAttributeWithMetadataName($"{nameof(SemPtr)}.{nameof(FunctionPointerAttribute)}",
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
								callConvs: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerAttribute.CallConvs)).Value switch
								{
									{ IsNull: false, Kind: TypedConstantKind.Array, Values: var values } => values.Select(value => value.Value).OfType<INamedTypeSymbol>().ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default),
									_ => null
								},
								location: applicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
							),
						_ => default
					})
					.Where(static tuple => tuple.delegateType is not null)
			)
			.SelectMany(static (tuples, _) => tuples)
			.Collect();

		var functionPointerAttributeTDelegateApplications = context.SyntaxProvider
			.ForAttributeWithMetadataName($"{nameof(SemPtr)}.{nameof(FunctionPointerAttribute<>)}`1", // (if this looks weird: `nameof(FunctionPointerAttribute<>)` yields "FunctionPointerAttribute", not "FunctionPointerAttribute<>"; this is just to make sure that we take the name of the correct type)
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
							callConvs: namedArguments.FirstOrDefault(kvp => kvp.Key is nameof(FunctionPointerAttribute<>.CallConvs)).Value switch
							{
								{ IsNull: false, Kind: TypedConstantKind.Array, Values: var values } => values.Select(value => value.Value).OfType<INamedTypeSymbol>().ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default),
								_ => null
							},
							location: applicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
						),
						_ => default
					})
					.Where(static tuple => tuple.delegateType is not null)
			)
			.SelectMany(static (tuples, _) => tuples)
			.Collect();

		/* We move the function pointer usages detection into the source output action stage, because it is very expensive to do
		 * and there we can better control whether we need to do it or not based on the configuration given by attribute applications.
		 * This might impact when the source generator triggers.
		 * If it doesn't work well, we might need to go back, uncomment this, and find a better way of filtering.
		 * 
		var functionPointerUsages = context.CompilationProvider
			.SelectMany(static (compilation, cancellationToken) =>
			{
				var functionPointerTypes = FunctionPointerCharacteristics.Enumerate(typeability: Typeability.Typed)
					.Select(characteristics => compilation.GetTypeByMetadataName($"{Config.PointerNamespaceName}.{characteristics.ToMetadataName()}")?.OriginalDefinition)
					.Where(static type => type is not null)
					.Select(static type => type!)
					.ToImmutableHashSet((IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default);

				// What we're going to do here is very expensive, but that's intended.
				// We're trying to find all usages of function pointer types in the entire compilation,
				// so we need to look at all nodes in all syntax trees, and for each node, we need to determine if it has a type associated with it, and if that type is a function pointer type.
				// If you say "well just look at specific node types, like VariableDeclarationSyntax and others", 
				// sadly, that wouldn't be enough, since we explicitly want to find all usages, even implicit or non-obvious ones, like, for example, as part of an expression.
				// We, and our users, just have to live with the cost of this (we'll tell the users about it in the documentation).

				return compilation.SyntaxTrees
					.SelectMany(syntaxTree =>
					{
						var semanticModel = compilation.GetSemanticModel(syntaxTree);
						var root = syntaxTree.GetRoot(cancellationToken);

						return root.DescendantNodesAndSelf()
							.Select(node => (type: semanticModel.GetTypeInfo(node, cancellationToken).Type as INamedTypeSymbol, node))
							.Where(tuple => tuple.type is not null && functionPointerTypes.Contains(tuple.type.OriginalDefinition, SymbolEqualityComparer.Default))
							.Select(tuple =>
							(
								delegateType: tuple.type?.TypeArguments[0].OriginalDefinition switch // using TypeArguments[0] unchecked is fine here, because we already filtered in a way that only function pointer types having exactly one type parameter are considered
								{
									INamedTypeSymbol { ConstructedFrom: { } namedDelegateType } => namedDelegateType,
									INamedTypeSymbol namedDelegateType => namedDelegateType!,
									_ => null! // null-forgiving is fine here, because we filter out nulls later on; it's just to make the compiler happy without too much effort
								},
								location: (Location?)tuple.node.GetLocation() // we want to combine this with the other sources later, so we need to make sure that the location field is of the same nullability as the other sources
							))
							.Where(static tuple => tuple.delegateType is not null);
					})
					.Select(static tuple =>
					(
						tuple.delegateType,
						callConvs: (ImmutableHashSet<INamedTypeSymbol>?)null,
						tuple.location
					));
			})
			.Collect();
		*/

		context.RegisterSourceOutput(
			source: context.CompilationProvider.Combine(functionPointerGenerationAttributeApplications).Combine(functionPointerAttributeApplications).Combine(functionPointerAttributeTDelegateApplications)/*.Combine(functionPointerUsages)*/,
			action: static (spc, types) =>
			{
				var /*(*/(((compilation, functionPointerGenerationAttributeApplications), functionPointerAttributeApplications), functionPointerAttributeTDelegateApplications)/*, functionPointerUsages)*/ = types;

				Generate(spc, compilation, functionPointerGenerationAttributeApplications, functionPointerAttributeApplications, functionPointerAttributeTDelegateApplications/*, functionPointerUsages*/);
			}
		);
	}
}
