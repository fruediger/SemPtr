using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerConversions(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		// We will primarily generate conversion treating the current characteristics as the source characteristics ("fromCharacteristics")
		// and we will iterate over all other possible characteristics as the target characteristics ("toCharacteristics")
		// to determine whether a conversion between them exists and what kind of conversion it is.
		// There are some notable exceptions to this, if our current pointer type is untyped and the target pointer type is typed,
		// we cannot easily generate a conversion operator in the current pointer type for that, as conversion operators cannot be generic in C#.
		// We'll solve this issue by moving the generation of such a conversion operator into the target type instead (where the type parameter is known).

		// With the recent addition of function pointers, we also need to add conversion operators for cross-conversions between data pointers and function pointers.

		builder.Clear();

		ref readonly var fromCharacteristics = ref characteristics;
		var fromTypeName = fromCharacteristics.ToTypeName();
		var fromTypeNameCRef = fromCharacteristics.Typeability switch { Typeability.Typed => $"{fromCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => fromTypeName };

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			partial struct {{fromTypeName}}
			{
			""");

		var conversionOperatorCounter = 0;

		foreach (var toCharacteristics in PointerCharacteristics.Enumerate())
		{
			if (fromCharacteristics == toCharacteristics)
			{
				// No conversions with one itself
				continue;
			}

			if (fromCharacteristics.Typeability is Typeability.Untyped && toCharacteristics.Typeability is Typeability.Typed)
			{
				// We can safely skip this case, as there either is no conversion operator to generate,
				// or if there is, it will be generated in the target pointer type instead, where the type parameter is known.
				continue;
			}

			var toTypeName = toCharacteristics.ToTypeName();
			var toTypeNameCRef = toCharacteristics.Typeability switch { Typeability.Typed => $"{toCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => toTypeName };

			if (fromCharacteristics.Typeability is Typeability.Typed && toCharacteristics.Typeability is Typeability.Untyped)
			{
				// Check if we need to generate a vice-versa conversion operator, which we couldn't generate in what's now the source pointer type,
				// because it was untyped, we're typed, and we need to type parameter to be present.
				// So, if that's the case, just for a moment, we need to swap the meaning of fromCharacteristics and toCharacteristics.

				var viceVersaConversion = toCharacteristics.GetConversionTo(in fromCharacteristics);

				if (viceVersaConversion is not Conversion.None)
				{
					builder.Append($$"""
							
							/// <summary>
							/// Converts a <see cref="{{toTypeNameCRef}}"/> to a <see cref="{{fromTypeNameCRef}}"/>.
							/// </summary>
							/// <param name="pointer">The <see cref="{{toTypeNameCRef}}"/> to convert.</param>
							/// <returns>A <see cref="{{fromTypeNameCRef}}"/> pointing to the same target as the specified <see cref="{{toTypeNameCRef}}"/>.</returns>
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							public static {{(viceVersaConversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{fromTypeName}}({{toTypeName}} pointer)
							{
								unsafe
								{
									return new(unchecked(({{Config.GenerationTypeParameterName}}*)pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
								}
							}

						""");

					conversionOperatorCounter++;
				}
			}

			var conversion = fromCharacteristics.GetConversionTo(in toCharacteristics);

			if (conversion is not Conversion.None)
			{
				builder.Append($$"""
						
						/// <summary>
						/// Converts a <see cref="{{fromTypeNameCRef}}"/> to a <see cref="{{toTypeNameCRef}}"/>.
						/// </summary>
						/// <param name="pointer">The <see cref="{{fromTypeNameCRef}}"/> to convert.</param>
						/// <returns>A <see cref="{{toTypeNameCRef}}"/> pointing to the same target as the specified <see cref="{{fromTypeNameCRef}}"/>.</returns>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public static {{(conversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{toTypeName}}({{fromTypeName}} pointer)
						{
							unsafe
							{
								return new(pointer.{{Config.GenerationRawPointerFieldName}});
							}
						}

					""");

				conversionOperatorCounter++;
			}
		}

		foreach (var toCharacteristics in FunctionPointerCharacteristics.Enumerate())
		{
			// We don't need to check for equality of characteristics here, as data pointers and function pointers are guaranteed to be different types.

			var toTypeName = toCharacteristics.ToTypeName();
			var toTypeNameCRef = toCharacteristics.Typeability switch { Typeability.Typed => $"{toCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => toTypeName };

			switch (fromCharacteristics.Typeability, toCharacteristics.Typeability)
			{
				case (Typeability.Typed, Typeability.Typed):
					// That's just impossible to do, as there are no generic conversion operators in C#, and there's no way of specifying the type argument for the target function pointer.
					continue;

				case (Typeability.Untyped, Typeability.Typed):
					// We can safely skip this case, as there either is no conversion operator to generate,
					// or if there is, it will be generated in the target function pointer type instead, where the type parameter is known.
					continue;

				case (Typeability.Typed, Typeability.Untyped):
					// Check if we need to generate a vice-versa conversion operator, which we couldn't generate in what's now the source function pointer type,
					// because we need to type parameter to be present.
					// So, if that's the case, just for a moment, we need to swap the meaning of fromCharacteristics and toCharacteristics.

					var viceVersaConversion = toCharacteristics.GetConversionTo(in fromCharacteristics);

					if (viceVersaConversion is not Conversion.None)
					{
						builder.Append($$"""
							
								/// <summary>
								/// Converts a <see cref="{{toTypeNameCRef}}"/> to a <see cref="{{fromTypeNameCRef}}"/>.
								/// </summary>
								/// <param name="pointer">The <see cref="{{toTypeNameCRef}}"/> to convert.</param>
								/// <returns>A <see cref="{{fromTypeNameCRef}}"/> pointing to the target that's the same target function as the specified <see cref="{{toTypeNameCRef}}"/>, reinterpreted as data.</returns>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public static {{(viceVersaConversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{fromTypeName}}({{toTypeName}} pointer)
								{
									unsafe
									{
										return new(unchecked(({{Config.GenerationTypeParameterName}}*)pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}}));
									}
								}

							""");

						conversionOperatorCounter++;
					}

					break;
			}

			var conversion = fromCharacteristics.GetConversionTo(in toCharacteristics);

			if (conversion is not Conversion.None)
			{
				builder.Append($$"""
						
						/// <summary>
						/// Converts a <see cref="{{fromTypeNameCRef}}"/> to a <see cref="{{toTypeNameCRef}}"/>.
						/// </summary>
						/// <param name="pointer">The <see cref="{{fromTypeNameCRef}}"/> to convert.</param>
						/// <returns>A <see cref="{{toTypeNameCRef}}"/> pointing to the target function that's the same target as the specified <see cref="{{fromTypeNameCRef}}"/>, reinterpreted as an executable function.</returns>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public static {{(conversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{toTypeName}}({{fromTypeName}} pointer)
						{
							unsafe
							{
								return new(pointer.{{Config.GenerationRawPointerFieldName}});
							}
						}

					""");

				conversionOperatorCounter++;
			}
		}

		if (conversionOperatorCounter is 0)
		{
			// We skip generating the source file if there are no conversion operators to generate; it would just be an empty partial type definition
			return;
		}

		builder.Append("""
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Conversions.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
