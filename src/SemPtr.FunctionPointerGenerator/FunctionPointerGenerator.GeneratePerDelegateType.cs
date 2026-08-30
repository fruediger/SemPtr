using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SemPtr.SourceGeneration;
using System.Collections.Immutable;
using System.Linq;
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

	private static readonly SymbolDisplayFormat mRawFunctionPointerFormat = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
		parameterOptions: SymbolDisplayParameterOptions.IncludeModifiers | SymbolDisplayParameterOptions.IncludeType,
		kindOptions: SymbolDisplayKindOptions.None,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
	);

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

	private static void GeneratePerDelegateType(
		SourceProductionContext spc,
		in FunctionPointerCharacteristics characteristics,
		INamedTypeSymbol delegateType,
		ImmutableArray<string> typeParamConstraints,
		IFunctionPointerTypeSymbol rawFunctionPointerType,
		StringBuilder builder
	)
	{
		var extensionFunctionPointerTypeName = $"global::{Config.PointerNamespaceName}.{characteristics.ToTypeName(typeParameterName: delegateType.ToDisplayString(mDelegateTypeAsTypeArgumentFormat))}";
		var functionPointerTypeNameCRef = $"global::{Config.PointerNamespaceName}.{characteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationDelegateTypeParameterName}}}";
		var rawFunctionPointerTypeName = rawFunctionPointerType.ToDisplayString(mRawFunctionPointerFormat);
		var delegateInvokeMethod = delegateType.DelegateInvokeMethod!; // We know this is not null because we already verified that earlier

		// Generate "instance" extension members that need a receiver instance

		builder.Append($$"""

				extension{{(delegateType.TypeParameters.Length is > 0
				? $"<{string.Join(", ", delegateType.TypeParameters.Select(static arg => arg.Name))}>"
				: string.Empty
			)}}({{extensionFunctionPointerTypeName}} {{Config.GenerationExtensionArgumentName}})
			""");

		foreach (var constraint in typeParamConstraints)
		{
			builder.Append($$"""
					
						{{constraint}}
				""");
		}

		// Generate the `Raw` extension property

		builder.Append($$"""

				{
					/// <summary>
					/// Gets a raw pointer pointing to the same target function as the current pointer.
					/// </summary>
					/// <value>
					/// A raw pointer pointing to the same target function as the current pointer.
					/// </value>
					[global::System.CodeDom.Compiler.GeneratedCode("{{mTool.Name}}", "{{mTool.Version}}")]
					public unsafe {{rawFunctionPointerTypeName}} {{Config.PointerInterfaceTypeRawPointerPropertyName}}
					{
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						get
						{
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							static void* getRaw<TPointer>(TPointer {{Config.GenerationExtensionArgumentName}})
								where TPointer : unmanaged, global::{{Config.PointerNamespaceName}}.{{Config.PointerInterfaceTypeName}}{{characteristics.Persistency switch { not Persistency.Persistent => ", allows ref struct", _ => string.Empty }}}
								=> {{Config.GenerationExtensionArgumentName}}.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
			
							return unchecked(({{rawFunctionPointerTypeName}})getRaw({{Config.GenerationExtensionArgumentName}}));
						}
					}

			""");

		if (characteristics.Nullability is Nullability.NonNull)
		{
			// If the function pointer type is non-null, generate the `Invoke` extension method

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
					"""); // I'll admit that I'm not proficient enough in English to know whether this is good enough or not...
			}

			if (!delegateInvokeMethod.ReturnsVoid)
			{
				builder.Append($$"""

							/// <returns>The return value of the invocation of the function pointer.</returns>
					""");
			}

			// Instead of checking whether the signature of the `Invoke` method actually needs to be declared `unsafe` or if we get away with an `unsafe` block inside the method body,
			// we just declare the method `unsafe` regardless of whether if it actually needs to be or not.
			// At the moment that's just for simplicity's sake (and convenience, as I don't want to write branching code here again),
			// but in the future, once the new `unsafe` propagation feature drops (expected in C# 15), this has actually additional meaning,
			// as it will propagate the need for an `unsafe` context to the caller of the `Invoke` method, which I think is a good thing, since it calls into unmanaged code.

			builder.Append($$"""

						/// <remarks>
						/// <para>
						/// This method invokes the target function the function pointer is pointing to, using the specified calling convention, if any.
						/// </para>
						/// <para>
						/// Note that for performance reasons, this method does not validate whether the function pointer is non-<c><see langword="null"/></c> or is pointing to a valid executable code before invoking it.
						/// Invoking a function pointer that is <c><see langword="null"/></c> or is not pointing to a valid executable code will result in undefined behavior, which may include crashes or worse behavior.
						/// You should always ensure that the function pointer is non-<c><see langword="null"/></c> before invoking it, e.g., by checking the <c><see cref="{{functionPointerTypeNameCRef}}.HasTargetFunction"/></c> property.
						/// </para>
						/// </remarks>
						[global::System.CodeDom.Compiler.GeneratedCode("{{mTool.Name}}", "{{mTool.Version}}")]
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public unsafe {{(delegateInvokeMethod.ReturnsByRef
					? "ref "
					: delegateInvokeMethod.ReturnsByRefReadonly
						? "ref readonly "
						: string.Empty
				)}}{{(delegateInvokeMethod.ReturnsVoid
					? "void"
					: delegateInvokeMethod.ReturnType.ToDisplayString(mInvokeReturnTypeFormat)
				)}} {{Config.GenerationInvokeMethodName}}({{string.Join(", ", delegateInvokeMethod.Parameters.Select(static param => $"{(param.IsParams
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
					: $"return {(delegateInvokeMethod.ReturnsByRef || delegateInvokeMethod.ReturnsByRefReadonly
						? "ref "
						: string.Empty
					)}"
				)}}unchecked((({{rawFunctionPointerTypeName}})getRaw({{Config.GenerationExtensionArgumentName}})))({{string.Join(", ", delegateInvokeMethod.Parameters.Select(static param => $"{param.RefKind switch
				{
					RefKind.Ref => "ref ",
					RefKind.Out => "out ",
					RefKind.In or RefKind.RefReadOnlyParameter => "in ",
					_ => string.Empty
				}}{param.Name}"))}});
						}

				""");
		}

		// Generate `static` extension members on the receiver type

		builder.Append($$"""
				}

				extension{{(delegateType.TypeParameters.Length is > 0
					? $"<{string.Join(", ", delegateType.TypeParameters.Select(static arg => arg.Name))}>"
					: string.Empty
				)}}({{extensionFunctionPointerTypeName}})
			""");

		foreach (var constraint in typeParamConstraints)
		{
			builder.Append($$"""
					
						{{constraint}}
				""");
		}

		builder.Append("""
				
				{
			""");

		// Generate the `FromRaw` extension method

		if (characteristics.Nullability is not Nullability.Nullable)
		{
			builder.Append($$"""

						/// <summary>
						/// Creates a <see cref="{{functionPointerTypeNameCRef}}"/> from a <paramref name="raw"/> pointer.
						/// </summary>
						/// <param name="raw">The raw pointer specifying the target function that the resulting <see cref="{{functionPointerTypeNameCRef}}"/> will point to.</param>
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
			builder.Append($$"""

						/// <summary>
						/// Creates a <see cref="{{functionPointerTypeNameCRef}}"/> from a <paramref name="raw"/> pointer.
						/// </summary>
						/// <param name="raw">The raw pointer specifying the target function that the resulting <see cref="{{functionPointerTypeNameCRef}}"/> will point to.</param>
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

		builder.Append($$"""
			
					[global::System.CodeDom.Compiler.GeneratedCode("{{mTool.Name}}", "{{mTool.Version}}")]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public unsafe static {{extensionFunctionPointerTypeName}} {{Config.PointerInterfaceTypeFromRawMethodName}}({{rawFunctionPointerTypeName}} raw)
					{
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						static TPointer fromRaw<TPointer>(void* raw)
							where TPointer : unmanaged, global::{{Config.PointerNamespaceName}}.{{Config.PointerInterfaceTypeName}}<TPointer>{{characteristics.Persistency switch { not Persistency.Persistent => ", allows ref struct", _ => string.Empty }}}
							=> TPointer.{{Config.PointerInterfaceTypeFromRawMethodName}}(raw);
				
						return fromRaw<{{extensionFunctionPointerTypeName}}>(unchecked((void*)raw));
					}
				}

			""");
	}
}
