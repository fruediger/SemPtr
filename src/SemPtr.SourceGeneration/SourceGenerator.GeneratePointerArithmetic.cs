using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerArithmetic(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		if (characteristics is not { Sequencability: Sequencability.Sequence, Typeability: Typeability.Typed })
		{
			return;
		}

		// Arithmetics to implement (VAL[op] / LHS [op] RHS -> Result (OverloadResolutionPriority), [via interface?]):
		// sequence pointer type++ (0, i.e. default), IIncrementOperators
		// sequence pointer type-- (0, i.e. default), IDecrementOperators
		// sequence pointer type + nint -> sequence pointer type (1), IAdditionOperators
		// sequence pointer type + nuint -> sequence pointer type (0, i.e. default), IAdditionOperators
		// nint + sequence pointer type -> sequence pointer type (1), no interface
		// nuint + sequence pointer type -> sequence pointer type (0, i.e. default), no interface
		// sequence pointer type - nint -> sequence pointer type (1), ISubtractionOperators
		// sequence pointer type - nuint -> sequence pointer type (0, i.e. default), ISubtractionOperators
		// (integral numeric value - sequence pointer type is omitted, as even C# does not permit this)
		// sequence pointer type - same sequence pointer type -> nint (5), ISubtractionOperators
		// sequence pointer type - NullablePointerReadOnly<T> -> nint (4), no interface
		// sequence pointer type - NullablePointerUninitialized<T> -> nint (3), no interface
		// NullablePointerReadOnly<T> - sequence pointer type -> nint (2), no interface
		// NullablePointerUninitialized<T> - sequence pointer type -> nint (1), no interface
		//
		// Note that the overload resolution priority of the pointer - pointer operators follows the same reasoning as the operator overloads for comparison.
		// See the comments in SourceGenerator.GeneratePointerComparability for more information.

		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var nproTypeName = new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.ReadOnly, Typeability.Typed).ToTypeName();
		var npuTypeName = new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.Uninitialized, Typeability.Typed).ToTypeName();

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			partial struct {{typeName}}
			""");

		if (characteristics.Persistency is Persistency.Persistent)
		{
			// None of these interfaces accept `ref struct` type arguments, so we only add them to the implemented interfaces list for persistent pointer types.
			// As always, the corresponding operator overloads will still be generated for all pointer types, regardless of persistency.

			builder.Append($$"""
				 :
					global::System.Numerics.IIncrementOperators<{{typeName}}>,
					global::System.Numerics.IDecrementOperators<{{typeName}}>,
					global::System.Numerics.IAdditionOperators<{{typeName}}, nint, {{typeName}}>,
					global::System.Numerics.IAdditionOperators<{{typeName}}, nuint, {{typeName}}>,
					global::System.Numerics.ISubtractionOperators<{{typeName}}, nint, {{typeName}}>,
					global::System.Numerics.ISubtractionOperators<{{typeName}}, nuint, {{typeName}}>,
					global::System.Numerics.ISubtractionOperators<{{typeName}}, {{typeName}}, nint>
				""");
		}

		builder.Append($$"""

			{
				/// <inheritdoc cref="global::System.Numerics.IIncrementOperators{TSelf}.op_Increment(TSelf)"/>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator ++({{typeName}} value)
				{
					unsafe
					{
						var raw = value.{{Config.GenerationRawPointerFieldName}};
						return new(++raw);
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IDecrementOperators{TSelf}.op_Decrement(TSelf)"/>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator --({{typeName}} value)
				{
					unsafe
					{
						var raw = value.{{Config.GenerationRawPointerFieldName}};
						return new(--raw);
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IAdditionOperators{TSelf, TOther, TResult}.op_Addition(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator +({{typeName}} left, nint right)
				{
					unsafe
					{
						return new(left.{{Config.GenerationRawPointerFieldName}} + right);
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IAdditionOperators{TSelf, TOther, TResult}.op_Addition(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator +({{typeName}} left, nuint right)
				{
					unsafe
					{
						return new(left.{{Config.GenerationRawPointerFieldName}} + right);
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IAdditionOperators{TSelf, TOther, TResult}.op_Addition(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator +(nint left, {{typeName}} right)
				{
					unsafe
					{
						return new(left + right.{{Config.GenerationRawPointerFieldName}});
					}
				}
			
				/// <inheritdoc cref="global::System.Numerics.IAdditionOperators{TSelf, TOther, TResult}.op_Addition(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator +(nuint left, {{typeName}} right)
				{
					unsafe
					{
						return new(left + right.{{Config.GenerationRawPointerFieldName}});
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator -({{typeName}} left, nint right)
				{
					unsafe
					{
						return new(left.{{Config.GenerationRawPointerFieldName}} - right);
					}
				}
			
				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static {{typeName}} operator -({{typeName}} left, nuint right)
				{
					unsafe
					{
						return new(left.{{Config.GenerationRawPointerFieldName}} - right);
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(5)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator -({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return unchecked((nint)(left.{{Config.GenerationRawPointerFieldName}} - right.{{Config.GenerationRawPointerFieldName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_CheckedSubtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(5)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator checked -({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return checked((nint)(left.{{Config.GenerationRawPointerFieldName}} - right.{{Config.GenerationRawPointerFieldName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(4)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator -({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return unchecked((nint)(left.{{Config.GenerationRawPointerFieldName}} - right.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_CheckedSubtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(4)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator checked -({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return checked((nint)(left.{{Config.GenerationRawPointerFieldName}} - right.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator -({{typeName}} left, {{npuTypeName}} right)
				{
					unsafe
					{
						return unchecked((nint)(left.{{Config.GenerationRawPointerFieldName}} - right.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_CheckedSubtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator checked -({{typeName}} left, {{npuTypeName}} right)
				{
					unsafe
					{
						return checked((nint)(left.{{Config.GenerationRawPointerFieldName}} - right.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator -({{nproTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return unchecked((nint)(left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} - right.{{Config.GenerationRawPointerFieldName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_CheckedSubtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator checked -({{nproTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return checked((nint)(left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} - right.{{Config.GenerationRawPointerFieldName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_Subtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator -({{npuTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return unchecked((nint)(left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} - right.{{Config.GenerationRawPointerFieldName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.ISubtractionOperators{TSelf, TOther, TResult}.op_CheckedSubtraction(TSelf, TOther)"/>
				/// <returns>The signed distance in number of elements of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/> between the the <paramref name="left"/> and <paramref name="right"/> pointers.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static nint operator checked -({{npuTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return checked((nint)(left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} - right.{{Config.GenerationRawPointerFieldName}}));
					}
				}
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Arithmetic.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
