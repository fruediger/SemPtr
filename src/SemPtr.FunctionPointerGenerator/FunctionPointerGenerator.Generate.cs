using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SemPtr.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;

namespace SemPtr;

partial class FunctionPointerGenerator
{
	private static readonly DiagnosticDescriptor mLaterCSharpVersionRequiredDescriptor = new(
		id: $"{DiagnosticDescriptorIdPrefix}0101",
		title: "Later C# version is required",
		messageFormat: 
			$"A later C# version is required for {nameof(SemPtr)}.{nameof(FunctionPointerGenerator)} to generate code." +
			" Minimum required C# version: {0}.",
		category: DiagnosticDescriptorCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor mMultipleFunctionPointerGenerationAttributesDescriptor = new(
		id: $"{DiagnosticDescriptorIdPrefix}0201",
		title: "Function pointer generation specified more than once",
		messageFormat:
			$"The {nameof(FunctionPointerGenerationAttribute)} is specified more than once for the same assembly." +
			$" The {nameof(FunctionPointerGenerationAttribute)} can only be specified once.",
		category: DiagnosticDescriptorCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor mConflictingFunctionPointerCallingConventionsDescriptor = new(
		id: $"{DiagnosticDescriptorIdPrefix}0202",
		title: "Conflicting function pointer calling conventions specified",
		messageFormat:
			"Conflicting function pointer calling conventions are specified for the delegate type '{0}': {1}." +
			$" Function pointer calling conventions must be consistent across all applications of {nameof(FunctionPointerAttribute)} and {nameof(FunctionPointerAttribute<>)}<{Config.GenerationDelegateTypeParameterName}> attributes for the same delegate type (e.g., not specified, null, or the same calling conventions).",
		category: DiagnosticDescriptorCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly DiagnosticDescriptor mCouldNotCreateRawFunctionPointerTypeDescriptor = new(
		id: $"{DiagnosticDescriptorIdPrefix}0301",
		title: "Could not create raw function pointer type",
#pragma warning disable RS1032 // Not ending in a period here, because the argument for {2} is the exception's message, and most exception messages already end in a period (and if they don't, that wouldn't be too much of an issue)
		messageFormat:
			"Could not create a raw function pointer type for the delegate type '{0}' with the specified calling conventions." +
			" This could be due to invalid or unsupported calling conventions, or other issues with the delegate type's signature." +
			$" {nameof(Compilation.CreateFunctionPointerTypeSymbol)}: [{{1}}] {{2}}",
#pragma warning restore RS1032 
		category: DiagnosticDescriptorCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	private static readonly SymbolDisplayFormat mExtractConstraintsFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
		delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.ExpandValueTuple
	);

	private static void Generate(
		SourceProductionContext spc,
		Compilation compilation,
		ImmutableArray<(FunctionPointerGeneration generation, Location? location)> functionPointerGenerationAttributeApplications,
		ImmutableArray<(INamedTypeSymbol delegateType, ImmutableHashSet<INamedTypeSymbol>? callConvs, Location? location)> functionPointerAttributeApplications,
		ImmutableArray<(INamedTypeSymbol delegateType, ImmutableHashSet<INamedTypeSymbol>? callConvs, Location? location)> functionPointerAttributeTDelegateApplications/*,
		ImmutableArray<(INamedTypeSymbol delegateType, ImmutableHashSet<INamedTypeSymbol>? callConvs, Location? location)> functionPointerUsages*/
	)
	{
		if (compilation is not CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp14 /* needed for `extension` members syntax, `allows ref struct` constraints, etc. */ })
		{
			spc.ReportDiagnostic(Diagnostic.Create(
				mLaterCSharpVersionRequiredDescriptor,
				location: Location.None,
				LanguageVersion.CSharp14.ToDisplayString()
			));

			return; // We shouldn't generate any code if the C# language version is not high enough to support the generated code
		}

		var unmanagedFunctionPointerAttributeType = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.UnmanagedFunctionPointerAttribute");

		FunctionPointerGeneration generation;
		switch (functionPointerGenerationAttributeApplications)
		{
			case { IsDefaultOrEmpty: true }:
				generation = FunctionPointerGeneration.All; // `FunctionPointerGenerationOption.All` is the default
				break;

			case [var (generationOption, _)]:
				generation = generationOption;
				break;

			default: // more than one `FunctionPointerGenerationAttribute` specified; this should be technically impossible, because the attribute is defined with `AllowMultiple = false`
				var report = false;
				foreach (var (_, location) in functionPointerGenerationAttributeApplications)
				{
					if (location is not null)
					{
						spc.ReportDiagnostic(Diagnostic.Create(
							mMultipleFunctionPointerGenerationAttributesDescriptor,
							location
						));
						report = true;
					}
				}

				if (!report)
				{
					// If we didn't report any diagnostics, then we have to report a diagnostic without a definitive location.
					spc.ReportDiagnostic(Diagnostic.Create(
						mMultipleFunctionPointerGenerationAttributesDescriptor,
						location: Location.None
					));
				}

				return; // We have to fail early in this case, because we can't really know what to do.
		}

		if (generation is FunctionPointerGeneration.None)
		{
			return; // There's nothing for us to do here, so we can just return early.
		}

		var functionPointerExtensionsGenerationSources = functionPointerAttributeApplications.Concat(functionPointerAttributeTDelegateApplications);

		if (generation is < FunctionPointerGeneration.AttributeOnly)
		{
			// Have a look at the comment in `FunctionPointerGenerator.cs`.
			// Again, if this doesn't work out (well), we need to remove the code added below, and go back to the previous approach.

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

			var functionPointerUsages = compilation.SyntaxTrees
				.SelectMany(syntaxTree =>
				{
					var semanticModel = compilation.GetSemanticModel(syntaxTree);
					var root = syntaxTree.GetRoot(spc.CancellationToken);

					return root.DescendantNodesAndSelf()
						.Select(node => (type: semanticModel.GetTypeInfo(node, spc.CancellationToken).Type as INamedTypeSymbol, node))
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

			functionPointerExtensionsGenerationSources = functionPointerExtensionsGenerationSources.Concat(functionPointerUsages);
		}

		var delegateTypeGroups = functionPointerExtensionsGenerationSources
			.Where(static tuple => tuple.delegateType.DelegateInvokeMethod is not null) // Making sure we're dealing with valid delegate types, because we will need the `DelegateInvokeMethod` later on
			.GroupBy(
				keySelector: static tuple => tuple.delegateType,
				elementSelector: static tuple => (tuple.callConvs, tuple.location),
				resultSelector: static (delegateType, tuples) => (delegateType, callConvsGroups: tuples
					.GroupBy(
						keySelector: static tuple => tuple.callConvs,
						elementSelector: static tuple => tuple.location,
						resultSelector: static (callConvs, locations) => (callConvs, locations: locations
							.OfType<Location>()
							.Distinct()
							.ToImmutableArray()
						),
						comparer: (IEqualityComparer<ImmutableHashSet<INamedTypeSymbol>?>)SetEqualityComparer<INamedTypeSymbol>.Instance // `SetEqualityComparer<>` uses the equality comparer that was set for the left-hand-side `ISet<>?` argument of the comparison (i.e., `SymbolEqualityComparer.Default` in our case);
																																		 // cast to keep the `ImmutableHashSet<INamedTypeSymbol>?` type of the key, otherwise it would degenerate to just `ISet<INamedTypeSymbol>?`
					)
					.ToImmutableArray() // We need to materialize the result, because we will use list pattern matching on it later on
				),
				comparer: (IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default // cast to keep the `INamedTypeSymbol` type of the key, otherwise it would degenerate to just `ISymbol`
			)
			.OrderBy(static tuple => tuple.delegateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal) // Do we need a dedicate SymbolDisplayFormat for this?
			.Select(tuple =>
			{
				var delegateInvokeMethod = tuple.delegateType.DelegateInvokeMethod!; // `DelegateInvokeMethod` is guaranteed to be non-null because of the `Where` clause above

				// Extract and collect the constraints of the delegate type's type parameters, if any, in a way that lets us easily append them later to the `extension` declarations

				var builder = ImmutableArray.CreateBuilder<string>(initialCapacity: tuple.delegateType.TypeParameters.Length);

				var extractedConstraintsPart = tuple.delegateType.ToDisplayParts(mExtractConstraintsFormat);

				var lastIndex = -1;
				for (var index = 0; index < extractedConstraintsPart.Length; index++)
				{
					var part = extractedConstraintsPart[index];
					if (part.Kind is SymbolDisplayPartKind.Keyword && part.ToString() is "where")
					{
						if (lastIndex is > 0)
						{
							builder.Add(extractedConstraintsPart[lastIndex..index].ToDisplayString());
						}

						lastIndex = index;
					}
				}

				if (lastIndex is not -1)
				{
					builder.Add(extractedConstraintsPart[lastIndex..].ToDisplayString());
				}

				var typeParamConstraints = builder.ToImmutable();

				// Get the calling convention specified by the `FunctionPointerAttribute` or `FunctionPointerAttribute<TDelegate>` for the delegate type, if any

				ImmutableHashSet<INamedTypeSymbol>? specifiedCallConvs;
				switch (tuple.callConvsGroups)
				{
					case { IsDefaultOrEmpty: true } or [(null, _)]:
						// No calling conventions specified or explicitly specified to be null
						specifiedCallConvs = null;
						break;

					case [(not null and var callConvs, _)]:
						// Calling conventions explicitly specified
						specifiedCallConvs = callConvs;
						break;

					case [(not null and var callConvs, _), (null, _)]:
						// Calling conventions were specified explicitly in one place and other places specified them to be null, which is totally fine, as this just means that the explicitly specified ones should be used
						specifiedCallConvs = callConvs;
						break;

					case [(null, _), (not null and var callConvs, _)]:
						// The same as the previous case; just making sure that the order doesn't matter
						specifiedCallConvs = callConvs;
						break;

					default:
						// This means there were multiple different calling conventions specified. We wouldn't know which one to use, so this is an error.

						// Since we're reporting diagnostics from a `Select` delegate, we must make sure that the resulting enumeration is actually enumerated, but that should be the case considering that we're materializing the result.

						var report = false;
						foreach (var (callConvs, locations) in tuple.callConvsGroups)
						{
							foreach (var location in locations)
							{
								spc.ReportDiagnostic(Diagnostic.Create(
									mConflictingFunctionPointerCallingConventionsDescriptor,
									location,
									tuple.delegateType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
									callConvs is not null
										? string.Join(", ", callConvs.Select(static callConv => callConv.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)))
										: "null"
								));
								report = true;
							}
						}

						if (!report)
						{
							// If we didn't report any diagnostics, then we have to report a diagnostic without a definitive location.
							spc.ReportDiagnostic(Diagnostic.Create(
								mConflictingFunctionPointerCallingConventionsDescriptor,
								location: Location.None,
								tuple.delegateType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
								string.Join(", ", tuple.callConvsGroups.Select(static tuple => tuple.callConvs is not null
									? $"[ {string.Join(", ", tuple.callConvs.Select(static callConv => callConv.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)))} ]"
									: "null"))
							));
						}

						// We don't fail completely here, because there might be other delegate types that are validly configured, so we just filter out the bad ones.

						return default; // Yields `null` for the `delegateType` field, which we will filter out later on
				}

				IFunctionPointerTypeSymbol rawFunctionPointerType;
				try
				{
					if (specifiedCallConvs is not null)
					{
						rawFunctionPointerType = compilation.CreateFunctionPointerTypeSymbol(
							returnType: delegateInvokeMethod.ReturnsVoid
								? compilation.GetSpecialType(SpecialType.System_Void)
								: delegateInvokeMethod.ReturnType,
							returnRefKind: delegateInvokeMethod.ReturnsByRef
								? RefKind.Ref
								: delegateInvokeMethod.ReturnsByRefReadonly
									? RefKind.RefReadOnly
									: RefKind.None,
							parameterTypes: [.. delegateInvokeMethod.Parameters.Select(static param => param.Type)],
							parameterRefKinds: [.. delegateInvokeMethod.Parameters.Select(static param => param.RefKind)],
							callingConvention: SignatureCallingConvention.Unmanaged, // Has to be unmanaged in this case (because we specify calling conventions through a CallConv* types array).
							callingConventionTypes: [.. specifiedCallConvs]
						);
					}
					else
					{
						// Get the calling convention specified by the `UnmanagedFunctionPointerAttribute` for the delegate type, if any. We use this as a fallback if `FunctionPointerAttribute` or `FunctionPointerAttribute<TDelegate>` didn't specify any calling conventions.

						var unmanagedFunctionPointerAttributeCallConv = unmanagedFunctionPointerAttributeType is not null
							? tuple.delegateType.GetAttributes()
								.Where(attr => attr.AttributeClass?.OriginalDefinition.Equals(unmanagedFunctionPointerAttributeType, SymbolEqualityComparer.Default) is true)
								.Select(attr => attr.ConstructorArguments switch
								{
									[{ Kind: TypedConstantKind.Enum, Value: int callConv }] => (CallingConvention)callConv,
									_ => default(CallingConvention?),
								})
								.FirstOrDefault()
							: default;

						rawFunctionPointerType = compilation.CreateFunctionPointerTypeSymbol(
							returnType: delegateInvokeMethod.ReturnsVoid
								? compilation.GetSpecialType(SpecialType.System_Void)
								: delegateInvokeMethod.ReturnType,
							returnRefKind: delegateInvokeMethod.ReturnsByRef
								? RefKind.Ref
								: delegateInvokeMethod.ReturnsByRefReadonly
									? RefKind.RefReadOnly
									: RefKind.None,
							parameterTypes: [.. delegateInvokeMethod.Parameters.Select(static param => param.Type)],
							parameterRefKinds: [.. delegateInvokeMethod.Parameters.Select(static param => param.RefKind)],
							callingConvention: unmanagedFunctionPointerAttributeCallConv switch
							{
								CallingConvention.Cdecl => SignatureCallingConvention.CDecl,
								CallingConvention.StdCall => SignatureCallingConvention.StdCall,
								CallingConvention.ThisCall => SignatureCallingConvention.ThisCall,
								CallingConvention.FastCall => SignatureCallingConvention.FastCall,
								_ => SignatureCallingConvention.Unmanaged // null (not given) or CallingConvention.Winapi (platform default) are both treated as default unmanaged
							}
						);
					}
				}
				catch (Exception e)
				{
					// This could happen, for example, when given `CallConv` types to specify the calling convention, those are invalid or not supported (we won't validate this on our own).
					// To keep parity with the case where the calling convention is specified through the `UnmanagedFunctionPointerAttribute`,
					// we'll catch exception from that here as well (there could be more edge cases which make `CreateFunctionPointerTypeSymbol` throw, and this way we catch those in for cases).

					spc.ReportDiagnostic(Diagnostic.Create(
						mCouldNotCreateRawFunctionPointerTypeDescriptor,
						location: Location.None, // `Location.None` is good enough here, as we can't really pinpoint what exactly caused the exception (even in the case when it's because of invalid calling conventions)
						tuple.delegateType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
						e.GetType().Name,
						e.Message
					));

					// We continue on with other delegate types, although this is a hard fail.
					// We do that, so we can report potential diagnostics for other delegate types, and not just fail on the first one we encounter.

					return default; // Yields `null` for the `delegateType` field, which we will filter out later on
				}

				return (
					tuple.delegateType,
					typeParamConstraints,
					rawFunctionPointerType
				);
			})
			.Where(static tuple => tuple.delegateType is not null) // Filter out the delegate types that had conflicting calling conventions specified; `default((INamedTypeSymbol delegateType, ...))` still yields `null` for the `delegateType` field
			.ToImmutableArray(); // We need to materialize the result, because we will enumerate it multiple times

		if (delegateTypeGroups.IsDefaultOrEmpty)
		{
			return; // There's nothing for us to do here, so we can just return early
		}

		var builder = new StringBuilder();

		foreach (var characteristic in FunctionPointerCharacteristics.Enumerate(typeability: Typeability.Typed))
		{
			GeneratePerFunctionPointerType(
				spc,
				in characteristic,
				delegateTypeGroups,
				builder
			);
		}
	}
}
