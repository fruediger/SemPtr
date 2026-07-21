using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerEquatability(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		// A short explanation about the equality overloads:
		// All pointer types can be implicitly converted to 'NullablePointerReadOnly', 'NullablePointerUninitialized', or both (or they are already one of those types).
		// Therefore, we can overload the 'Equals' methods/equality operators to accept those types (on the right-hand side),
		// and this way will allow for equality comparisons between any pointer types.
		// In the case that a type can be implicitly converted to either 'NullablePointerReadOnly' or 'NullablePointerUninitialized',
		// we'll use 'OverloadResolutionPriority' to prioritize the same type overload, then the 'NullablePointerReadOnly' overload, then the 'NullablePointerUninitialized' overload.

		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var characteristicsIsNotNpro = characteristics is not { Nullability: Nullability.Nullable, Persistency: Persistency.Transient, Sequencability: Sequencability.Object, Accessibility: Accessibility.ReadOnly, Typeability: Typeability.Untyped }; // NullablePointerReadOnly
		var characteristicsIsNotNpu = characteristics is not { Nullability: Nullability.Nullable, Persistency: Persistency.Transient, Sequencability: Sequencability.Object, Accessibility: Accessibility.Uninitialized, Typeability: Typeability.Untyped }; // NullablePointerUninitialized
		var nproTypeName = characteristicsIsNotNpro ? new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.ReadOnly, Typeability.Untyped).ToTypeName() : "";
		var npuTypeName = characteristicsIsNotNpu ? new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.Uninitialized, Typeability.Untyped).ToTypeName() : "";

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

		var sameTypeOverloadPriority = (characteristicsIsNotNpu, characteristicsIsNotNpro) switch { (true, true) => "3", (false, true) or (true, false) => "2", _ => "1" };

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
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{(characteristicsIsNotNpu ? "2" : "1")}})]
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
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
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

		builder.Append($$"""
				
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
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{(characteristicsIsNotNpu ? "2" : "1")}})]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator ==({{typeName}} left, {{nproTypeName}} right)
					{
						unsafe
						{
							return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority({{(characteristicsIsNotNpu ? "2" : "1")}})]
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
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator ==({{typeName}} left, {{npuTypeName}} right)
					{
						unsafe
						{
							return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
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

		builder.Append("""
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Equatability.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
