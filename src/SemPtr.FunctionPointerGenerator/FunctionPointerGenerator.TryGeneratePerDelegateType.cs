using Microsoft.CodeAnalysis;
using SemPtr.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;

namespace SemPtr;

partial class FunctionPointerGenerator
{
	private static readonly SymbolDisplayFormat mDelegateTypeAsTypeArgumentFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
	);

	private static readonly SymbolDisplayFormat mExtensionsContraintsFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
		delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.ExpandValueTuple
	);

	private static bool TryGeneratePerDelegateType(
		SourceProductionContext spc,
		Compilation compilation,
		INamedTypeSymbol delegateType,
		IEnumerable<(string invokeMethodName, IEnumerable<(ImmutableHashSet<INamedTypeSymbol>? callConvs, ImmutableArray<Location> attributeLocations)> callConvsGroups)> invokeMethodNameGroups,
		INamedTypeSymbol? unmanagedFunctionPointerAttributeType,
		StringBuilder primaryBuilder,
		StringBuilder secondaryBuilder
	)
	{
		if (delegateType is not { DelegateInvokeMethod: IMethodSymbol delegateInvokeMethod })
		{
			return false;
		}

		var delegateTypeTypeParameterConstraints = new List<string>();
		var extensionConstraintsPart = delegateType.ToDisplayParts(mExtensionsContraintsFormat);
		var lastIndex = -1;
		for (var index = 0; index < extensionConstraintsPart.Length; index++)
		{
			var part = extensionConstraintsPart[index];
			if (part.Kind is SymbolDisplayPartKind.Keyword && part.ToString() is "where")
			{
				if (lastIndex is > 0)
				{
					delegateTypeTypeParameterConstraints.Add(extensionConstraintsPart[lastIndex..index].ToDisplayString());
				}

				lastIndex = index;
			}
		}
		if (lastIndex is not -1)
		{
			delegateTypeTypeParameterConstraints.Add(extensionConstraintsPart[lastIndex..].ToDisplayString());
		}

		var extensionFunctionPointerTypeNames = FunctionPointerCharacteristics.Enumerate(typeability: Typeability.Typed)
			.Select(characteristics => $"global::{Config.PointerNamespaceName}.{characteristics.ToTypeName(typeParameterName: delegateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))}")
			.ToImmutableArray();

		var unmanagedFunctionPointerAttributeCallingConvention = unmanagedFunctionPointerAttributeType is not null
			? delegateType.GetAttributes()
				.Where(attr => attr.AttributeClass?.OriginalDefinition.Equals(unmanagedFunctionPointerAttributeType, SymbolEqualityComparer.Default) is true)
				.Select(attr => attr.ConstructorArguments switch
				{
					[{ Kind: TypedConstantKind.Enum, Value: int callingConvention }] => (CallingConvention)callingConvention,
					_ => default(CallingConvention?),
				})
				.FirstOrDefault()
			: default;

		// TODO: do we need to materialize this?
		var invokeMethodNameGroupsMaterialized = invokeMethodNameGroups
			.Select(invokeMethodNameGroup => (invokeMethodNameGroup.invokeMethodName, callConvsGroups: invokeMethodNameGroup.callConvsGroups.ToImmutableArray()))
			.ToImmutableArray();

		var result = false;
		var rawFunctionPointerTypes = new List<IFunctionPointerTypeSymbol>();

		foreach (var characteristics in FunctionPointerCharacteristics.Enumerate(typeability: Typeability.Typed))
		{
			var extensionFunctionPointerTypeName = $"global::{Config.PointerNamespaceName}.{characteristics.ToTypeName(typeParameterName: delegateType.ToDisplayString(mDelegateTypeAsTypeArgumentFormat))}";
			var functionPointerTypeNameCRef = $"global::{Config.PointerNamespaceName}.{characteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationDelegateTypeParameterName}}}";

			secondaryBuilder.Clear();
			rawFunctionPointerTypes.Clear();

			foreach (var (invokeMethodName, callConvsGroups) in invokeMethodNameGroupsMaterialized)
			{
				if (!TryGeneratePerInvokeMethodName(
					spc,
					compilation,
					invokeMethodName,
					callConvsGroups,
					delegateInvokeMethod,
					unmanagedFunctionPointerAttributeCallingConvention,
					in characteristics,
					functionPointerTypeNameCRef,
					secondaryBuilder,
					out var functionPointerType
				))
				{
					continue;
				}

				rawFunctionPointerTypes.Add(functionPointerType);
			}

			if (rawFunctionPointerTypes.Count is not > 0)
			{
				// This essentially means that all the invoke method failed to generate, so we can skip generating for this particular function pointer characteristics.
				continue;
			}

			primaryBuilder.Append($$"""

					extension{{(delegateType.TypeParameters.Length is > 0
					? $"<{string.Join(", ", delegateType.TypeParameters.Select(static arg => arg.Name))}>"
					: string.Empty
				)}}({{extensionFunctionPointerTypeName}} {{Config.GenerationExtensionArgumentName}})
				""");

			foreach (var constraint in delegateTypeTypeParameterConstraints)
			{
				primaryBuilder.Append($$"""
					
							{{constraint}}
					""");
			}

			primaryBuilder.Append("""

					{
				""");						

			primaryBuilder.Append(secondaryBuilder);

			var rawFunctionPointerTypeName = rawFunctionPointerTypes switch
			{
				[var rawFunctionPointerType] => rawFunctionPointerType.ToDisplayString(mRawFunctionPointerFormat),
				_ => null
			};

			// We generate the `Raw`/`FromRaw` members, that typed function pointers don't come equipped with because they depend on the actual delegate type.
			// If we have multiple raw function pointer signatures (mostly differing by calling convention), we can't generate members for all of them
			// (because in C# signatures can't differ by return type alone).
			// Instead we generate a single kind of `Raw`/`FromRaw` members, accepting a `void*` argument instead of a specific function pointer type.

			primaryBuilder.Append($$"""

						/// <summary>
						/// Gets a raw pointer pointing to the same target function as the current pointer.
						/// </summary>
						/// <value>
						/// A raw pointer pointing to the same target function as the current pointer.
						/// </value>
						public unsafe {{rawFunctionPointerTypeName ?? "void*"}} {{Config.PointerInterfaceTypeRawPointerPropertyName}}
						{
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							get
							{
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								static void* getRaw<TPointer>(TPointer {{Config.GenerationExtensionArgumentName}})
									where TPointer : unmanaged, global::{{Config.PointerNamespaceName}}.{{Config.PointerInterfaceTypeName}}{{characteristics.Persistency switch { not Persistency.Persistent => ", allows ref struct", _ => string.Empty }}}
									=> {{Config.GenerationExtensionArgumentName}}.{{Config.PointerInterfaceTypeRawPointerPropertyName}};

								return {{rawFunctionPointerTypeName switch { not null => $"unchecked(({rawFunctionPointerTypeName})", _=> string.Empty }}}getRaw({{Config.GenerationExtensionArgumentName}}){{rawFunctionPointerTypeName switch { not null => ")", _=> string.Empty }}};
							}
						}
					}

					extension{{(delegateType.TypeParameters.Length is > 0
					? $"<{string.Join(", ", delegateType.TypeParameters.Select(static arg => arg.Name))}>"
					: string.Empty
				)}}({{extensionFunctionPointerTypeName}})
				""");

			foreach (var constraint in delegateTypeTypeParameterConstraints)
			{
				primaryBuilder.Append($$"""
					
							{{constraint}}
					""");
			}

			primaryBuilder.Append("""
				
					{
				""");

			if (characteristics.Nullability is not Nullability.Nullable)
			{
				primaryBuilder.Append($$"""

							/// <summary>
							/// Creates a <see cref="{{functionPointerTypeNameCRef}}"/> from a <paramref name="raw"/> pointer.
							/// </summary>
							/// <param name="raw">The raw pointer specifying the target function that the resulting <see cref="{{functionPointerTypeNameCRef}}"/> will point to.</param>
							/// <param name="_"><em>Ignore.</em> This parameter is unused and exists only to differentiate between overload method signatures. You should not provide a value for this parameter.</param>
							/// <returns>A <see cref="{{functionPointerTypeNameCRef}}"/> that points to the same target function as the specified <paramref name="raw"/> pointer.</returns>
							/// <remarks>
							/// <para>
							/// The <paramref name="raw"/> pointer must not be <c><see langword="null"/></c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
							/// </para>
							/// <para>
							/// The resulting <see cref="{{functionPointerTypeNameCRef}}"/> will point to the same target function as <paramref name="raw"/>.
							/// </para>
							/// </remarks>
							/// <exception cref="global::System.ArgumentNullException"><paramref name="raw"/> is <c><see langword="null"/></c></exception>
					""");
			}
			else
			{
				primaryBuilder.Append($$"""

							/// <summary>
							/// Creates a <see cref="{{functionPointerTypeNameCRef}}"/> from a <paramref name="raw"/> pointer.
							/// </summary>
							/// <param name="raw">The raw pointer specifying the target function that the resulting <see cref="{{functionPointerTypeNameCRef}}"/> will point to.</param>
							/// <param name="_"><em>Ignore.</em> This parameter is unused and exists only to differentiate between overload method signatures. You should not provide a value for this parameter.</param>
							/// <returns>A <see cref="{{functionPointerTypeNameCRef}}"/> that points to the same target function as the specified <paramref name="raw"/> pointer.</returns>
							/// <remarks>
							/// <para>
							/// The <paramref name="raw"/> pointer may be <c><see langword="null"/></c>. If it is, the resulting <see cref="{{functionPointerTypeNameCRef}}"/> will represent a null pointer.
							/// </para>
							/// <para>
							/// The resulting <see cref="{{functionPointerTypeNameCRef}}"/> will point to the same target function as <paramref name="raw"/>.
							/// </para>
							/// </remarks>
					""");
			}

			primaryBuilder.Append($$"""

						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public unsafe static {{extensionFunctionPointerTypeName}} {{Config.PointerInterfaceTypeFromRawMethodName}}({{rawFunctionPointerTypeName ?? "void*"}} raw, global::{{Config.PointerNamespaceName}}.{{Config.PointerDispatchInterfaceTypeName}}<{{extensionFunctionPointerTypeName}}>? _ = default)
						{
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							static TPointer fromRaw<TPointer>(void* raw)
								where TPointer : unmanaged, global::{{Config.PointerNamespaceName}}.{{Config.PointerInterfaceTypeName}}<TPointer>{{characteristics.Persistency switch { not Persistency.Persistent => ", allows ref struct", _ => string.Empty }}}
								=> TPointer.{{Config.PointerInterfaceTypeFromRawMethodName}}(raw);
				
							return fromRaw<{{extensionFunctionPointerTypeName}}>({{rawFunctionPointerTypeName switch { not null => "unchecked((void*)", _ => string.Empty }}}raw{{rawFunctionPointerTypeName switch { not null => ")", _ => string.Empty }}});
						}
					}

				""");

			result = true;
		}

		return result;
	}
}
