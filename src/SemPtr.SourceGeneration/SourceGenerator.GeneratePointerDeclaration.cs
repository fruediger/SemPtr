using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

internal partial class SourceGenerator
{
	private static void GeneratePointerDeclaration(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var typeNameWithoutTypeParameter = characteristics.ToTypeNameWithoutTypeParameter();
		var typeNameCRef = characteristics.Typeability switch { Typeability.Typed => $"{typeNameWithoutTypeParameter}{{{Config.GenerationTypeParameterName}}}", _ => typeName };

		var rawPointerType = characteristics switch
		{
			{ Typeability: Typeability.Typed } => $"{Config.GenerationTypeParameterName}*",
			_ => "void*"
		};

		builder.Append($$"""
			#nullable enable

			namespace {{Config.PointerNamespaceName}};

			/// <summary>
			/// Represents a {{characteristics.Nullability switch {
				Nullability.NonNull => "non-<c><see langword=\"null\"/></c>able ",
				_ => "<c><see langword=\"null\"/></c>-able "
			}}} {{characteristics.Persistency switch {
				Persistency.Persistent => "persistent ",
				_ => "transient "
			}}} pointer to a {{characteristics.Accessibility switch { 
				Accessibility.ReadOnly => "read-only ",
				Accessibility.Uninitialized => "uninitialized ",
				_ => string.Empty
			}}}{{characteristics.Sequencability switch {
				Sequencability.Sequence => "contiguous sequence of targets",
				_ => "target"
			}}}{{characteristics.Typeability switch {
				Typeability.Typed => $" of type {Config.GenerationTypeParameterName}",
				_ => string.Empty
			}}}.
			/// </summary>
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
				/// <description>You can {{characteristics.Accessibility switch { Accessibility.Uninitialized => "initialize", _ => "access" }}} the pointer's target directly.</description>
				/// </item>
				""");
		}
		else
		{
			builder.Append($$"""
				
				/// <item>
				/// <term><c><see langword="null"/></c>-able</term>
				/// <description>You must check whether the pointer is <c><see langword="null"/></c> using <see cref="TryGetNonNull(out {{characteristics with { Nullability = Nullability.NonNull } switch { { Typeability: Typeability.Typed } nonNullCharacteristics => $"{nonNullCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", var nonNullCharacteristics => nonNullCharacteristics.ToTypeName() } }})"/> before {{characteristics.Accessibility switch { Accessibility.Uninitialized => "initializing", _ => "accessing" }}} the pointer's target.</description>
				/// </item>
				""");
		}

