using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerComparability(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		if (characteristics.Sequencability is not Sequencability.Sequence)
		{
			// We only allow sequence pointers to be compared.
			// To be more precise, comparing pointers requires one of them to be a sequence pointer, the other one can be a sequence pointer or an object pointer.
			return;
		}

		// See `GeneratePointerEquality` for the reasoning behind the implementation model and the use of `OverloadResolutionPriority`.
		// In extension to that, we also need to consider when the other pointer is the left-hand side argument and the sequence pointer is on the right-hand side.
		// For that we add the approppriate overloads (i.e., with the sequence pointer as the right-hand side argument)
		// and because we don't want to clash with the LHS overloads, we resolve this by using `OverloadResolutionPriority` again and fully prioritize the LHS overloads over the RHS overloads.
		//
		// The total order of overload resolution priority is as follows from highest priority to lowest priority (LHS <-> RHS (OverloadResolutionPriority)):
		// sequence pointer type <-> same sequence pointer type (5)
		// sequence pointer type <-> NullablePointerReadOnly (4)
		// sequence pointer type <-> NullablePointerUninitialized (3)
		// NullablePointerReadOnly <-> sequence pointer type (2)
		// NullablePointerUninitialized <-> sequence pointer type (1)
		//
		// Since at this point we're surely generating for a sequence pointer type that cannot be `NullablePointerReadOnly` or `NullablePointerUninitialized`,
		// we can safely emit the full set of overloads as described above.

		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var characteristicsPersistent = characteristics.Persistency is Persistency.Persistent;
		var nproTypeName = new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.ReadOnly, Typeability.Untyped).ToTypeName();
		var npuTypeName = new PointerCharacteristics(Nullability.Nullable, Persistency.Transient, Sequencability.Object, Accessibility.Uninitialized, Typeability.Untyped).ToTypeName();

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			partial struct {{typeName}} :
			""");

		if (characteristicsPersistent)
		{
			// Like the obsoleted `Equals(object)` implementation, we cannot implement `IComparable` for transient pointers,
			// because there's no (boxing) conversion between an `object` (living on the heap) and a `ref struct`, and rightfully so.
			// Unlike `Equals(object)` which we had to awkwardly "hide" for transient pointers, in this case we can simply not implement the `object` accepting overload.

			builder.Append("""

					global::System.IComparable,
				""");
		}

		builder.Append($$"""

				global::System.IComparable<{{typeName}}>,
				global::System.IComparable<{{nproTypeName}}>,
				global::System.IComparable<{{npuTypeName}}>
			""");

		if (characteristicsPersistent)
		{
			// Like `IEqualityOperators`, `IComparisonOperators` doesn't accept `ref structs` as generic type arguments, so we can only implement this interface for persistent pointers.
			// However, we'll still implement the comparison operators for transient pointers as well.

			builder.Append($$"""
					,
					global::System.Numerics.IComparisonOperators<{{typeName}}, {{typeName}}, bool>
				""");
		}

		builder.Append($$"""

			{
			""");

		if (characteristicsPersistent)
		{
			builder.Append($$"""

					/// <inheritdoc/>
					/// <exception cref="global::System.ArgumentException"><paramref name="obj"/> is not a pointer type</exception>
					public readonly int CompareTo(object? obj)
					{
						unsafe
						{
							[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
							static int failObjIsNotPointer() => throw new global::System.ArgumentException($"{nameof(obj)} must be a pointer type", nameof(obj));

							return obj switch
							{
								null => 1,
								{{typeName}} other => CompareTo(other),
								{{Config.PointerInterfaceTypeName}} { {{Config.PointerInterfaceTypeRawPointerPropertyName}}: var otherRaw } => unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}}).CompareTo(unchecked((global::System.IntPtr)otherRaw)),
								_ => failObjIsNotPointer()
							};
						}
					}

				""");
		}

		builder.Append($$"""

				/// <inheritdoc/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly int CompareTo({{typeName}} other)
				{
					unsafe
					{
						return unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}}).CompareTo(unchecked((global::System.IntPtr)other.{{Config.GenerationRawPointerFieldName}}));
					}
				}

				/// <inheritdoc/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly int CompareTo({{nproTypeName}} other)
				{
					unsafe
					{
						return unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}}).CompareTo(unchecked((global::System.IntPtr)other.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
					}
				}

				/// <inheritdoc/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly int CompareTo({{npuTypeName}} other)
				{
					unsafe
					{
						return unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}}).CompareTo(unchecked((global::System.IntPtr)other.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(5)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} < right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(5)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} > right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(5)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <=({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} <= right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(5)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >=({{typeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} >= right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(4)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} < right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(4)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} > right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(4)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <=({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} <= right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(4)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >=({{typeName}} left, {{nproTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} >= right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <({{typeName}} left, {{npuTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} < right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >({{typeName}} left, {{npuTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} > right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <=({{typeName}} left, {{npuTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} <= right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(3)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >=({{typeName}} left, {{npuTypeName}} right)
				{
					unsafe
					{
						return left.{{Config.GenerationRawPointerFieldName}} >= right.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <({{nproTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} < right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >({{nproTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} > right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <=({{nproTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} <= right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >=({{nproTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} >= right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <({{npuTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} < right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThan(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >({{npuTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} > right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_LessThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator <=({{npuTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} <= right.{{Config.GenerationRawPointerFieldName}};
					}
				}

				/// <inheritdoc cref="global::System.Numerics.IComparisonOperators{TSelf, TOther, TResult}.op_GreaterThanOrEqual(TSelf, TOther)"/>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator >=({{npuTypeName}} left, {{typeName}} right)
				{
					unsafe
					{
						return left.{{Config.PointerInterfaceTypeRawPointerPropertyName}} >= right.{{Config.GenerationRawPointerFieldName}};
					}
				}
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Comparability.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
