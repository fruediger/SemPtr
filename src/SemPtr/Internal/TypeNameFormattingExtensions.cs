using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SemPtr.Internal;

internal static class TypeNameFormattingExtensions
{
	extension(StringBuilder? builder)
	{
		[return: NotNullIfNotNull(nameof(builder))]
		public StringBuilder? AppendQualifiedCSharpTypeName(Type? type)
		{
			if (builder is null || type is null) return builder;
			return builder.AppendQualifiedCSharpTypeNameNonNull(type);
		}
	}

	extension(StringBuilder builder)
	{
		// Formats a non-null Type into a C#-style name, preparing generic arguments and delegating to the recursive formatter
		private StringBuilder AppendQualifiedCSharpTypeNameNonNull(Type type)
		{
			var genArgsIndex = 0;
			return builder.AppendQualifiedCSharpTypeNameRecursiveNonNull(
				type,
				genArgs: type.GetGenericArguments(),
				ref genArgsIndex
			);
		}

		// Null-tolerant wrapper for the recursive formatter
		private StringBuilder AppendQualifiedCSharpTypeNameRecursiveNullable(Type? type, Type[] genArgs, ref int genArgsIndex)
		{
			if (type is null) return builder;
			return builder.AppendQualifiedCSharpTypeNameRecursiveNonNull(type, genArgs, ref genArgsIndex);
		}

		// Core recursive formatter for a non-null Type
		private StringBuilder AppendQualifiedCSharpTypeNameRecursiveNonNull(Type type, Type[] genArgs, ref int genArgsIndex)
		{
			switch (type)
			{
				// Keyword aliases for built-in CLR types
				case { Namespace: "System", Name: "Void" }: return builder.Append("void"); // also covers void* and void&
				case { Namespace: "System", Name: "Boolean" }: return builder.Append("bool");
				case { Namespace: "System", Name: "Byte" }: return builder.Append("byte");
				case { Namespace: "System", Name: "SByte" }: return builder.Append("sbyte");
				case { Namespace: "System", Name: "UInt16" }: return builder.Append("ushort");
				case { Namespace: "System", Name: "Int16" }: return builder.Append("short");
				case { Namespace: "System", Name: "UInt32" }: return builder.Append("uint");
				case { Namespace: "System", Name: "Int32" }: return builder.Append("int");
				case { Namespace: "System", Name: "UInt64" }: return builder.Append("ulong");
				case { Namespace: "System", Name: "Int64" }: return builder.Append("long");
				case { Namespace: "System", Name: "UIntPtr" }: return builder.Append("nuint");
				case { Namespace: "System", Name: "IntPtr" }: return builder.Append("nint");
				case { Namespace: "System", Name: "Single" }: return builder.Append("float");
				case { Namespace: "System", Name: "Double" }: return builder.Append("double");
				case { Namespace: "System", Name: "Decimal" }: return builder.Append("decimal");
				case { Namespace: "System", Name: "Char" }: return builder.Append("char");
				case { Namespace: "System", Name: "String" }: return builder.Append("string");
				case { Namespace: "System", Name: "Object" }: return builder.Append("object");

				// Generic type parameter (e.g., T in List<T>)
				case { IsGenericParameter: true }: return builder.Append(type.Name);

				// By-ref type
				case { IsByRef: true }:
					return builder.AppendQualifiedCSharpTypeNameRecursiveNullable(type.GetElementType(), genArgs, ref genArgsIndex)
								  .Append('&');

				// Pointer type
				case { IsPointer: true }:
					return builder.AppendQualifiedCSharpTypeNameRecursiveNullable(type.GetElementType(), genArgs, ref genArgsIndex)
								  .Append('*');

				// Array type (handles jagged + multi-dimensional correctly)
				case { IsArray: true }:
					{
						// The CLR's metadata order for mixed arrays (e.g., int?[,][]) differs from C# syntax.
						// We append all rank suffixes in CLR order, copy them out, truncate, print the innermost
						// element type, then append the saved suffixes. This yields the correct C# order:
						// inner dimensions before outer dimensions.
						var suffixStart = builder.Length;

						// Walk through all array layers, appending each rank suffix in CLR order.
						do
						{
							builder.Append('[')
								   .Append(',', type.GetArrayRank() - 1)
								   .Append(']');

							var elementType = type.GetElementType();
							if (elementType is null)
							{
								// If GetElementType() returns null, there is no further element type to format.
								// The suffixes appended so far are still valid, so we can return the builder as-is.
								return builder;
							}

							type = elementType;
						}
						while (type.IsArray);

						var suffixLength = builder.Length - suffixStart;

						Span<char> suffix = stackalloc char[suffixLength];
						builder.CopyTo(suffixStart, suffix, suffixLength);

						builder.Length = suffixStart;

						// Appending the saved suffixes after printing the innermost element type, produces the correct C# order: inner dimensions before outer dimensions.
						return builder.AppendQualifiedCSharpTypeNameRecursiveNonNull(type, genArgs, ref genArgsIndex)
									  .Append(suffix);
					}

				// Nullable<T> → T?
				case { IsGenericType: true } when type.GetGenericTypeDefinition() is { Namespace: "System", Name: "Nullable`1" }:
					// It's safe to index genArgs[0] here because:
					// - If this branch is hit, 'type' is exactly Nullable<T> (not nested)
					// - Nullable<T> always has exactly one generic argument
					return builder.AppendQualifiedCSharpTypeNameRecursiveNonNull(genArgs[0], genArgs, ref genArgsIndex)
								  .Append('?');

				// Nested type
				case { DeclaringType: { } declaringType }:
					return builder.AppendQualifiedCSharpTypeNameRecursiveNonNull(declaringType, genArgs, ref genArgsIndex)
								  .Append('.')
								  .AppendShortName(type, genArgs, ref genArgsIndex);


				// Namespace-qualified type
				case { Namespace: { Length: > 0 } @namespace }:
					return builder
						.Append(@namespace)
						.Append('.')
						.AppendShortName(type, genArgs, ref genArgsIndex);

				// Fallback: just the short name
				default: return builder.AppendShortName(type, genArgs, ref genArgsIndex);
			}

		}

		// Appends the short name of a type and its own generic arguments (consuming from genArgs)
		private StringBuilder AppendShortName(Type type, Type[] genArgs, ref int genArgsIndex)
		{
			var name = type.Name.AsSpan();
			var tickIndex = name.IndexOf('`');

			if (tickIndex is < 0) return builder.Append(name);

			_ = int.TryParse(name[(tickIndex + 1)..], out var arity);
			name = name[..tickIndex];
			builder.Append(name);

			if (arity is 0) return builder;

			builder.Append('<');

			var endIndex = genArgsIndex + arity;

			// First argument (no leading comma)
			if (!genArgs[genArgsIndex].IsGenericParameter)
				builder.AppendQualifiedCSharpTypeNameNonNull(genArgs[genArgsIndex]);

			// Remaining arguments
			for (genArgsIndex++; genArgsIndex < endIndex; genArgsIndex++)
			{
				builder.Append(',');
				if (!genArgs[genArgsIndex].IsGenericParameter)
				{
					builder.Append(' ')
						   .AppendQualifiedCSharpTypeNameNonNull(genArgs[genArgsIndex]);
				}
			}

			builder.Append('>');
			return builder;
		}
	}
}
