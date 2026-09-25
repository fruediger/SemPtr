using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SemPtr.Analyzers;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GenerateFunctionPointerConversions(IncrementalGeneratorPostInitializationContext pic, in FunctionPointerCharacteristics characteristics, StringBuilder builder)
	{
		// See the comment in GenerateFunctionPointerConversions for a brief explanation on how conversions are implemented for pointers.

		builder.Clear();

		ref readonly var fromCharacteristics = ref characteristics;
		var fromTypeName = fromCharacteristics.ToTypeName();
		var fromTypeNameCRef = fromCharacteristics.Typeability switch { Typeability.Typed => $"{fromCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationDelegateTypeParameterName}}}", _ => fromTypeName };

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			partial struct {{fromTypeName}}
			{
			""");

		foreach (var toCharacteristics in FunctionPointerCharacteristics.Enumerate())
		{
			if (fromCharacteristics == toCharacteristics)
			{
				// No conversions with one itself
				continue;
			}

			if (fromCharacteristics.Typeability is Typeability.Untyped && toCharacteristics.Typeability is Typeability.Typed)
			{
				// We can safely skip this case, as there either is no conversion operator to generate,
				// or if there is, it will be generated in the target function pointer type instead, where the type parameter is known.
				continue;
			}

			var toTypeName = toCharacteristics.ToTypeName();
			var toTypeNameCRef = toCharacteristics.Typeability switch { Typeability.Typed => $"{toCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationDelegateTypeParameterName}}}", _ => toTypeName };

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
							/// <returns>A <see cref="{{fromTypeNameCRef}}"/> pointing to the same target function as the specified <see cref="{{toTypeNameCRef}}"/>.</returns>
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							public static {{(viceVersaConversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{fromTypeName}}({{toTypeName}} pointer)
							{
								unsafe
								{
									return new(pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}}, {{Config.GenerationUncheckedConstructorDispatchParameterName}}: default);
								}
							}

						""");
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
						/// <returns>A <see cref="{{toTypeNameCRef}}"/> pointing to the same target function as the specified <see cref="{{fromTypeNameCRef}}"/>.</returns>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public static {{(conversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{toTypeName}}({{fromTypeName}} pointer)
						{
							unsafe
							{
								return new(pointer.{{Config.GenerationRawPointerFieldName}}, {{Config.GenerationUncheckedConstructorDispatchParameterName}}: default);
							}
						}

					""");
			}
		}

		foreach (var toCharacteristics in PointerCharacteristics.Enumerate())
		{
			// We don't need to check for equality of characteristics here, as data pointers and function pointers are guaranteed to be different types.

			var toTypeName = toCharacteristics.ToTypeName();
			var toTypeNameCRef = toCharacteristics.Typeability switch { Typeability.Typed => $"{toCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => toTypeName };

			switch (fromCharacteristics.Typeability, toCharacteristics.Typeability)
			{
				case (Typeability.Typed, Typeability.Typed):
					// That's just impossible to do, as there are no generic conversion operators in C#, and there's no way of specifying the type argument for the target data pointer.
					continue;

				case (Typeability.Untyped, Typeability.Typed):
					// We can safely skip this case, as there either is no conversion operator to generate,
					// or if there is, it will be generated in the target function pointer type instead, where the type parameter is known.
					continue;

				case (Typeability.Typed, Typeability.Untyped):
					// Check if we need to generate a vice-versa conversion operator, which we couldn't generate in what's now the source data pointer type,
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
								/// <returns>A <see cref="{{fromTypeNameCRef}}"/> pointing to the target function that's the same target as the specified <see cref="{{toTypeNameCRef}}"/>, reinterpreted as an executable function.</returns>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public static {{(viceVersaConversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{fromTypeName}}({{toTypeName}} pointer)
								{
									unsafe
									{
										return new(pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}}, {{Config.GenerationUncheckedConstructorDispatchParameterName}}: default);
									}
								}

							""");
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
						/// <returns>A <see cref="{{toTypeNameCRef}}"/> pointing to the target that's the same target function as the specified <see cref="{{fromTypeNameCRef}}"/>, reinterpreted as data.</returns>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public static {{(conversion is Conversion.Implicit ? "implicit" : "explicit")}} operator {{toTypeName}}({{fromTypeName}} pointer)
						{
							unsafe
							{
								return new(pointer.{{Config.GenerationRawPointerFieldName}}, {{Config.GenerationUncheckedConstructorDispatchParameterName}}: default);
							}
						}

					""");
			}
		}

		builder.Append($$"""

				/// <summary>
				/// Converts a <paramref name="raw"/> pointer to a <see cref="{{fromTypeNameCRef}}"/>.
				/// </summary>
				/// <param name="raw">The raw pointer to convert.</param>
				/// <returns>A <see cref="{{fromTypeNameCRef}}"/> that points to the same target function as the specified <paramref name="raw"/> pointer.</returns>
			""");

		if (fromCharacteristics.Nullability is not Nullability.Nullable)
		{
			builder.Append($"""

					/// <remarks>
					/// <para>
					/// The <paramref name="raw"/> pointer must not be <c><see langword="null"/></c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
					/// </para>
					/// </remarks>
					/// <exception cref="global::System.ArgumentNullException"><paramref name="raw"/> is <c><see langword="null"/></c></exception>
				""");
		}

		// See the comment in `SourceGenerator.GeneratePointerConversions.cs` for a brief explanation on why the conversion operator that converts TO a raw pointer must be declared `explicit`.
		// See the comment in `SourceGenerator.GenerateFunctionPointerDeclaration.cs` for the reason why we only provide `void*` pointer conversions, regardless of whether the function pointer type is typed or untyped.
		// For that, the same reasoning applies as for the comment mentioned above: There will be a shipped Roslyn analyzer checking the actual raw function pointer source/target type of the conversion, and erroring out if the conversion would be invalid.

		builder.Append($$"""
			
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static implicit operator {{fromTypeName}}(void* raw) => new(raw);
			
				/// <summary>
				/// Converts a <see cref="{{fromTypeNameCRef}}"/> to a raw pointer.
				/// </summary>
				/// <param name="pointer">The <see cref="{{fromTypeNameCRef}}"/> to convert.</param>
				/// <returns>A raw pointer that points to the same target function as the specified <see cref="{{fromTypeNameCRef}}"/>.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static explicit operator void*({{fromTypeName}} pointer) => pointer.{{Config.GenerationRawPointerFieldName}};
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Conversions.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
