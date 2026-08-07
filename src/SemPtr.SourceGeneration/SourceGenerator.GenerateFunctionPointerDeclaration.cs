using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GenerateFunctionPointerDeclaration(IncrementalGeneratorPostInitializationContext pic, in FunctionPointerCharacteristics characteristics, StringBuilder builder)
	{
		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var typeNameWithoutTypeParameter = characteristics.ToTypeNameWithoutTypeParameter();
		var typeNameCRef = characteristics.Typeability switch { Typeability.Typed => $"{typeNameWithoutTypeParameter}{{{Config.GenerationDelegateTypeParameterName}}}", _ => typeName };

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			/// <summary>
			/// Represents a {{characteristics.Nullability switch
			{ 
				Nullability.NonNull => "non-<c><see langword=\"null\"/></c>able ",
				_ => "<c><see langword=\"null\"/></c>-able "
			}}} {{characteristics.Persistency switch
			{
				Persistency.Persistent => "persistent ",
				_ => "transient "
			}}} function pointer pointing to a target function{{characteristics.Typeability switch
			{
				Typeability.Typed => $" whose signature is represented by <typeparamref name=\"{Config.GenerationDelegateTypeParameterName}\"/>",
				_ => string.Empty
			}}}
			/// </summary>
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""

				/// <typeparam name="{{Config.GenerationDelegateTypeParameterName}}">The type of the <see langword="delegate"/> representing the signature of the target function that this pointer points to.</typeparam>
				""");
		}

		builder.Append($$"""

			/// <remarks>
			/// <para>
			/// <see cref="{{typeNameCRef}}"/> has the following characteristics:
			/// <list type="bullet">
			""");

		if (characteristics.Nullability is Nullability.NonNull)
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Non-<c><see langword="null"/></c>able</term>
				/// <description>You can invoke the function pointer's target function directly without checking for <c><see langword="null"/></c>.</description>
				/// </item>
				""");
		}
		else
		{
			builder.Append($$"""
				
				/// <item>
				/// <term><c><see langword="null"/></c>-able</term>
				/// <description>You must check whether the function pointer is <c><see langword="null"/></c> using <see cref="TryGetNonNull(out {{characteristics with { Nullability = Nullability.NonNull } switch { { Typeability: Typeability.Typed } nonNullCharacteristics => $"{nonNullCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationDelegateTypeParameterName}}}", var nonNullCharacteristics => nonNullCharacteristics.ToTypeName() }}})"/> before invoking the function pointer's target function.</description>
				/// </item>
				""");
		}

		if (characteristics.Persistency is Persistency.Persistent)
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Persistent</term>
				/// <description>The function pointer's target function outlives the pointer's initial scope. You can let the function pointer escape the current scope and store it for later invocation. No guarantees are made about how long the target function will remain valid.</description>
				/// </item>
				""");
		}
		else
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Transient</term>
				/// <description>The function pointer's target function is only valid for the lifetime of the pointer. You cannot let the function pointer escape the current scope and you cannot invoke the function pointer's target function after the pointer's lifetime ends.</description>
				/// </item>
				""");
		}

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Typed</term>
				/// <description>The function pointer points to a target function whose signature is represented by <typeparamref name="{{Config.GenerationDelegateTypeParameterName}}"/>.</description>
				/// </item>
				""");
		}
		else
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Untyped</term>
				/// <description>The function pointer points to a target function of unknown signature. You must cast the function pointer to a specific type before you can invoke its target function.</description>
				/// </item>
				""");
		}

		builder.Append("""

			/// </list>
			/// </para>
			""");

		if (characteristics.Nullability is not Nullability.Nullable)
		{
			builder.Append($$"""

				/// <para>
				/// You should never create instances of <see cref="{{typeNameCRef}}"/> using its parameterless constructors or <c><see langword="default"/>(<see cref="{{typeNameCRef}}"/>)</c>. Doing so will result in undefined behavior.
				/// Always use the {{characteristics.Typeability switch { Typeability.Typed => $"<see cref=\"{Config.FunctionPointerInterfaceFromDelegateMethodName}({Config.GenerationDelegateTypeParameterName}?)\"/>", _ => $"<see cref=\"{Config.PointerInterfaceTypeFromRawMethodName}(void*)\"/>" }}}, <see cref="{{Config.PointerInterfaceTypeFromIntPtrMethodName}}(global::System.IntPtr)"/>, or <see cref="{{Config.PointerInterfaceTypeFromUIntPtrMethodName}}(global::System.UIntPtr)"/> methods to create instances of <see cref="{{typeNameCRef}}"/> function pointers.
				/// </para>
				""");
		}

		builder.Append($$"""

			/// </remarks>
			[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]
			public readonly {{characteristics.Persistency switch { not Persistency.Persistent => "ref ", _ => string.Empty }}}partial struct {{typeName}} : {{characteristics.Typeability switch { Typeability.Typed => $"{Config.FunctionPointerTypedInterfaceTypeName}<{Config.GenerationDelegateTypeParameterName}, {typeName}>", _ => $"{Config.FunctionPointerInterfaceTypeName}<{typeName}>" }}}
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""

					where {{Config.GenerationDelegateTypeParameterName}} : notnull, global::System.Delegate
				""");
		}

		// We store the function pointer as a raw void* pointer, because we can't declare the field as delegate*, lacking the parameter and return types.
		// That means we need to be sure that void* can represent function pointers on all platforms that .NET/Mono (because of MAUI/Xamarin) supports.
		// This should be true at the time of writing, but if .NET/Mono ever expands their support for new platforms, this should be re-evaluated.

		builder.Append($$"""

			{
				private unsafe readonly void* {{Config.GenerationRawPointerFieldName}};
			
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				internal unsafe {{typeNameWithoutTypeParameter}}(void* raw) => {{Config.GenerationRawPointerFieldName}} = raw;

			""");

		if (characteristics.Nullability is not Nullability.Nullable)
		{
			builder.Append($$"""

					/// <summary>Do not use. Do not create instances of non-nullable function pointers using parameterless constructors or default values.</summary>
					[global::System.Obsolete("Do not use. Do not create instances of non-nullable function pointers using parameterless constructors or default values.", error: true)]
					public {{typeNameWithoutTypeParameter}}() { }

				""");
		}

		if (characteristics.Typeability is Typeability.Typed)
		{
			// The shipped source generator will generate a typed `Raw` extension property based on the `TDelegate` type argument, e.g. `public delegate* unmanaged<int, void> Raw`.
			// And because the type of the `Raw` property is based on the `TDelegate` type argument, we can't implement it in the generated pointer struct itself.

			builder.Append($$"""

					readonly unsafe void* {{Config.PointerInterfaceTypeName}}.{{Config.PointerInterfaceTypeRawPointerPropertyName}}
					{
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						get => {{Config.GenerationRawPointerFieldName}};
					}

				""");
		}
		else
		{
			builder.Append($$"""

					/// <summary>
					/// Gets a raw pointer pointing to the same target function as the current pointer.
					/// </summary>
					/// <value>
					/// A raw pointer pointing to the same target function as the current pointer.
					/// </value>
					public readonly unsafe void* {{Config.PointerInterfaceTypeRawPointerPropertyName}}
					{
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						get => {{Config.GenerationRawPointerFieldName}};
					}

				""");
		}

		builder.Append($$"""
				
				/// <summary>
				/// Gets an <see cref="global::System.IntPtr"/> that represents the same memory address as the current pointer.
				/// </summary>
				/// <value>A <see cref="global::System.IntPtr"/> that represents the same memory address as the current pointer.</value>
				public readonly global::System.IntPtr {{Config.PointerInterfaceTypeAsIntPtrPropertyName}}
				{
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					get
					{
						unsafe
						{
							return unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}});
						}
					}
				}
			
				/// <summary>
				/// Gets an <see cref="global::System.UIntPtr"/> that represents the same memory address as the current pointer.
				/// </summary>
				/// <value>A <see cref="global::System.UIntPtr"/> that represents the same memory address as the current pointer.</value>				
				public readonly global::System.UIntPtr {{Config.PointerInterfaceTypeAsUIntPtrPropertyName}}
				{
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					get
					{
						unsafe
						{
							return unchecked((global::System.UIntPtr){{Config.GenerationRawPointerFieldName}});
						}
					}
				}

			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			// The same as for the `Raw` property, `FromRaw`'s signature is based on the `TDelegate` type argument,
			// so the shipped source generator will generate it, e.g., `$functionPointer type$ FromRaw(delegate* unmanaged<int, void> raw)`,
			// and we can't implement it in the generated pointer struct itself.

			if (characteristics.Nullability is not Nullability.Nullable)
			{
				builder.Append($$"""

						/// <exception cref="global::System.ArgumentNullException"><paramref name="raw"/> is <c><see langword="null"/></c></exception>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						unsafe static {{typeName}} {{Config.PointerInterfaceTypeName}}<{{typeName}}>.{{Config.PointerInterfaceTypeFromRawMethodName}}(void* raw)
						{
							if (raw is null)
							{
								[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
								static void failRawArgumentNull() => throw new global::System.ArgumentNullException(nameof(raw));

								failRawArgumentNull();
							}

							return new(raw);
						}

					""");
			}
			else
			{
				builder.Append($$"""

						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						unsafe static {{typeName}} {{Config.PointerInterfaceTypeName}}<{{typeName}}>.{{Config.PointerInterfaceTypeFromRawMethodName}}(void* raw) => new(raw);

					""");
			}
		}
		else
		{
			if (characteristics.Nullability is not Nullability.Nullable)
			{
				builder.Append($$"""

						/// <summary>
						/// Creates a <see cref="{{typeNameCRef}}"/> from a <paramref name="raw"/> pointer.
						/// </summary>
						/// <param name="raw">The raw pointer specifying the target function that the resulting <see cref="{{typeNameCRef}}"/> will point to.</param>
						/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same target function as the specified <paramref name="raw"/> pointer.</returns>
						/// <remarks>
						/// <para>
						/// The <paramref name="raw"/> pointer must not be <c><see langword="null"/></c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
						/// </para>
						/// <para>
						/// The resulting <see cref="{{typeNameCRef}}"/> will point to the same target function as <paramref name="raw"/>.
						/// </para>
						/// </remarks>
						/// <exception cref="global::System.ArgumentNullException"><paramref name="raw"/> is <c><see langword="null"/></c></exception>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public unsafe static {{typeName}} {{Config.PointerInterfaceTypeFromRawMethodName}}(void* raw)
						{
							if (raw is null)
							{
								[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
								static void failRawArgumentNull() => throw new global::System.ArgumentNullException(nameof(raw));

								failRawArgumentNull();
							}

							return new(raw);
						}

					""");
			}
			else
			{
				builder.Append($$"""

						/// <summary>
						/// Creates a <see cref="{{typeNameCRef}}"/> from a <paramref name="raw"/> pointer.
						/// </summary>
						/// <param name="raw">The raw pointer specifying the target function that the resulting <see cref="{{typeNameCRef}}"/> will point to.</param>
						/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same target function as the specified <paramref name="raw"/> pointer.</returns>
						/// <remarks>
						/// <para>
						/// The <paramref name="raw"/> pointer may be <c><see langword="null"/></c>. If it is, the resulting <see cref="{{typeNameCRef}}"/> will represent a null pointer.
						/// </para>
						/// <para>
						/// The resulting <see cref="{{typeNameCRef}}"/> will point to the same target function as <paramref name="raw"/>.
						/// </para>
						/// </remarks>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public unsafe static {{typeName}} {{Config.PointerInterfaceTypeFromRawMethodName}}(void* raw) => new(raw);

					""");
			}
		}

		if (characteristics.Nullability is not Nullability.Nullable)
		{
			builder.Append($$"""
				
					/// <summary>
					/// Creates a <see cref="{{typeNameCRef}}"/> from a memory address given as a <see cref="global::System.IntPtr"/>.
					/// </summary>
					/// <param name="value">The memory address to create a <see cref="{{typeNameCRef}}"/> from.</param>
					/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same memory address as the specified <paramref name="value"/>.</returns>
					/// <remarks>
					/// <para>
					/// The <paramref name="value"/> must not be <c>0</c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
					/// </para>
					/// </remarks>
					/// <exception cref="global::System.ArgumentNullException"><paramref name="value"/> is <c>0</c></exception>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static {{typeName}} {{Config.PointerInterfaceTypeFromIntPtrMethodName}}(global::System.IntPtr value)
					{
						unsafe
						{
							if (value is 0)
							{
								[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
								static void failValueArgumentNull() => throw new global::System.ArgumentNullException(nameof(value));
				
								failValueArgumentNull();
							}
				
							return new(unchecked((void*)value));
						}
					}
				
					/// <summary>
					/// Creates a <see cref="{{typeNameCRef}}"/> from a memory address given as a <see cref="global::System.IntPtr"/>.
					/// </summary>
					/// <param name="value">The memory address to create a <see cref="{{typeNameCRef}}"/> from.</param>
					/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same memory address as the specified <paramref name="value"/>.</returns>
					/// <remarks>
					/// <para>
					/// The <paramref name="value"/> must not be <c>0</c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
					/// </para>
					/// </remarks>
					/// <exception cref="global::System.ArgumentNullException"><paramref name="value"/> is <c>0</c></exception>
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static {{typeName}} {{Config.PointerInterfaceTypeFromUIntPtrMethodName}}(global::System.UIntPtr value)
					{
						unsafe
						{
							if (value is 0)
							{
								[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
								static void failValueArgumentNull() => throw new global::System.ArgumentNullException(nameof(value));
				
								failValueArgumentNull();
							}
				
							return new(unchecked((void*)value));
						}
					}

				""");
		}
		else
		{
			builder.Append($$"""

					/// <summary>
					/// Creates a <see cref="{{typeNameCRef}}"/> from a memory address given as a <see cref="global::System.IntPtr"/>.
					/// </summary>
					/// <param name="value">The memory address to create a <see cref="{{typeNameCRef}}"/> from.</param>
					/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same memory address as the specified <paramref name="value"/>.</returns>
					[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static {{typeName}} {{Config.PointerInterfaceTypeFromIntPtrMethodName}}(global::System.IntPtr value)
					{
						unsafe
						{				
							return new(unchecked((void*)value));
						}
					}
				
					/// <summary>
					/// Creates a <see cref="{{typeNameCRef}}"/> from a memory address given as a <see cref="global::System.IntPtr"/>.
					/// </summary>
					/// <param name="value">The memory address to create a <see cref="{{typeNameCRef}}"/> from.</param>
					/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same memory address as the specified <paramref name="value"/>.</returns>
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static {{typeName}} {{Config.PointerInterfaceTypeFromUIntPtrMethodName}}(global::System.UIntPtr value)
					{
						unsafe
						{				
							return new(unchecked((void*)value));
						}
					}

				""");
		}

		if (characteristics.Typeability is Typeability.Typed)
		{
			if (characteristics.Nullability is not Nullability.Nullable)
			{
				builder.Append($$"""

						/// <summary>
						/// Creates a <see cref="{{typeNameCRef}}"/> from a <see langword="delegate"/> of type <typeparamref name="{{Config.GenerationDelegateTypeParameterName}}"/>.
						/// </summary>
						/// <param name="delegate">The <see langword="delegate"/> to create a <see cref="{{typeNameCRef}}"/> from.</param>
						/// <returns>A <see cref="{{typeNameCRef}}"/> pointing to the target function represented by <paramref name="delegate"/>.</returns>
						/// <remarks>
						/// <para>
						/// The <paramref name="delegate"/> must not be <c><see langword="null"/></c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
						/// </para>
						/// </remarks>
						/// <exception cref="global::System.ArgumentNullException"><paramref name="delegate"/> is <c><see langword="null"/></c></exception>					
					#pragma warning disable CS8767 // The nullability of the `@delegate` parameter doesn't match the inherited nullability of the corresponding parameter in the interface declaration.
					                               // That's fine here and intentional, because we check for null and throw an exception if it is, as well as warn the user that the parameter can't be null through a `DisallowNull` attribute and through documentation.
						public static {{typeName}} {{Config.FunctionPointerInterfaceFromDelegateMethodName}}([global::System.Diagnostics.CodeAnalysis.NotNull, System.Diagnostics.CodeAnalysis.DisallowNull] {{Config.GenerationDelegateTypeParameterName}}? @delegate)
					#pragma warning restore CS8767
						{
							unsafe
							{
								if (@delegate is null)
								{
									[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
									static void failDelegateArgumentNull() => throw new global::System.ArgumentNullException(nameof(@delegate));

									failDelegateArgumentNull();
								}

								return new(unchecked((void*)global::System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(@delegate)));
							}
						}

						/// <summary>
						/// Converts the current function pointer to a <see langword="delegate"/> of type <typeparamref name="TDelegate"/>.
						/// </summary>
						/// <returns>A <see langword="delegate"/> of type <typeparamref name="TDelegate"/> representing the target function.</returns>
						/// <remarks>
						/// <para>
						/// The current instance must not be a null pointer. If it is, an <see cref="global::System.InvalidOperationException"/> will be thrown.
						/// </para>
						/// </remarks>
						/// <exception cref="global::System.InvalidOperationException">The current function pointer is a <c><see langword="null"/></c> pointer</exception>
						[return: global::System.Diagnostics.CodeAnalysis.NotNull]
						public readonly {{Config.GenerationDelegateTypeParameterName}}? {{Config.FunctionPointerInterfaceToDelegateMethodName}}()
						{
							unsafe
							{
								if (mRaw is null)
								{
									[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
									static void failPointerIsNull() => throw new global::System.InvalidOperationException($"The current {nameof({{typeNameWithoutTypeParameter}}{{characteristics.Typeability switch { Typeability.Typed => $"<>", _ => string.Empty }}})} is unexpectedly null.");

									failPointerIsNull();
								}

								return global::System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<{{Config.GenerationDelegateTypeParameterName}}>(unchecked((global::System.IntPtr)mRaw));
							}
						}

					""");
			}
			else
			{
				builder.Append($$"""

						/// <summary>
						/// Creates a <see cref="{{typeNameCRef}}"/> from a <see langword="delegate"/> of type <typeparamref name="{{Config.GenerationDelegateTypeParameterName}}"/>.
						/// </summary>
						/// <param name="delegate">The <see langword="delegate"/> to create a <see cref="{{typeNameCRef}}"/> from.</param>
						/// <returns>A <see cref="{{typeNameCRef}}"/> pointing to the target function represented by <paramref name="delegate"/>.</returns>
						/// <remarks>
						/// <para>
						/// The <paramref name="delegate"/> pointer may be <c><see langword="null"/></c>. If it is, then the resulting <see cref="{{typeNameCRef}}"/> will represent a <c><see langword="null"/></c> pointer.
						/// </para>
						/// </remarks>
						public static {{typeName}} {{Config.FunctionPointerInterfaceFromDelegateMethodName}}({{Config.GenerationDelegateTypeParameterName}}? @delegate)
						{
							unsafe
							{
								if (@delegate is null)
								{
									return Null;
								}

								return new(unchecked((void*)global::System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(@delegate)));
							}
						}

						/// <summary>
						/// Converts the current function pointer to a <see langword="delegate"/> of type <typeparamref name="TDelegate"/>.
						/// </summary>
						/// <returns>A <see langword="delegate"/> of type <typeparamref name="TDelegate"/> representing the target function.</returns>
						/// <remarks>
						/// <para>
						/// The current instance may be a <c><see langword="null"/></c> pointer. If it is, then the resulting <see langword="delegate"/> will be <c><see langword="null"/></c>.
						/// </para>
						/// </remarks>
						public readonly {{Config.GenerationDelegateTypeParameterName}}? {{Config.FunctionPointerInterfaceToDelegateMethodName}}()
						{
							unsafe
							{
								if (mRaw is null)
								{
									return null;
								}

								return global::System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<{{Config.GenerationDelegateTypeParameterName}}>(unchecked((global::System.IntPtr)mRaw));
							}
						}

					""");
			}
		}
		else
		{
			var typeCharacteristics = characteristics with { Typeability = Typeability.Typed };
			var typedTypeName = typeCharacteristics.ToTypeName();
			var typedTypeNameCRef = $"{typeCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationDelegateTypeParameterName}}}";

			builder.Append($$"""

					/// <inheritdoc cref="{{typedTypeNameCRef}}.{{Config.FunctionPointerInterfaceFromDelegateMethodName}}({{Config.GenerationDelegateTypeParameterName}})"/>
					/// <typeparam name="{{Config.GenerationDelegateTypeParameterName}}">The type of the <see langword="delegate"/> representing the signature of the target function that the resulting <see cref="{{typedTypeNameCRef}}"/> will point to.</typeparam>
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static {{typedTypeName}} {{Config.FunctionPointerInterfaceFromDelegateMethodName}}<{{Config.GenerationDelegateTypeParameterName}}>({{Config.GenerationDelegateTypeParameterName}}{{characteristics.Nullability switch { Nullability.Nullable => "?", _ => string.Empty }}} @delegate)
						where {{Config.GenerationDelegateTypeParameterName}} : notnull, global::System.Delegate
						=> {{typedTypeName}}.{{Config.FunctionPointerInterfaceFromDelegateMethodName}}(@delegate);

				""");
		}

		builder.Append($$"""
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
