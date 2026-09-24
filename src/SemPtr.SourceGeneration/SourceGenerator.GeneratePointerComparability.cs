using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SemPtr.Analyzers;
using System.Text;
using Accessibility = SemPtr.Analyzers.Accessibility;

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
		// sequence pointer type <-> raw pointer type (6)
		// raw pointer type <-> sequence pointer type (6)
		// sequence pointer type <-> same sequence pointer type (5)
		// sequence pointer type <-> NullablePointerReadOnly (4)
		// sequence pointer type <-> NullablePointerUninitialized (3)
		// NullablePointerReadOnly <-> sequence pointer type (2)
		// NullablePointerUninitialized <-> sequence pointer type (1)
		//
		// Since at this point we're surely generating for a sequence pointer type that cannot be `NullablePointerReadOnly` or `NullablePointerUninitialized`,
		// we can safely emit the full set of overloads as described above.
		//
		// The list shown above was recently updated to include overloads for raw pointer types which follow the same reasoning as the raw pointer equality overloads defined in `SourceGenerator.GeneratePointerEquatability.cs`.
		// See the comments there for a more in-depth explanation of what's going on here.
		// On that note, raw pointer comparison operator overloads and their vice-versa counterparts (LHS and RHS swapped) don't need to be in different overload resolution priorities,
		// because a core requirement of adding raw pointer conversions to semantic data pointer types was that semantic pointer types are always just going to be `explicit`ly convertible to raw pointers,
		// together with the fact that every semantic data pointer type defines those two comparison operator overload pairs (LHS and RHS swapped) for raw pointers,
		// there's no way for an ambiguity to arise, neither when comparing, for example, `<semantic pointer type> < null` or `null < <semantic pointer type>`, since both are distinguishably declared on their own,
		// nor when comparing, for example, `<semantic pointer type> < <other semantic pointer type>` or `<other semantic pointer type> < <semantic pointer type>`, since in that case the conversion operator path can't be taken.

		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var typeNameCRef = characteristics.Typeability switch { Typeability.Typed => $"{characteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => typeName };
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

		// Since we don't need to separate LHS and RHS comparison overloads into different overload resolution priorities (all `CompareTo` comparisons are technically LHS comparisons),
		// the overload resolution priorities used for `CompareTo` overloads only goes up to 4 instead of 6 as in the comparison operators.

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

				/// <summary>
				/// Compares the current pointer with another raw pointer and returns an integer that indicates whether the current pointer precedes, follows, or equals the raw pointer in the sort order.
				/// </summary>
				/// <param name="raw">The raw pointer to compare with the current pointer.</param>
				/// <returns>
				/// A value that indicates the relative order of the pointers being compared.
				/// The return value has these meanings:
				/// <list type="table">
				///		<listheader>
				///			<term>Value</term>
				/// 		<description>Meaning</description>
				///		</listheader>
				/// 	<item>
				///			<term>Less than zero</term>
				/// 		<description>The current pointer precedes <paramref name="raw"/> in the sort order.</description>
				///		</item>
				/// 	<item>
				///			<term>Zero</term>
				///			<description>The current pointer equals <paramref name="raw"/> in the sort order.</description>
				/// 	</item>
				/// 	<item>
				///			<term>Greater than zero</term>
				///			<description>The current pointer follows <paramref name="raw"/> in the sort order.</description>
				/// 	</item>
				/// </list>
				/// </returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(4)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe readonly int CompareTo(void* raw) => unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}}).CompareTo(unchecked((global::System.IntPtr)raw));

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

				/// <summary>
				/// Compares a {{typeNameCRef}} with a raw pointer to determine which is less.
				/// </summary>
				/// <param name="left">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <param name="right">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <returns><c><see langword="true"/></c>, if the {{typeNameCRef}} is less than the raw pointer; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator <({{typeName}} left, void* right) => left.{{Config.GenerationRawPointerFieldName}} < right;

				/// <summary>
				/// Compares a {{typeNameCRef}} with a raw pointer to determine which is greater.
				/// </summary>
				/// <param name="left">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <param name="right">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <returns><c><see langword="true"/></c>, if the {{typeNameCRef}} is greater than the raw pointer; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator >({{typeName}} left, void* right) => left.{{Config.GenerationRawPointerFieldName}} > right;

				/// <summary>
				/// Compares a {{typeNameCRef}} with a raw pointer to determine which is less or equal.
				/// </summary>
				/// <param name="left">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <param name="right">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <returns><c><see langword="true"/></c>, if the {{typeNameCRef}} is less than or equal to the raw pointer; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator <=({{typeName}} left, void* right) => left.{{Config.GenerationRawPointerFieldName}} <= right;

				/// <summary>
				/// Compares a {{typeNameCRef}} with a raw pointer to determine which is greater or equal.
				/// </summary>
				/// <param name="left">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <param name="right">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <returns><c><see langword="true"/></c>, if the {{typeNameCRef}} is greater than or equal to the raw pointer; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator >=({{typeName}} left, void* right) => left.{{Config.GenerationRawPointerFieldName}} >= right;

				/// <summary>
				/// Compares a raw pointer with a {{typeNameCRef}} to determine which is less.
				/// </summary>
				/// <param name="left">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <param name="right">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <returns><c><see langword="true"/></c>, if the raw pointer is less than the {{typeNameCRef}}; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator <(void* left, {{typeName}} right) => left < right.{{Config.GenerationRawPointerFieldName}};

				/// <summary>
				/// Compares a raw pointer with a {{typeNameCRef}} to determine which is greater.
				/// </summary>
				/// <param name="left">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <param name="right">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <returns><c><see langword="true"/></c>, if the raw pointer is greater than the {{typeNameCRef}}; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator >(void* left, {{typeName}} right) => left > right.{{Config.GenerationRawPointerFieldName}};

				/// <summary>
				/// Compares a raw pointer with a {{typeNameCRef}} to determine which is less or equal.
				/// </summary>
				/// <param name="left">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <param name="right">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <returns><c><see langword="true"/></c>, if the raw pointer is less than or equal to the {{typeNameCRef}}; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator <=(void* left, {{typeName}} right) => left <= right.{{Config.GenerationRawPointerFieldName}};
				
				/// <summary>
				/// Compares a raw pointer with a {{typeNameCRef}} to determine which is greater or equal.
				/// </summary>
				/// <param name="left">The raw pointer to compare with the {{typeNameCRef}}.</param>
				/// <param name="right">The {{typeNameCRef}} to compare with the raw pointer.</param>
				/// <returns><c><see langword="true"/></c>, if the raw pointer is greater than or equal to the {{typeNameCRef}}; otherwise, <c><see langword="false"/></c>.</returns>
				[global::System.Runtime.CompilerServices.OverloadResolutionPriority(6)]
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static bool operator >=(void* left, {{typeName}} right) => left >= right.{{Config.GenerationRawPointerFieldName}};
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Comparability.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
