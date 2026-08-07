using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SemPtr.SourceGeneration;
using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;

namespace SemPtr;

partial class FunctionPointerGenerator
{
	private static readonly SymbolDisplayFormat mInvokeReturnTypeFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
		parameterOptions: SymbolDisplayParameterOptions.IncludeType,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
	);

	private static readonly SymbolDisplayFormat mInvokeParameterTypeFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
		parameterOptions: SymbolDisplayParameterOptions.IncludeType,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
	);

	private static readonly SymbolDisplayFormat mRawFunctionPointerFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
		parameterOptions: SymbolDisplayParameterOptions.IncludeModifiers | SymbolDisplayParameterOptions.IncludeType,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
	);

	private static bool TryGeneratePerInvokeMethodName(
		SourceProductionContext spc,
		Compilation compilation,
		string invokeMethodName,
		ImmutableArray<(ImmutableHashSet<INamedTypeSymbol>? callConvs, ImmutableArray<Location> attributeLocations)> callConvsGroups,
		IMethodSymbol delegateInvokeMethod,
		CallingConvention? unmanagedFunctionPointerAttributeCallingConvention,
		in FunctionPointerCharacteristics characteristics,
		string functionPointerTypeNameCRef,
		StringBuilder builder,
		[NotNullWhen(true)] out IFunctionPointerTypeSymbol? rawFunctionPointerType
	)
	{
		ImmutableHashSet<INamedTypeSymbol>? specifiedCallConvs;

		switch (callConvsGroups)
		{
			case [] or [(null, _)]:
				specifiedCallConvs = null;
				break;

			// The following three cases work, because `callConvsGroups`is actually grouped by the `callConvs` value.
			// So if we have specified call conventions in one place and all other places don't specify any call conventions (`callConvs` is null),
			// then our overriding behavior is to take the specified call conventions over others.

			case [({ } callConvs, _)]:
				specifiedCallConvs = callConvs;
				break;

			case [({ } callConvs, _), (null, _)]:
				specifiedCallConvs = callConvs;
				break;

			case [(null, _), ({ } callConvs, _)]:
				specifiedCallConvs = callConvs;
				break;

			default:
				// This means there are more than one specified call conventions for the same invoke method name.
				// This is an error.

				// TODO: report diagnostic for all the conflicting `callConvs` values in `callConvsGroups`

				rawFunctionPointerType = null;
				return false;
		}
		
		rawFunctionPointerType = specifiedCallConvs is not null
			? compilation.CreateFunctionPointerTypeSymbol(
				returnType: delegateInvokeMethod.ReturnsVoid
					? compilation.GetSpecialType(SpecialType.System_Void)
					: delegateInvokeMethod.ReturnType,
				returnRefKind: delegateInvokeMethod.ReturnsByRef
					? RefKind.Ref
					: delegateInvokeMethod.ReturnsByRefReadonly
						? RefKind.RefReadOnly
						: RefKind.None,
				parameterTypes: [..delegateInvokeMethod.Parameters.Select(static param => param.Type)],
				parameterRefKinds: [..delegateInvokeMethod.Parameters.Select(static param => param.RefKind)],
				callingConvention: SignatureCallingConvention.Unmanaged, // Has to be unmanaged in this case (because we specify calling conventions through a CallConv* types array).
				callingConventionTypes: [..specifiedCallConvs]
			)
			: compilation.CreateFunctionPointerTypeSymbol(
				returnType: delegateInvokeMethod.ReturnsVoid
					? compilation.GetSpecialType(SpecialType.System_Void)
					: delegateInvokeMethod.ReturnType,
				returnRefKind: delegateInvokeMethod.ReturnsByRef
					? RefKind.Ref
					: delegateInvokeMethod.ReturnsByRefReadonly
						? RefKind.RefReadOnly
						: RefKind.None,
				parameterTypes: [..delegateInvokeMethod.Parameters.Select(static param => param.Type)],
				parameterRefKinds: [..delegateInvokeMethod.Parameters.Select(static param => param.RefKind)],
				callingConvention: unmanagedFunctionPointerAttributeCallingConvention switch
				{
					CallingConvention.Cdecl => SignatureCallingConvention.CDecl,
					CallingConvention.StdCall => SignatureCallingConvention.StdCall,
					CallingConvention.ThisCall => SignatureCallingConvention.ThisCall,
					CallingConvention.FastCall => SignatureCallingConvention.FastCall,
					_ => SignatureCallingConvention.Unmanaged // null (not given) or CallingConvention.Winapi (platform default) are both treated as default unmanaged
				}
			);

		builder.Append($$"""

					/// <summary>
					/// Invokes the function pointer{{delegateInvokeMethod.Parameters.Length switch { > 0 => " with the specified arguments", _ => string.Empty }}}.
					/// </summary>
			""");

		for (var i = 0; i < delegateInvokeMethod.Parameters.Length; i++)
		{
			var param = delegateInvokeMethod.Parameters[i];

			builder.Append($$"""

						/// <param name="{{param.Name}}">The {{i switch
				{
					0 => "first",
					1 => "second",
					2 => "third",
					3 => "fourth",
					4 => "fifth",
					5 => "sixth",
					6 => "seventh",
					7 => "eighth",
					8 => "ninth",
					9 => "tenth",
					10 => "eleventh",
					11 => "twelfth",
					12 => "thirteenth",
					_ => $"{i + 1}{(i % 100) switch
					{
						>= 10 and <= 12 => "th",
						_ => (i % 10) switch
						{
							0 => "st",
							1 => "nd",
							2 => "rd",
							_ => "th"
						}
					}}"
				}}} argument.</param>
				""");
		}

		if (!delegateInvokeMethod.ReturnsVoid)
		{
			builder.Append($$"""

						/// <returns>The return value of the invocation of the function pointer.</returns>
				""");
		}

		builder.Append($$"""

					/// <remarks>
					/// <para>
					/// This method invokes the target function the function pointer is pointing to, using the specified calling convention, if any.
					/// </para>
					/// <para>
					/// Note that for performance reasons, this method does not validate whether the function pointer is non-<c><see langword="null"/></c> or is pointing to a valid executable code before invoking it.
					/// Invoking a function pointer that is <c><see langword="null"/></c> or is not pointing to a valid executable code will result in undefined behavior, which may include a crashes or worse behavior.
					/// You should always ensure that the function pointer is non-<c><see langword="null"/></c> before invoking it, e.g., by checking the <c><see cref="{{functionPointerTypeNameCRef}}.HasTargetFunction"/></c> property.
					/// </para>
					/// </remarks>
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public unsafe {{(delegateInvokeMethod.ReturnsByRef
				? "ref "
				: delegateInvokeMethod.ReturnsByRefReadonly
					? "ref readonly "
					: string.Empty
			)}}{{(delegateInvokeMethod.ReturnsVoid
				? "void"
				: delegateInvokeMethod.ReturnType.ToDisplayString(mInvokeReturnTypeFormat)
			)}} {{invokeMethodName}}({{string.Join(", ", delegateInvokeMethod.Parameters.Select(static param => $"{(param.IsParams
				? "params "
				: string.Empty
			)}{param.ScopedKind switch
			{
				ScopedKind.ScopedRef or ScopedKind.ScopedValue => "scoped ",
				_ => string.Empty
			}}{param.RefKind switch
			{
				RefKind.Ref => "ref ",
				RefKind.Out => "out ",
				RefKind.In => "in ",
				RefKind.RefReadOnlyParameter => "ref readonly ",
				_ => string.Empty
			}}{param.Type.ToDisplayString(mInvokeParameterTypeFormat)} {param.Name}{(param.HasExplicitDefaultValue
				? $" = {SymbolDisplay.FormatPrimitive(param.ExplicitDefaultValue, quoteStrings: true, useHexadecimalNumbers: false)}"
				: string.Empty
			)}"))}})
					{
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						static void* getRaw<TPointer>(TPointer {{Config.GenerationExtensionArgumentName}})
							where TPointer : unmanaged, global::{{Config.PointerNamespaceName}}.{{Config.PointerInterfaceTypeName}}{{characteristics.Persistency switch { not Persistency.Persistent => ", allows ref struct", _ => string.Empty }}}
							=> {{Config.GenerationExtensionArgumentName}}.{{Config.PointerInterfaceTypeRawPointerPropertyName}};

						{{(delegateInvokeMethod.ReturnsVoid
				? string.Empty
				: $"return {(delegateInvokeMethod.ReturnsByRef | delegateInvokeMethod.ReturnsByRefReadonly
					? "ref "
					: string.Empty
				)}"
			)}}unchecked((({{rawFunctionPointerType.ToDisplayString(mRawFunctionPointerFormat)}})getRaw({{Config.GenerationExtensionArgumentName}})))({{string.Join(", ", delegateInvokeMethod.Parameters.Select(static param => $"{param.RefKind switch
			{
				RefKind.Ref => "ref ",
				RefKind.Out => "out ",
				RefKind.In or RefKind.RefReadOnlyParameter => "in ",
				_ => string.Empty
			}}{param.Name}"))}});
					}

			""");

		return true;
	}
}
