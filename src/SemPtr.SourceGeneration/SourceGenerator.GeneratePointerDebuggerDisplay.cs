using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SemPtr.SourceGeneration;

partial class SourceGenerator
{
	private static void GeneratePointerDebuggerDisplay(IncrementalGeneratorPostInitializationContext pic, in PointerCharacteristics characteristics, StringBuilder builder)
	{
		builder.Clear();

		var typeName = characteristics.ToTypeName();
		var rawPointerType = characteristics switch
		{
			{ Typeability: Typeability.Typed } => $"{Config.GenerationTypeParameterName}*",
			_ => "void*"
		};

		builder.Append($$$$"""
			#nullable enable

			using global::{{{{Config.InternalNamespaceName}}}};

			namespace {{{{Config.PointerNamespaceName}}}};

			[global::System.Diagnostics.DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
			[global::System.Diagnostics.DebuggerTypeProxy(typeof({{{{characteristics.ToTypeNameWithoutTypeParameter()}}}}{{{{characteristics.Typeability switch { Typeability.Typed => "<>", _ => string.Empty }}}}}.DebuggerTypeProxy))]
			partial struct {{{{typeName}}}}
			{
				private sealed unsafe class DebuggerTypeProxy
				{
			""");

		if (characteristics.Typeability is Typeability.Typed)
		{
			if (characteristics.Sequencability is Sequencability.Sequence)
			{
				builder.Append($$$$"""
				
							[global::System.Diagnostics.DebuggerDisplay($"{{{nameof(Value)},nq}}", Name = $"{{{nameof(Name)},nq}}", Type = $"{{{nameof(Type)},nq}}")]
							[global::System.Diagnostics.DebuggerTypeProxy(typeof({{{{characteristics.ToTypeNameWithoutTypeParameter()}}}}{{{{characteristics.Typeability switch { Typeability.Typed => "<>", _ => string.Empty }}}}}.DebuggerTypeProxy))]
							public readonly struct OffsetPointerProxy
							{
								[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
								[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
								private readonly nuint mOffset;

								[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
								[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
								private readonly {{{{rawPointerType}}}} mRaw;

								public OffsetPointerProxy(nuint offset, {{{{rawPointerType}}}} raw)
								{
									mOffset = offset;
									mRaw = raw;
								}

								[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
								[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
								public readonly nuint Offset => mOffset;

								[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
								[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
								public readonly string Name => $"+{mOffset}";

								[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
								[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
								public readonly {{{{typeName}}}} Value => new(mRaw);

								[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
								[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
								public readonly string Type => new global::System.Text.StringBuilder().{{{{Config.TypeNameFormattingExtensionsMethodName}}}}(typeof({{{{typeName}}}})).ToString();
							}

					""");
			}

			builder.Append($$$$"""

						[global::System.Diagnostics.DebuggerDisplay($"{{{nameof(Value)},nq}}", Name = $"{{{nameof(Name)},nq}}", Type = $"{{{nameof(Type)},nq}}")]
						public readonly struct {{{{Config.GenerationTargetPropertyName}}}}Proxy
						{
				""");

			if (characteristics.Persistency is Persistency.Persistent)
			{
				builder.Append($$"""		
					
								[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
								[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
								private readonly {{rawPointerType}} mRaw;

								public {{Config.GenerationTargetPropertyName}}Proxy({{rawPointerType}} raw)
								{
									mRaw = raw;
								}

					""");
			}

			builder.Append($$"""	
				
							[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
							[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
							public readonly string Name => nameof({{Config.GenerationTargetPropertyName}});
				
							[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
							[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
							public readonly string Value
				""");

			if (characteristics.Persistency is Persistency.Persistent)
			{
				builder.Append("""
					
								{
									get
									{
										try
										{
											return $"{*mRaw}";
										}
										catch
										{
											return "<could not evaluate pointer target>";
										}
									}
								}

					""");
			}
			else
			{
				builder.Append("""
					 => Type;

					""");
			}

			builder.Append($$"""				
				
							[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
							[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
							public readonly string Type => new global::System.Text.StringBuilder().{{Config.TypeNameFormattingExtensionsMethodName}}(typeof({{Config.GenerationTypeParameterName}})).ToString();
						}

				""");
		}

		if (characteristics.Typeability is Typeability.Typed)
		{
			builder.Append($$"""

						[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
						[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
						private readonly {{rawPointerType}} mRaw;

				""");

			if (characteristics.Sequencability is Sequencability.Sequence)
			{
				builder.Append($$"""	
				
							[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
							[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
							private readonly nuint mOffset;

							public DebuggerTypeProxy({{typeName}} ptr)
							{
								mRaw = ptr.{{Config.GenerationRawPointerFieldName}};
								mOffset = 0;
							}

							public DebuggerTypeProxy(OffsetPointerProxy offsetPtr)
							{
								mRaw = offsetPtr.Value.{{Config.GenerationRawPointerFieldName}};
								mOffset = offsetPtr.Offset;
							}

							[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.RootHidden)]
							public OffsetPointerProxy[] Next => [new(mOffset + 1, mRaw + 1)];

					""");
			}
			else
			{
				builder.Append($$"""

							public DebuggerTypeProxy({{typeName}} ptr)
							{
								mRaw = ptr.{{Config.GenerationRawPointerFieldName}};
							}

					""");
			}

			builder.Append($$"""	
				
						public {{Config.GenerationTargetPropertyName}}Proxy Target => new({{characteristics.Persistency switch { Persistency.Persistent => "mRaw", _ => string.Empty }}});

				""");
		}
		else
		{
			builder.Append($$"""

						public DebuggerTypeProxy({{typeName}} ptr)
						{ }

				""");
		}

		builder.Append($$"""			
				}
				
				[global::System.Diagnostics.DebuggerBrowsable(global::System.Diagnostics.DebuggerBrowsableState.Never)]
				[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
				private readonly string DebuggerDisplay => ToString(formatProvider: global::System.Globalization.CultureInfo.InvariantCulture);
			}

			#nullable restore
			""");

		pic.AddSource($"{Config.PointerNamespaceName}.{characteristics.ToFileNamePrefix()}.DebuggerDisplay.g.cs", SourceText.From(
			text: builder.ToString(),
			encoding: Encoding.UTF8
		));
	}
}
