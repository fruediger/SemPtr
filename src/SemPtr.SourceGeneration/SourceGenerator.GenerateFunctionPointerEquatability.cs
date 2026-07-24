using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GenerateFunctionPointerEquatability(IncrementalGeneratorPostInitializationContext pic, in FunctionPointerCharacteristics characteristics, StringBuilder builder)
	{
		// See the comment in GenerateFunctionPointerEquatability for a brief explanation on how equatability is implemented for pointers.

		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var characteristicsIsNotNfp = characteristics is not { Nullability: Nullability.Nullable, Persistency: Persistency.Transient, Typeability: Typeability.Untyped };
		var nfpTypeName = characteristicsIsNotNfp ? new FunctionPointerCharacteristics(Nullability.Nullable, Persistency.Transient, Typeability.Untyped).ToTypeName() : "";
		var nproTypeName = new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.ReadOnly, Typeability.Untyped).ToTypeName();
		var npuTypeName = new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.Uninitialized, Typeability.Untyped).ToTypeName();

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			partial struct {{typeName}} :
				global::System.IEquatable<{{typeName}}>
			""");

		if (characteristicsIsNotNfp)
		{
			builder.Append($$"""
				,
					global::System.IEquatable<{{nfpTypeName}}>
				""");
		}

		builder.Append($$"""
			,
				global::System.IEquatable<{{nproTypeName}}>,
				global::System.IEquatable<{{npuTypeName}}>
			""");

		if (characteristics.Persistency is Persistency.Persistent)
		{
			// See the comment in GeneratePointerEquatability for why we add IEqualityOperators to the implemented interfaces list only for persistent pointers.

			builder.Append($$"""
				,
					global::System.Numerics.IEqualityOperators<{{typeName}}, {{typeName}}, bool>
				""");
		}

		builder.Append($$"""

			{
			""");

		if (characteristics.Persistency is not Persistency.Persistent)
		{
			var typeNameWithoutTypeParameter = characteristics.ToTypeNameWithoutTypeParameter();

			builder.Append($$"""

					/// <summary>Not supported. Do not call this method, use the <see cref="Equals({{typeNameWithoutTypeParameter}}{{characteristics switch { { Typeability: Typeability.Typed } => $"{{{Config.GenerationDelegateTypeParameterName}}}", _ => string.Empty }}})" /> method instead.</summary>
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

		var sameTypeOverloadPriority = characteristicsIsNotNfp ? "4" : "3";

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

		if (characteristicsIsNotNfp)
		{
			builder.Append($$"""

					/// <inheritdoc/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public readonly bool Equals({{nfpTypeName}} other) 
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
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly bool Equals({{nproTypeName}} other) 
				{
					unsafe
					{
						return {{Config.GenerationRawPointerFieldName}} == other.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

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

		if (characteristicsIsNotNfp)
		{
			builder.Append($$"""

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Equality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator ==({{typeName}} left, {{nfpTypeName}} right)
					{
						unsafe
						{
							return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
						}
					}

					/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static bool operator !=({{typeName}} left, {{nfpTypeName}} right)
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
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator ==({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} == right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IEqualityOperators{TSelf,TOther,TOtherResult}.op_Inequality(TSelf,TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator !=({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} != right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

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
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Equatability.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
