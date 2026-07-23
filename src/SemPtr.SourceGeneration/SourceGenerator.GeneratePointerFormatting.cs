using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerFormatting(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.Formatting.g.cs", SourceText.From(
			text: $$"""
				#nullable enable

				using global::{{Config.InternalNamespaceName}};

				namespace {{Config.PointerNamespaceName}};

				partial struct {{characteristics.ToTypeName(Config.GenerationTypeParameterName)}} :
					global::System.IFormattable,
					global::System.ISpanFormattable,
					global::System.IUtf8SpanFormattable
				{
					/// <summary>Formats the value of the current instance using the default format.</summary>
					/// <returns>The value of the current instance in the default format.</returns>
					public readonly override string ToString()
						=> ToString(formatProvider: null);

					/// <inheritdoc cref="global::System.IFormattable.ToString(string?, global::System.IFormatProvider?)"/>
					public readonly string ToString(global::System.IFormatProvider? formatProvider)
						=> $"0x{ToString(format: {{Config.PointerFormatTypeName}}.{{Config.PointerFormatTypeDefaultFormatStringPropertyName}}, formatProvider)}";

					/// <inheritdoc cref="global::System.IFormattable.ToString(string?, global::System.IFormatProvider?)"/>
					public readonly string ToString(string? format)
						=> ToString(format, formatProvider: null);

					/// <inheritdoc/>
					public readonly string ToString(string? format, global::System.IFormatProvider? formatProvider)
					{
						unsafe
						{
							return unchecked((nuint){{Config.GenerationRawPointerFieldName}}).ToString(format, formatProvider);
						}
					}

					/// <inheritdoc cref="global::System.ISpanFormattable.TryFormat(global::System.Span{char}, out int, global::System.ReadOnlySpan{char}, global::System.IFormatProvider?)"/>
					public readonly bool TryFormat(global::System.Span<char> destination, out int charsWritten, global::System.IFormatProvider? provider = default)
					{
						if (destination.Length is < 2)
						{
							charsWritten = 0;
							return false;
						}

						destination[0] = '0';
						destination[1] = 'x';
						charsWritten = 2;

						if (!TryFormat(destination[2..], out var additionalCharsWritten, format: {{Config.PointerFormatTypeName}}.{{Config.PointerFormatTypeDefaultFormatStringPropertyName}}, provider))
						{
							charsWritten = 0;
							return false;
						}

						charsWritten += additionalCharsWritten;
						return true;
					}

					/// <inheritdoc/>
					public readonly bool TryFormat(global::System.Span<char> destination, out int charsWritten, global::System.ReadOnlySpan<char> format, global::System.IFormatProvider? provider = default)
					{
						unsafe
						{
							return unchecked((nuint){{Config.GenerationRawPointerFieldName}}).TryFormat(destination, out charsWritten, format, provider);
						}
					}

					/// <inheritdoc cref="global::System.IUtf8SpanFormattable.TryFormat(global::System.Span{byte}, out int, global::System.ReadOnlySpan{char}, global::System.IFormatProvider?)"/>
					public readonly bool TryFormat(global::System.Span<byte> destination, out int bytesWritten, global::System.IFormatProvider? provider = default)
					{
						if (destination.Length is < 2)
						{
							bytesWritten = 0;
							return false;
						}

						destination[0] = (byte)'0';
						destination[1] = (byte)'x';
						bytesWritten = 2;

						if (!TryFormat(destination[2..], out var additionalBytesWritten, format: {{Config.PointerFormatTypeName}}.{{Config.PointerFormatTypeDefaultFormatStringPropertyName}}, provider))
						{
							bytesWritten = 0;
							return false;
						}

						bytesWritten += additionalBytesWritten;
						return true;
					}

					/// <inheritdoc/>
					public readonly bool TryFormat(global::System.Span<byte> destination, out int bytesWritten, global::System.ReadOnlySpan<char> format, global::System.IFormatProvider? provider = default)
					{
						unsafe
						{
							return unchecked((nuint){{Config.GenerationRawPointerFieldName}}).TryFormat(destination, out bytesWritten, format, provider);
						}
					}
				}

				#nullable restore
				""",
			encoding: Encoding.UTF8
		));
	}
}
