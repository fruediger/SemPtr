using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SemPtr.SourceGeneration;

[StructLayout(LayoutKind.Sequential)]
[method: SetsRequiredMembers]
internal readonly struct FunctionPointerCharacteristics(Nullability nullability, Persistency persistency, Typeability typeability) : IPointerCharacteristics<FunctionPointerCharacteristics>
{
	private readonly Nullability mNullability = nullability;
	private readonly Persistency mPersistency = persistency;
	private readonly Typeability mTypeability = typeability;

	public required readonly Nullability Nullability
	{
		get => mNullability;
		init => mNullability = value;
	}

	public required readonly Persistency Persistency
	{
		get => mPersistency;
		init => mPersistency = value;
	}

	public required readonly Typeability Typeability
	{
		get => mTypeability;
		init => mTypeability = value;
	}

	public void Deconstruct(out Nullability nullability, out Persistency persistency, out Typeability typeability)
	{
		nullability = mNullability;
		persistency = mPersistency;
		typeability = mTypeability;
	}

	public static IEnumerable<FunctionPointerCharacteristics> Enumerate(Nullability? nullability = default, Persistency? persistency = default, Typeability? typeability = default)
	{
		foreach (var n in nullability switch { Nullability value => [value], _ => Nullability.Values })
		{
			foreach (var p in persistency switch { Persistency value => [value], _ => Persistency.Values })
			{
				foreach (var t in typeability switch { Typeability value => [value], _ => Typeability.Values })
				{
					yield return new(n, p, t);
				}
			}
		}
	}

	public readonly override bool Equals(object obj) => obj is FunctionPointerCharacteristics other && Equals(other);

	public readonly bool Equals(in FunctionPointerCharacteristics other)
		=> mNullability == other.mNullability
		&& mPersistency == other.mPersistency
		&& mTypeability == other.mTypeability;

	bool IEquatable<FunctionPointerCharacteristics>.Equals(FunctionPointerCharacteristics other) => Equals(other);

	public readonly override int GetHashCode() => HashCode.Combine(
		mNullability,
		mPersistency,
		mTypeability
	);

	public readonly override string ToString()
		=> $"{{ {nameof(Nullability)}: {mNullability}, {
			nameof(Persistency)}: {mPersistency}, {
			nameof(Typeability)}: {mTypeability} }}";

	public readonly string ToFileNamePrefix(string typeParameterName = Config.GenerationDelegateTypeParameterName)
		=> $"{ToTypeNameWithoutTypeParameter()}{Typeability switch
		{
			Typeability.Typed => $"_{typeParameterName}",
			_ => string.Empty
		}}";

	string IPointerCharacteristics<FunctionPointerCharacteristics>.ToFileNamePrefix() => ToFileNamePrefix();

	public readonly string ToMetadataName()
		=> $"{ToTypeNameWithoutTypeParameter()}{Typeability switch
		{
			Typeability.Typed => $"`1",
			_ => string.Empty
		}}";

	public readonly string ToTypeName(string typeParameterName = Config.GenerationDelegateTypeParameterName)
		=> $"{ToTypeNameWithoutTypeParameter()}{Typeability switch
		{
			Typeability.Typed => $"<{typeParameterName}>",
			_ => string.Empty
		}}";

	string IPointerCharacteristics<FunctionPointerCharacteristics>.ToTypeName() => ToTypeName();

	public readonly string ToTypeNameWithoutTypeParameter()
		=> $"{Nullability switch
		{
			Nullability.Nullable => "Nullable",
			_ => string.Empty
		}}{Persistency switch
		{
			Persistency.Persistent => "Persistent",
			_ => string.Empty
		}}FunctionPointer";

	public static bool operator ==(in FunctionPointerCharacteristics left, in FunctionPointerCharacteristics right) => left.Equals(right);

	public static bool operator !=(in FunctionPointerCharacteristics left, in FunctionPointerCharacteristics right) => !(left == right);

	public static implicit operator FunctionPointerCharacteristics((Nullability nullability, Persistency persistency, Typeability typeability) tuple)
		=> new(tuple.nullability, tuple.persistency, tuple.typeability);

	public static implicit operator (Nullability nullability, Persistency persistency, Typeability typeability)(in FunctionPointerCharacteristics characteristics)
		=> (characteristics.mNullability, characteristics.mPersistency, characteristics.mTypeability);
}