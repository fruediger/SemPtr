using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerEquatability(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		// A brief explanation about the equality overloads:
		// All pointer types can be implicitly converted to 'NullablePointerReadOnly', 'NullablePointerUninitialized', or both (or they are already one of those types).
		// Therefore, we can overload the 'Equals' methods/equality operators to accept those types (on the right-hand side),
		// and this way will allow for equality comparisons between any pointer types.
		// In the case that a type can be implicitly converted to either 'NullablePointerReadOnly' or 'NullablePointerUninitialized',
		// we'll use 'OverloadResolutionPriority' to prioritize the same type overload, then the 'NullablePointerReadOnly' overload, then the 'NullablePointerUninitialized' overload.

		// With the recent addition of function pointers, we also need to add overloads for 'NullableFunctionPointer' and only for that type, as all function pointer types can be implicitly converted to 'NullableFunctionPointer'.

		// Since pointer types recently became convertible to and from raw pointers, we also need to add equality overloads for raw pointers, to enable, for example, `null`-comparisons for non-nullable pointers.
		// Otherwise, the existing equality overloads would force an implicit conversion from the raw pointer and could throw an `ArgumentNullException` if the given raw pointer happens to be `null`.
		// However, we explicitly want to enable users to compare any pointers against the `null` literal without any issues.
		// To gain parity across all pointer types, we'll also add those raw pointer equality overloads for nullable pointer types as well.
		// For that to work, the raw pointer equality overloads must have the highest overload resolution priority and the semantic pointer to raw pointer conversion must be `explicit` (which it is, see `SourceGenerator.GeneratePointerConversions.cs`).

		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var typeNameCRef = characteristics.Typeability switch { Typeability.Typed => $"{characteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => typeName };
		var characteristicsIsNotNpro = characteristics is not { Nullability: Nullability.Nullable, Persistency: Persistency.Transient, Sequencability: Sequencability.Object, Accessibility: Accessibility.ReadOnly, Typeability: Typeability.Untyped }; // NullablePointerReadOnly
		var characteristicsIsNotNpu = characteristics is not { Nullability: Nullability.Nullable, Persistency: Persistency.Transient, Sequencability: Sequencability.Object, Accessibility: Accessibility.Uninitialized, Typeability: Typeability.Untyped }; // NullablePointerUninitialized
		var nproTypeName = characteristicsIsNotNpro ? new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.ReadOnly, Typeability.Untyped).ToTypeName() : "";
		var npuTypeName = characteristicsIsNotNpu ? new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.Uninitialized, Typeability.Untyped).ToTypeName() : "";
		var nfpTypeName = new FunctionPointerCharacteristics(Nullability.Nullable, Persistency.Transient, Typeability.Untyped).ToTypeName();

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};
			
			partial struct {{typeName}} :
				global::System.IEquatable<{{typeName}}>
			""");

		if (characteristicsIsNotNpro)
		{
			builder.Append($$"""
				,
					global::System.IEquatable<{{nproTypeName}}>
				""");
		}

		if (characteristicsIsNotNpu)
		{
			builder.Append($$"""
				,
					global::System.IEquatable<{{npuTypeName}}>
				""");
		}

		builder.Append($$"""
			,
				global::System.IEquatable<{{nfpTypeName}}>
			""");

		if (characteristics.Persistency is Persistency.Persistent)
		{
			// Sadly, we can't add 'IEqualityOperators' to non-persistent pointers, as they are declared as 'ref struct', and 'IEqualityOperators' does not have 'allows ref struct' on any of its type parameter contraints.
			// However, we'll add the equality operators anyway, it's just that non-persistent pointers won't be able to implement the interfaces.
			builder.Append($$"""
				,
					global::System.Numerics.IEqualityOperators<{{typeName}}, {{typeName}}, bool>
				""");

			// At this point, we'd like to add 'IEqualityOperators' interface implementations for 'NullablePointerReadOnly' and 'NullablePointerUninitialized' parameters on the right-hand side as well,
			// but because 'NullablePointerReadOnly' and 'NullablePointerUninitialized' are not persistent, and therefore are 'ref struct's, we can't do that.
			// However, as already stated, we'll add the corresponding equality operators anyway.
		}

		builder.Append($$"""

			{
			""");

		if (characteristics.Persistency is not Persistency.Persistent)
		{
			var typeNameWithoutTypeParameter = characteristics.ToTypeNameWithoutTypeParameter();

			builder.Append($$"""

					/// <summary>Not supported. Do not call this method, use the <see cref="Equals({{typeNameWithoutTypeParameter}}{{characteristics switch { { Typeability: Typeability.Typed } => $"{{{Config.GenerationTypeParameterName}}}", _ => string.Empty }}})" /> method instead.</summary>
					/// <exception cref="global::System.NotSupportedException">Always</exception>
					[global::System.Obsolete($"Not supported. Do not call this method, use the {nameof(Equals)}({nameof({{typeNameWithoutTypeParameter}}{{characteristics switch { { Typeability: Typeability.Typed } => "<>", _ => string.Empty }}})}) method instead.")]
					[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
				#pragma warning disable CS0809 // That's just how it is for ref structs
					public readonly override bool Equals([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj) => throw new global::System.NotSupportedException();
				#pragma warning restore CS0809

				""");
		}
		else
		{
			builder.Append($$"""

					/// <inheritdoc/>
					public readonly override bool Equals([global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj)
					{
						unsafe
						{
							return obj switch
							{
								{{typeName}} other => Equals(other),
								{{Config.PointerInterfaceTypeName}} { {{Config.PointerInterfaceTypeRawPointerPropertyName}}: var otherRaw } => {{Config.GenerationRawPointerFieldName}} == otherRaw,
								_ => false
							};
						}
					}

				""");
		}

		var (sameTypeOverloadPriority, rawPointerOverloadPriority) = (characteristicsIsNotNpu, characteristicsIsNotNpro) switch
		{
			(true, true) => ("4", "5"),
			(false, true) or (true, false) => ("3", "4"),
			_ => ("2", "3")
		};

		builder.Append($$"""
				
				/// <inheritdoc/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{sameTypeOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly bool Equals({{typeName}} other) 
				{
					unsafe
					{
						return {{Config.GenerationRawPointerFieldName}} == other.{{Config.GenerationRawPointerFieldName}};
					}
				}

			""");

		if (characteristicsIsNotNpro)
		{
			builder.Append($$"""

					/// <inheritdoc/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{(characteristicsIsNotNpu ? "3" : "2")}})]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public readonly bool Equals({{nproTypeName}} other) 
					{
						unsafe
						{
							return {{Config.GenerationRawPointerFieldName}} == other.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

				""");
		}

		if (characteristicsIsNotNpu)
		{
			builder.Append($$"""

					/// <inheritdoc/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public readonly bool Equals({{npuTypeName}} other) 
					{
						unsafe
						{
							return {{Config.GenerationRawPointerFieldName}} == other.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

				""");
		}

		// There's just a single raw pointer overload that accepts a `void*` argument, for the same reason as the `Equals(NullablePointerReadOnly)` and `Equals(NullablePointerUninitialized)` overloads,
		// every raw pointer type in C# can be implicitly converted to a `void*` and we don't want to restrict the kinds of raw pointer types the semantic pointer type can be compared against.

		builder.Append($$"""

				/// <inheritdoc/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly bool Equals({{nfpTypeName}} other) 
				{
					unsafe
					{
						return {{Config.GenerationRawPointerFieldName}} == other.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}
			
				/// <summary>
				/// Indicates whether the current pointer is equal to the specified raw pointer.
				/// </summary>
				/// <param name="raw">The raw pointer to compare with the current pointer.</param>
				/// <returns><c><see langword="true"/></c>, if the current pointer is equal to the specified raw pointer; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{rawPointerOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe readonly bool Equals(void* raw) => {{Config.GenerationRawPointerFieldName}} == raw;
				
				/// <inheritdoc/>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly override int GetHashCode()
				{
					unsafe
					{
						return unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}}).GetHashCode();
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Equality(TSelf,TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{sameTypeOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator ==({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{sameTypeOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator !=({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} != right.{{Config.GenerationRawPointerFieldName}};
					}
				}

			""");

		if (characteristicsIsNotNpro)
		{
			builder.Append($$"""

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Equality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{(characteristicsIsNotNpu ? "3" : "2")}})]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator ==({{typeName}} left, {{nproTypeName}} right)
					{
						unsafe
						{
							return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{(characteristicsIsNotNpu ? "3" : "2")}})]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator !=({{typeName}} left, {{nproTypeName}} right)
					{
						unsafe
						{
							return left.{{Config.GenerationRawPointerFieldName}} != right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

				""");
		}

		if (characteristicsIsNotNpu)
		{
			builder.Append($$"""

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Equality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator ==({{typeName}} left, {{npuTypeName}} right)
					{
						unsafe
						{
							return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator !=({{typeName}} left, {{npuTypeName}} right)
					{
						unsafe
						{
							return left.{{Config.GenerationRawPointerFieldName}} != right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

				""");
		}

		builder.Append($$"""

				/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Equality(TSelf,TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator ==({{typeName}} left, {{nfpTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator !=({{typeName}} left, {{nfpTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} != right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <summary>
				/// Compares a {{typeNameCRef}} with a raw pointer to determine equality.
				/// </summary>
				/// <param name="left">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <param name="right">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <returns><c><see langword="true"/></c>, if the {{typeNameCRef}} is equal to the raw pointer; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{rawPointerOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator ==({{typeName}} left, void* right) => left.{{Config.GenerationRawPointerFieldName}} == right;

				/// <summary>
				/// Compares a {{typeNameCRef}} with a raw pointer to determine inequality.
				/// </summary>
				/// <param name="left">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <param name="right">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <returns><c><see langword="true"/></c>, if the {{typeNameCRef}} is not equal to the raw pointer; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{rawPointerOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator !=({{typeName}} left, void* right) => left.{{Config.GenerationRawPointerFieldName}} != right;

				/// <summary>
				/// Compares a raw pointer with a {{typeNameCRef}} to determine equality.
				/// </summary>
				/// <param name="left">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <param name="right">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <returns><c><see langword="true"/></c>, if the raw pointer is equal to the {{typeNameCRef}}; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{rawPointerOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator ==(void* left, {{typeName}} right) => left == right.{{Config.GenerationRawPointerFieldName}};

				/// <summary>
				/// Compares a raw pointer with a {{typeNameCRef}} to determine inequality.
				/// </summary>
				/// <param name="left">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <param name="right">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <returns><c><see langword="true"/></c>, if the raw pointer is not equal to the {{typeNameCRef}}; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{rawPointerOverloadPriority}})]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator !=(void* left, {{typeName}} right) => left != right.{{Config.GenerationRawPointerFieldName}};
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Equatability.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
