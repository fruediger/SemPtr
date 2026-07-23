using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerNullability(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var typeNameCRef = characteristics.Typeability switch { Typeability.Typed => $"{characteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => typeName };

		builder.Append($$"""
			#nullable enable
			
			namespace {{Config.PointerNamespaceName}};
			
			partial struct {{typeName}}
			{
			""");

		if (characteristics.Nullability is Nullability.Nullable)
		{
			builder.Append($$"""				
				
					/// <summary>
					/// Gets a <see cref="{{typeNameCRef}}"/> that represents a <c><see langword="null"/></c> pointer.
					/// </summary>
					/// <value>
					/// A <see cref="{{typeNameCRef}}"/> that represents a <c><see langword="null"/></c> pointer.
					/// </value>
					public static {{typeName}} Null
					{
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						get
						{
							unsafe
							{
								return new(null);
							}
						}
					}

				""");
		}

		builder.Append($$"""
			
				/// <summary>
				/// Gets a value indicating whether the current pointer is non-<c><see langword="null"/></c>.
				/// </summary>
				/// <value>
				/// <c><see langword="true"/></c>, if the current pointer is non-<c><see langword="null"/></c>; otherwise, <c><see langword="false"/></c>.
				/// </value>
				public readonly bool HasTarget
				{
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					get
					{
						unsafe
						{
							return {{Config.GenerationRawPointerFieldName}} is not null;
						}
					}
				}

			""");

		if (characteristics.Nullability is Nullability.Nullable)
		{
			var nonNullCharacteristics = characteristics with { Nullability = Nullability.NonNull };
			var nonNullCharacteristicsTypeName = nonNullCharacteristics.ToTypeName();

			builder.Append($$"""

					/// <summary>
					/// Attempts to get a non-<c><see langword="null"/></c> pointer from the current pointer.
					/// </summary>
					/// <param name="nonNullPointer">A non-<c><see langword="null"/></c> pointer pointing to the same target as the current pointer, if the current pointer is non-<c><see langword="null"/></c>; otherwise, the <c><see langword="default"/>(<see cref="{{nonNullCharacteristics.Typeability switch { Typeability.Typed => $"{nonNullCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => nonNullCharacteristicsTypeName }}}"/>)</c>.</param>
					/// <returns><c><see langword="true"/></c>, if the current pointer is non-<c><see langword="null"/></c>; otherwise, <c><see langword="false"/></c>.</returns>
					/// <remarks>
					/// <para>
					/// Do <em>not</em> use the resulting <paramref name="nonNullPointer"/> if this method returns <c><see langword="false"/></c>! Doing so might result in undefined behavior.
					/// </para>
					/// </remarks>
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public readonly bool TryGetNonNull(out {{nonNullCharacteristicsTypeName}} nonNullPointer)
					{
						unsafe
						{
							if ({{Config.GenerationRawPointerFieldName}} is not null)
							{
								nonNullPointer = new({{Config.GenerationRawPointerFieldName}});
								return true;
							}

							nonNullPointer = default;
							return false;
						}
					}

				""");
		}

		// To mimic how you can use pointers (or rather integral values) in C, we'll overload the bool operators to allow for something like this:
		//   if (myPointer) { ... } // true if the pointer is non-null

		builder.Append($$"""

				/// <summary>
				/// Determines whether the specified pointer is non-<c><see langword="null"/></c>.
				/// </summary>
				/// <param name="pointer">The pointer to check if it is non-<c><see langword="null"/></c>.</param>
				/// <returns><c><see langword="true"/></c>, if the specified pointer is non-<c><see langword="null"/></c>; otherwise, <c><see langword="false"/></c>.</returns>
				/// <remarks>
				/// <para>
				/// This operator allows for the use of the pointer in conditional statements, such as <c>if (pointer) { ... }</c>, where the condition evaluates to <c><see langword="true"/></c> if the pointer is non-<c><see langword="null"/></c> and <c><see langword="false"/></c> if it is <c><see langword="null"/></c>.
				/// </para>
				/// </remarks>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator true({{typeName}} pointer)
				{
					unsafe
					{
						return pointer.{{Config.GenerationRawPointerFieldName}} is not null;
					}
				}

				/// <summary>
				/// Determines whether the specified pointer is <c><see langword="null"/></c>.
				/// </summary>
				/// <param name="pointer">The pointer to check if it is <c><see langword="null"/></c>.</param>
				/// <returns><c><see langword="true"/></c>, if the specified pointer is <c><see langword="null"/></c>; otherwise, <c><see langword="false"/></c>.</returns>
				/// <remarks>
				/// <para>
				/// This operator allows for the use of the pointer in conditional statements, such as <c>if (pointer) { } else { ... }</c>, where the condition evaluates to <c><see langword="false"/></c> and the `else` branch is taken if the pointer is <c><see langword="null"/></c> and <c><see langword="true"/></c> if it is non-<c><see langword="null"/></c>.
				/// </para>
				/// </remarks>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public static bool operator false({{typeName}} pointer)
				{
					unsafe
					{
						return pointer.{{Config.GenerationRawPointerFieldName}} is null;
					}
				}
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Nullability.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
