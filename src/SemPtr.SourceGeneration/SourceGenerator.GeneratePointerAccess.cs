using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerAccess(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		if (characteristics is not { Nullability: Nullability.NonNull, Typeability: Typeability.Typed })
		{
			return;
		}

		builder.Clear();

		var typeName = characteristics.ToTypeName();

		builder.Append($$"""
			#nullable enable
			
			namespace {{Config.PointerNamespaceName}};
			
			partial struct {{typeName}}
			{
			""");

		switch (characteristics.Accessibility)
		{
			case Accessibility.Random:
				builder.Append($$"""

						/// <summary>
						/// Gets a reference to the target value the current pointer is pointing to.
						/// </summary>
						/// <value>
						/// A reference to the target value the current pointer is pointing to.
						/// </value>
						/// <remarks>
						/// <para>
						/// Accessing the target value through this property is equivalent to dereferencing the pointer.
						/// </para>
						/// </remarks>
						public readonly ref {{Config.GenerationTypeParameterName}} {{Config.GenerationTargetPropertyName}}
						{
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							get
							{
								unsafe
								{
									return ref *{{Config.GenerationRawPointerFieldName}};
								}
							}
						}

					""");

				if (characteristics.Sequencability is Sequencability.Sequence)
				{
					builder.Append($$"""
							
							/// <summary>
							/// Gets a reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </summary>
							/// <param name="index">The index of the target value in the sequence.</param>
							/// <value>
							/// A reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </value>
							/// <remarks>
							/// <para>
							/// Accessing the target value at the specified <paramref name="index"/> through this property is equivalent to dereferencing the pointer at the specified <paramref name="index"/> offset.
							/// </para>
							/// </remarks>
							[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
							public readonly ref {{Config.GenerationTypeParameterName}} this[nint index]
							{
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								get
								{
									unsafe
									{
										return ref {{Config.GenerationRawPointerFieldName}}[index];
									}
								}
							}

							/// <summary>
							/// Gets a reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </summary>
							/// <param name="index">The index of the target value in the sequence.</param>
							/// <value>
							/// A reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </value>
							/// <remarks>
							/// <para>
							/// Accessing the target value at the specified <paramref name="index"/> through this property is equivalent to dereferencing the pointer at the specified <paramref name="index"/> offset.
							/// </para>
							/// </remarks>
							public readonly ref {{Config.GenerationTypeParameterName}} this[nuint index]
							{
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								get
								{
									unsafe
									{
										return ref {{Config.GenerationRawPointerFieldName}}[index];
									}
								}
							}

						""");

					if (characteristics.Persistency is Persistency.Persistent)
					{
						builder.Append($$"""

								/// <summary>
								/// Gets a <see cref="global::System.Span{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
								/// </summary>
								/// <param name="start">The start index of the sequence.</param>
								/// <param name="length">The length of the sequence.</param>
								/// <returns>A <see cref="global::System.Span{T}"/> that represents the specified range of values.</returns>
								/// <remarks>
								/// <para>
								/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
								/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
								/// </para>
								/// </remarks>
								[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly global::System.Span<{{Config.GenerationTypeParameterName}}> AsSpan(nint start, int length)
								{
									unsafe
									{
										return new({{Config.GenerationRawPointerFieldName}} + start, length);
									}
								}

								/// <summary>
								/// Gets a <see cref="global::System.Span{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
								/// </summary>
								/// <param name="start">The start index of the sequence.</param>
								/// <param name="length">The length of the sequence.</param>
								/// <returns>A <see cref="global::System.Span{T}"/> that represents the specified range of values.</returns>
								/// <remarks>
								/// <para>
								/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
								/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
								/// </para>
								/// </remarks>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly global::System.Span<{{Config.GenerationTypeParameterName}}> AsSpan(nuint start, int length)
								{
									unsafe
									{
										return new({{Config.GenerationRawPointerFieldName}} + start, length);
									}
								}

								/// <summary>
								/// Gets a <see cref="global::System.Span{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, having the specified <paramref name="length"/>.
								/// </summary>
								/// <param name="length">The length of the sequence.</param>
								/// <returns>A <see cref="global::System.Span{T}"/> that represents the specified range of values.</returns>
								/// <remarks>
								/// <para>
								/// There are no bounds checks performed regarding the <paramref name="length"/> parameter.
								/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
								/// </para>
								/// </remarks>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly global::System.Span<{{Config.GenerationTypeParameterName}}> AsSpan(int length)
									=> AsSpan(0, length);

							""");
					}
					else
					{
						// See the comment in the `Accessibility.Uninitialized` case for why we need to generate this as an extension method instead of a member method.

						pic.AddSource($"{Config.PointerNamespaceName}.{Config.PointerExtensionsTypeName}.{characteristics.ToFileNamePrefix()}.Access.g.cs", SourceText.From(
							text: $$"""
								#nullable enable
								
								namespace {{Config.PointerNamespaceName}};

								partial class {{Config.PointerExtensionsTypeName}}
								{
									extension<{{Config.GenerationTypeParameterName}}>({{typeName}} pointer)
										where {{Config.GenerationTypeParameterName}} : unmanaged
									{
										/// <summary>
										/// Gets a <see cref="global::System.Span{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
										/// </summary>
										/// <param name="start">The start index of the sequence.</param>
										/// <param name="length">The length of the sequence.</param>
										/// <returns>A <see cref="global::System.Span{T}"/> that represents the specified range of values.</returns>
										/// <remarks>
										/// <para>
										/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
										/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
										/// </para>
										/// </remarks>
										[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
										[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
										public global::System.Span<{{Config.GenerationTypeParameterName}}> AsSpan(nint start, int length)
										{
											unsafe
											{
												return new(pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}} + start, length);
											}
										}

										/// <summary>
										/// Gets a <see cref="global::System.Span{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
										/// </summary>
										/// <param name="start">The start index of the sequence.</param>
										/// <param name="length">The length of the sequence.</param>
										/// <returns>A <see cref="global::System.Span{T}"/> that represents the specified range of values.</returns>
										/// <remarks>
										/// <para>
										/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
										/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
										/// </para>
										/// </remarks>
										[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
										public global::System.Span<{{Config.GenerationTypeParameterName}}> AsSpan(nuint start, int length)
										{
											unsafe
											{
												return new(pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}} + start, length);
											}
										}

										/// <summary>
										/// Gets a <see cref="global::System.Span{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, having the specified <paramref name="length"/>.
										/// </summary>
										/// <param name="length">The length of the sequence.</param>
										/// <returns>A <see cref="global::System.Span{T}"/> that represents the specified range of values.</returns>
										/// <remarks>
										/// <para>
										/// There are no bounds checks performed regarding the <paramref name="length"/> parameter.
										/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
										/// </para>
										/// </remarks>
										[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
										public global::System.Span<{{Config.GenerationTypeParameterName}}> AsSpan(int length)
											=> pointer.AsSpan(0, length);
									}
								}

								#nullable restore
								""",
							encoding: Encoding.UTF8
						));
					}
				}
				break;

			case Accessibility.ReadOnly:
				builder.Append($$"""

						/// <summary>
						/// Gets a read-only reference to the target value the current pointer is pointing to.
						/// </summary>
						/// <value>
						/// A read-only reference to the target value the current pointer is pointing to.
						/// </value>
						/// <remarks>
						/// <para>
						/// Accessing the target value through this property is equivalent to dereferencing the pointer.
						/// </para>
						/// </remarks>
						public readonly ref readonly {{Config.GenerationTypeParameterName}} {{Config.GenerationTargetPropertyName}}
						{
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							get
							{
								unsafe
								{
									return ref *{{Config.GenerationRawPointerFieldName}};
								}
							}
						}

					""");

				if (characteristics.Sequencability is Sequencability.Sequence)
				{
					builder.Append($$"""
							
							/// <summary>
							/// Gets a read-only reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </summary>
							/// <param name="index">The index of the target value in the sequence.</param>
							/// <value>
							/// A read-only reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </value>
							/// <remarks>
							/// <para>
							/// Accessing the target value at the specified <paramref name="index"/> through this property is equivalent to dereferencing the pointer at the specified <paramref name="index"/> offset.
							/// </para>
							/// </remarks>
							[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
							public readonly ref readonly {{Config.GenerationTypeParameterName}} this[nint index]
							{
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								get
								{
									unsafe
									{
										return ref {{Config.GenerationRawPointerFieldName}}[index];
									}
								}
							}

							/// <summary>
							/// Gets a read-only reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </summary>
							/// <param name="index">The index of the target value in the sequence.</param>
							/// <value>
							/// A read-only reference to the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to.
							/// </value>
							/// <remarks>
							/// <para>
							/// Accessing the target value at the specified <paramref name="index"/> through this property is equivalent to dereferencing the pointer at the specified <paramref name="index"/> offset.
							/// </para>
							/// </remarks>
							public readonly ref readonly {{Config.GenerationTypeParameterName}} this[nuint index]
							{
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								get
								{
									unsafe
									{
										return ref {{Config.GenerationRawPointerFieldName}}[index];
									}
								}
							}

						""");

					if (characteristics.Persistency is Persistency.Persistent)
					{
						builder.Append($$"""

								/// <summary>
								/// Gets a <see cref="global::System.ReadOnlySpan{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
								/// </summary>
								/// <param name="start">The start index of the sequence.</param>
								/// <param name="length">The length of the sequence.</param>
								/// <returns>A <see cref="global::System.ReadOnlySpan{T}"/> that represents the specified range of values.</returns>
								/// <remarks>
								/// <para>
								/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
								/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
								/// </para>
								/// </remarks>
								[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> AsSpan(nint start, int length)
								{
									unsafe
									{
										return new({{Config.GenerationRawPointerFieldName}} + start, length);
									}
								}

								/// <summary>
								/// Gets a <see cref="global::System.ReadOnlySpan{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
								/// </summary>
								/// <param name="start">The start index of the sequence.</param>
								/// <param name="length">The length of the sequence.</param>
								/// <returns>A <see cref="global::System.ReadOnlySpan{T}"/> that represents the specified range of values.</returns>
								/// <remarks>
								/// <para>
								/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
								/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
								/// </para>
								/// </remarks>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> AsSpan(nuint start, int length)
								{
									unsafe
									{
										return new({{Config.GenerationRawPointerFieldName}} + start, length);
									}
								}

								/// <summary>
								/// Gets a <see cref="global::System.ReadOnlySpan{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, having the specified <paramref name="length"/>.
								/// </summary>
								/// <param name="length">The length of the sequence.</param>
								/// <returns>A <see cref="global::System.ReadOnlySpan{T}"/> that represents the specified range of values.</returns>
								/// <remarks>
								/// <para>
								/// There are no bounds checks performed regarding the <paramref name="length"/> parameter.
								/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
								/// </para>
								/// </remarks>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> AsSpan(int length)
									=> AsSpan(0, length);

							""");
					}
					else
					{
						// See the comment in the `Accessibility.Uninitialized` case for why we need to generate this as an extension method instead of a member method.

						pic.AddSource($"{Config.PointerNamespaceName}.{Config.PointerExtensionsTypeName}.{characteristics.ToFileNamePrefix()}.Access.g.cs", SourceText.From(
							text: $$"""
								#nullable enable
								
								namespace {{Config.PointerNamespaceName}};

								partial class {{Config.PointerExtensionsTypeName}}
								{
									extension<{{Config.GenerationTypeParameterName}}>({{typeName}} pointer)
										where {{Config.GenerationTypeParameterName}} : unmanaged
									{
										/// <summary>
										/// Gets a <see cref="global::System.ReadOnlySpan{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
										/// </summary>
										/// <param name="start">The start index of the sequence.</param>
										/// <param name="length">The length of the sequence.</param>
										/// <returns>A <see cref="global::System.ReadOnlySpan{T}"/> that represents the specified range of values.</returns>
										/// <remarks>
										/// <para>
										/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
										/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
										/// </para>
										/// </remarks>
										[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
										[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
										public global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> AsSpan(nint start, int length)
										{
											unsafe
											{
												return new(pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}} + start, length);
											}
										}

										/// <summary>
										/// Gets a <see cref="global::System.ReadOnlySpan{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, starting at the specified <paramref name="start"/> index and having the specified <paramref name="length"/>.
										/// </summary>
										/// <param name="start">The start index of the sequence.</param>
										/// <param name="length">The length of the sequence.</param>
										/// <returns>A <see cref="global::System.ReadOnlySpan{T}"/> that represents the specified range of values.</returns>
										/// <remarks>
										/// <para>
										/// There are no bounds checks performed regarding the <paramref name="start"/> and <paramref name="length"/> parameters.
										/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
										/// </para>
										/// </remarks>
										[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
										public global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> AsSpan(nuint start, int length)
										{
											unsafe
											{
												return new(pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}} + start, length);
											}
										}

										/// <summary>
										/// Gets a <see cref="global::System.ReadOnlySpan{T}"/> that represents a contiguous sequence of values of type <typeparamref name="T"/> the current pointer is pointing to, having the specified <paramref name="length"/>.
										/// </summary>
										/// <param name="length">The length of the sequence.</param>
										/// <returns>A <see cref="global::System.ReadOnlySpan{T}"/> that represents the specified range of values.</returns>
										/// <remarks>
										/// <para>
										/// There are no bounds checks performed regarding the <paramref name="length"/> parameter.
										/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
										/// </para>
										/// </remarks>
										[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
										public global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> AsSpan(int length)
											=> pointer.AsSpan(0, length);
									}
								}

								#nullable restore
								""",
							encoding: Encoding.UTF8
						));
					}
				}
				break;

			case Accessibility.Uninitialized:
				var objectRandomAccessCharacteristics = characteristics with { Sequencability = Sequencability.Object, Accessibility = Accessibility.Random };
				var objectRandomAccessCharacteristicsTypeName = $"global::{Config.PointerNamespaceName}.{objectRandomAccessCharacteristics.ToTypeName()}";
				var objectRandomAccessCharacteristicsCRef = objectRandomAccessCharacteristics.Typeability switch { Typeability.Typed => $"{objectRandomAccessCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => objectRandomAccessCharacteristicsTypeName };

				builder.Append($$"""

						/// <summary>
						/// Initializes the target value the current pointer is pointing to with the specified <paramref name="value"/>.
						/// </summary>
						/// <param name="value">The value to initialize at the target location.</param>
						/// <returns>A new <see cref="{{objectRandomAccessCharacteristicsCRef}}"/> pointing to the same target as the current pointer, representing the initialized target.</returns>
						[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
						public readonly {{objectRandomAccessCharacteristicsTypeName}} Initialize{{Config.GenerationTargetPropertyName}}(in {{Config.GenerationTypeParameterName}} value)
						{
							unsafe
							{
								*{{Config.GenerationRawPointerFieldName}} = value;
								return new({{Config.GenerationRawPointerFieldName}});
							}
						}

					""");

				if (characteristics.Sequencability is Sequencability.Sequence)
				{
					builder.Append($$"""
							
							/// <summary>
							/// Initializes the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to with the specified <paramref name="value"/>.
							/// </summary>
							/// <param name="index">The index of the target value in the sequence.</param>
							/// <param name="value">The value to initialize at the specified index.</param>
							/// <returns>A new <see cref="{{objectRandomAccessCharacteristicsCRef}}"/> pointing to the same target with the specified <paramref name="index"/> in the sequence, representing the single initialized target.</returns>
							[global::System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
							[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
							public readonly {{objectRandomAccessCharacteristicsTypeName}} Initialize{{Config.GenerationTargetPropertyName}}(nint index, in {{Config.GenerationTypeParameterName}} value)
							{
									unsafe
									{
										var raw = {{Config.GenerationRawPointerFieldName}} + index;
										*raw = value;
										return new(raw);
									}
								}

								/// <summary>
								/// Initializes the target value at the specified <paramref name="index"/> in the sequence the current pointer is pointing to with the specified <paramref name="value"/>.
								/// </summary>
								/// <param name="index">The index of the target value in the sequence.</param>
								/// <param name="value">The value to initialize at the specified index.</param>
								/// <returns>A new <see cref="{{objectRandomAccessCharacteristicsCRef}}"/> pointing to the same target with the specified <paramref name="index"/> in the sequence, representing the single initialized target.</returns>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly {{objectRandomAccessCharacteristicsTypeName}} Initialize{{Config.GenerationTargetPropertyName}}(nuint index, in {{Config.GenerationTypeParameterName}} value)
								{
									unsafe
									{
										var raw = {{Config.GenerationRawPointerFieldName}} + index;
										*raw = value;
										return new(raw);

									}
								}

						""");

					var sequenceRandomAccessCharacteristics = characteristics with { Accessibility = Accessibility.Random };
					var sequenceRandomAccessCharacteristicsTypeName = $"global::{Config.PointerNamespaceName}.{sequenceRandomAccessCharacteristics.ToTypeName()}";
					var sequenceRandomAccessCharacteristicsCRef = sequenceRandomAccessCharacteristics.Typeability switch { Typeability.Typed => $"{sequenceRandomAccessCharacteristics.ToTypeNameWithoutTypeParameter()}{{{Config.GenerationTypeParameterName}}}", _ => sequenceRandomAccessCharacteristicsTypeName };

					if (sequenceRandomAccessCharacteristics.Persistency is Persistency.Persistent)
					{
						builder.Append($$"""
							
								/// <summary>
								/// Initializes the sequence of target values the current pointer is pointing to with the specified <paramref name="values"/>.
								/// </summary>
								/// <param name="values">The values to initialize at the target locations.</param>
								/// <returns>A new <see cref="{{sequenceRandomAccessCharacteristicsCRef}}"/> pointing to the same target as the current pointer, representing the initialized sequence.</returns>
								/// <remarks>
								/// <para>
								/// There are no bounds checks performed regarding the length of the <paramref name="values"/> parameter.
								/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
								/// </para>
								/// <para>
								/// The sequence the current pointer is pointing to and the sequence referenced by the <paramref name="values"/> parameter can overlap without any issues.
								/// </para>
								/// </remarks>
								[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
								public readonly {{sequenceRandomAccessCharacteristicsTypeName}} Initialize{{Config.GenerationTargetPropertyName}}s(global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> values)
								{
									unsafe
									{
										fixed ({{Config.GenerationTypeParameterName}}* source = values)
										{
											global::System.Runtime.InteropServices.NativeMemory.Copy(source, {{Config.GenerationRawPointerFieldName}}, checked((nuint)values.Length * (nuint)global::System.Runtime.CompilerServices.Unsafe.SizeOf<{{Config.GenerationTypeParameterName}}>()));
										}

										return new({{Config.GenerationRawPointerFieldName}});
									}
								}

							""");
					}
					else
					{
						// Since `ReadOnlySpan<T>` doesn't accept `allows ref struct` type parameters as its generic type argument,
						// we can't add the `Initialize(ReadOnlySpan<T>)` method to the pointer type itself.
						// Instead, we make use of the newly introduced extension members syntax feature (C#14),
						// and add `Initialize(ReadOnlySpan<T>)` as an extension method while "de-constraining" the generic type parameter `T` to omit the `allows ref struct` constraint.
						// Of course, this means that `Initialize(ReadOnlySpan<T>)` will only be available for pointer types whose generic type parameter `T` is not an `ref struct` type,
						// but that's the best we can do given the current state of the BCL (ReadOnlySpan<T> not allowing for `allows ref struct` type parameters).

						pic.AddSource($"{Config.PointerNamespaceName}.{Config.PointerExtensionsTypeName}.{characteristics.ToFileNamePrefix()}.Access.g.cs", SourceText.From(
							text: $$"""
								#nullable enable
								
								namespace {{Config.PointerNamespaceName}};

								partial class {{Config.PointerExtensionsTypeName}}
								{
									extension<{{Config.GenerationTypeParameterName}}>({{typeName}} pointer)
										where {{Config.GenerationTypeParameterName}} : unmanaged
									{
										/// <summary>
										/// Initializes the sequence of target values the current pointer is pointing to with the specified <paramref name="values"/>.
										/// </summary>
										/// <param name="values">The values to initialize at the target locations.</param>
										/// <returns>A new <see cref="{{sequenceRandomAccessCharacteristicsCRef}}"/> pointing to the same target as the current pointer, representing the initialized sequence.</returns>
										/// <remarks>
										/// <para>
										/// There are no bounds checks performed regarding the length of the <paramref name="values"/> parameter.
										/// It is the caller's responsibility to ensure that the specified range is valid and does not exceed the bounds of the sequence the pointer is pointing to.
										/// </para>
										/// <para>
										/// The sequence the current pointer is pointing to and the sequence referenced by the <paramref name="values"/> parameter can overlap without any issues.
										/// </para>
										/// </remarks>
										[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
										public {{sequenceRandomAccessCharacteristicsTypeName}} Initialize{{Config.GenerationTargetPropertyName}}s(global::System.ReadOnlySpan<{{Config.GenerationTypeParameterName}}> values)
										{
											unsafe
											{
												fixed ({{Config.GenerationTypeParameterName}}* source = values)
												{
													global::System.Runtime.InteropServices.NativeMemory.Copy(source, pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}}, checked((nuint)values.Length * (nuint)global::System.Runtime.CompilerServices.Unsafe.SizeOf<{{Config.GenerationTypeParameterName}}>()));
												}
								
												return new(pointer.{{Config.PointerInterfaceTypeRawPointerPropertyName}});
											}
										}
									}
								}

								#nullable restore
								""",
							encoding: Encoding.UTF8
						));
					}
				}
				break;
		}

		builder.Append($$"""
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Access.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