		if (characteristics.Persistency is Persistency.Persistent)
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Persistent</term>
				/// <description>The pointer's target outlives the pointer's initial scope. You can let the pointer escape the current scope and store it for later access. No guarantees are made about how long the target will remain valid or who owns it.</description>
				/// </item>
				""");
		}
		else
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Transient</term>
				/// <description>The pointer's target is only valid for the lifetime of the pointer. You cannot let the pointer escape the current scope and you cannot access the pointer's target after the pointer's lifetime ends.</description>
				/// </item>
				""");
		}

		if (characteristics.Sequencability is Sequencability.Sequence)
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Contiguous sequence</term>
				/// <description>The pointer points to a contiguous sequence of targets.{{characteristics.Typeability switch { Typeability.Typed => $" You can access the pointer's target objects individually via indexing. You can perform pointer arithmetic with the pointer.", _ => string.Empty }}}</description>
				/// </item>
				""");
		}
		else
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Single object</term>
				/// <description>The pointer points to a single target.{{characteristics.Typeability switch { Typeability.Typed => $" You can only access that target directly. You cannot perform pointer arithmetic with the pointer.", _ => string.Empty }}}</description>
				/// </item>
				""");
		}

		switch (characteristics.Accessibility)
		{
			case Accessibility.ReadOnly:
				builder.Append($$"""
				
				/// <item>
				/// <term>Read-only</term>
				/// <description>{{characteristics.Sequencability switch {
					Sequencability.Sequence => "You can only read the pointer's targets. You cannot modify them.",
					_ => "You can only read the pointer's target. You cannot modify it."
				}}}
				/// </description>
				/// </item>
				""");
				break;

			case Accessibility.Uninitialized:
				builder.Append($$"""
				
				/// <item>
				/// <term>Uninitialized</term>
				/// <description>{{characteristics.Sequencability switch {
					Sequencability.Sequence => "The pointer's targets are uninitialized and you should initialize them. You cannot read them before writing to them.",
					_ => "The pointer's target is uninitialized and you should initialize it. You cannot read it before writing to it."
				}}}
				/// </description>
				/// </item>
				""");
				break;

			default:
				builder.Append($$"""
				
				/// <item>
				/// <term>Read-write</term>
				/// <description>You can read and write the pointer's target.</description>
				/// </item>
				""");
				break;
		}

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Typed</term>
				/// <description>The pointer points to {{characteristics.Sequencability switch { Sequencability.Sequence => "a contiguous sequence of targets", _ => "a single target" }}} of type <typeparamref name="{{Config.GenerationTypeParameterName}}"/>.</description>
				/// </item>
				""");
		}
		else
		{
			builder.Append($$"""
				
				/// <item>
				/// <term>Untyped</term>
				/// <description>The pointer points to {{characteristics.Sequencability switch { Sequencability.Sequence => "a contiguous sequence of targets", _ => "a single target" }}} of unknown type. You must cast the pointer to a specific type before you can access its target{{characteristics.Sequencability switch { Sequencability.Sequence => "s", _ => string.Empty }}}.</description>
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
				/// You should never create instances of <see cref="{{typeNameCRef}}"/> pointers using its parameterless constructors or <c><see langword="default"/>(<see cref="{{typeNameCRef}}"/>)</c>. Doing so will result in undefined behavior.
				/// Always use the <see cref="{{typeNameCRef}}.{{typeNameWithoutTypeParameter}}({{rawPointerType}})"/> constructor or the <see cref="FromIntPtr(global::System.IntPtr)"/> and <see cref="FromUIntPtr(global::System.UIntPtr)"/> methods to create instances of <see cref="{{typeNameCRef}}"/> pointers.
				/// </para>
				""");
		}

		builder.Append($$"""

			/// </remarks>
			[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]
			public readonly {{characteristics switch { { Persistency: not Persistency.Persistent } => "ref ", _ => string.Empty }}}partial struct {{typeName}} : {{Config.PointerInterfaceTypeName}}{{characteristics switch { { Typeability: Typeability.Typed } => $"<{Config.GenerationTypeParameterName}>", _ => string.Empty }}}
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""

					where {{Config.GenerationTypeParameterName}} : unmanaged{{characteristics switch { { Persistency: not Persistency.Persistent } => ", allows ref struct", _ => string.Empty }}}
				""");
		}

		builder.Append($$"""

			{
				private unsafe readonly {{rawPointerType}} {{Config.GenerationRawPointerFieldName}};

			""");

		if (characteristics.Nullability is not Nullability.Nullable)
		{
			builder.Append($$"""

				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				internal unsafe {{typeNameWithoutTypeParameter}}({{rawPointerType}} raw, bool {{Config.GenerationUncheckedConstructorDispatcherParameterName}}) => {{Config.GenerationRawPointerFieldName}} = raw;
			
				/// <summary>
				/// Creates a <see cref="{{typeNameCRef}}"/> from a <paramref name="raw"/> pointer.
				/// </summary>
				/// <param name="raw">The raw pointer specifying the {{characteristics.Sequencability switch { Sequencability.Sequence => "contiguous target sequence", _ => "target" }}} that the resulting <see cref="{{typeNameCRef}}"/> will point to.</param>
				/// <remarks>
				/// <para>
				/// The <paramref name="raw"/> pointer must not be <c><see langword="null"/></c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
				/// </para>
				/// <para>
				/// The resulting <see cref="{{typeNameCRef}}"/> will point to the same {{characteristics.Sequencability switch { Sequencability.Sequence => "contiguous target sequence", _ => "target" }}} as <paramref name="raw"/>.
				/// </para>
				/// </remarks>
				/// <exception cref="global::System.ArgumentNullException"><paramref name="raw"/> is <c><see langword="null"/></c></exception>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe {{typeNameWithoutTypeParameter}}({{rawPointerType}} raw)
				{
					if (raw is null)
					{
						[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
						static void failRawArgumentNull() => throw new global::System.ArgumentNullException(nameof(raw));

						failRawArgumentNull();
					}

					{{Config.GenerationRawPointerFieldName}} = raw;
				}

				/// <summary>Do not use. Do not create instances of non-nullable pointers using parameterless constructors or default values.</summary>
				[global::System.Obsolete("Do not use. Do not create instances of non-nullable pointers using parameterless constructors or default values.", error: true)]
				public {{typeNameWithoutTypeParameter}}() { }

			""");
		}
		else
		{
			builder.Append($$"""
			
				/// <summary>
				/// Creates a <see cref="{{typeNameCRef}}"/> from a <paramref name="raw"/> pointer.
				/// </summary>
				/// <param name="raw">The raw pointer specifying the {{characteristics.Sequencability switch { Sequencability.Sequence => "contiguous target sequence", _ => "target" }}} that the resulting <see cref="{{typeNameCRef}}"/> will point to.</param>
				/// <remarks>
				/// <para>
				/// The <paramref name="raw"/> pointer may be <c><see langword="null"/></c>. If it is, the resulting <see cref="{{typeNameCRef}}"/> will represent a null pointer.
				/// </para>
				/// <para>
				/// The resulting <see cref="{{typeNameCRef}}"/> will point to the same {{characteristics.Sequencability switch { Sequencability.Sequence => "contiguous target sequence", _ => "target" }}} as <paramref name="raw"/>.
				/// </para>
				/// </remarks>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe {{typeNameWithoutTypeParameter}}({{rawPointerType}} raw) => {{Config.GenerationRawPointerFieldName}} = raw;

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
				/// Gets a raw pointer pointing to the same target as the current pointer.
				/// </summary>
				/// <value>
				/// A raw pointer pointing to the same target as the current pointer.
				/// </value>
				public readonly unsafe {{rawPointerType}} {{Config.PointerInterfaceTypeRawPointerPropertyName}}
				{
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					get => {{Config.GenerationRawPointerFieldName}};
				}

			""");

		if (characteristics is { Typeability: Typeability.Typed })
		{
			builder.Append($$"""

					readonly unsafe void* {{Config.PointerInterfaceTypeName}}.{{Config.PointerInterfaceTypeRawPointerPropertyName}}
					{
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						get => {{Config.GenerationRawPointerFieldName}};
					}

				""");
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
					public static {{typeName}} FromIntPtr(global::System.IntPtr value)
					{
						unsafe
						{
							if (value is 0)
							{
								[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
								static void failValueArgumentNull() => throw new global::System.ArgumentNullException(nameof(value));
				
								failValueArgumentNull();
							}
				
							return new(unchecked(({{rawPointerType}})value), {{Config.GenerationUncheckedConstructorDispatcherParameterName}}: default);
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
					public static {{typeName}} FromUIntPtr(global::System.UIntPtr value)
					{
						unsafe
						{
							if (value is 0)
							{
								[global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
								static void failValueArgumentNull() => throw new global::System.ArgumentNullException(nameof(value));
				
								failValueArgumentNull();
							}
				
							return new(unchecked(({{rawPointerType}})value), {{Config.GenerationUncheckedConstructorDispatcherParameterName}}: default);
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
					public static {{typeName}} FromIntPtr(global::System.IntPtr value)
					{
						unsafe
						{				
							return new(unchecked(({{rawPointerType}})value));
						}
					}
				
					/// <summary>
					/// Creates a <see cref="{{typeNameCRef}}"/> from a memory address given as a <see cref="global::System.IntPtr"/>.
					/// </summary>
					/// <param name="value">The memory address to create a <see cref="{{typeNameCRef}}"/> from.</param>
					/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same memory address as the specified <paramref name="value"/>.</returns>
					[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
					public static {{typeName}} FromUIntPtr(global::System.UIntPtr value)
					{
						unsafe
						{				
							return new(unchecked(({{rawPointerType}})value));
						}
					}

				""");
		}

		builder.Append($$"""

				/// <summary>
				/// Gets a <see cref="global::System.IntPtr"/> that represents the same memory address as the current pointer.
				/// </summary>
				/// <returns>A <see cref="global::System.IntPtr"/> that represents the same memory address as the current pointer.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly global::System.IntPtr ToIntPtr()
				{
					unsafe
					{
						return unchecked((global::System.IntPtr){{Config.GenerationRawPointerFieldName}});
					}
				}

				/// <summary>
				/// Gets a <see cref="global::System.UIntPtr"/> that represents the same memory address as the current pointer.
				/// </summary>
				/// <returns>A <see cref="global::System.UIntPtr"/> that represents the same memory address as the current pointer.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public readonly global::System.UIntPtr ToUIntPtr()
				{
					unsafe
					{
						return unchecked((global::System.UIntPtr){{Config.GenerationRawPointerFieldName}});
					}
				}

				/// <summary>
				/// Converts a raw pointer to a <see cref="{{typeNameCRef}}"/>.
				/// </summary>
				/// <param name="raw">The raw pointer to convert.</param>
				/// <returns>A <see cref="{{typeNameCRef}}"/> that points to the same target as the specified <paramref name="raw"/> pointer.</returns>
				/// <remarks>
				/// <para>
				/// The <paramref name="raw"/> pointer must not be <c><see langword="null"/></c>. If it is, an <see cref="global::System.ArgumentNullException"/> will be thrown.
				/// </para>
				/// </remarks>
				/// <exception cref="global::System.ArgumentNullException"><paramref name="raw"/> is <c><see langword="null"/></c></exception>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static implicit operator {{typeName}}({{rawPointerType}} raw) => new(raw);

				/// <summary>
				/// Converts a <see cref="{{typeNameCRef}}"/> to a raw pointer.
				/// </summary>
				/// <param name="pointer">The <see cref="{{typeNameCRef}}"/> to convert.</param>
				/// <returns>A raw pointer that points to the same target as the specified <paramref name="pointer"/>.</returns>
				[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
				public unsafe static explicit operator {{rawPointerType}}({{typeName}} pointer) => pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}};
			}
			
			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
