using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SemPtr.SourceGeneration;

[StructLayout(LayoutKind.Sequential)]
[method: SetsRequiredMembers]
internal readonly struct PointerCharacteristics(Nullability nullability, Persistency persistency, Sequencability sequencability, Accessibility accessibility, Typeability typeability) : IPointerCharacteristics<PointerCharacteristics>
{
	private readonly Nullability mNullability = nullability;
	private readonly Persistency mPersistency = persistency;
	private readonly Sequencability mSequencability = sequencability;
	private readonly Accessibility mAccessibility = accessibility;
	private readonly Typeability mTypeability = typeability;

	public required readonly Nullability Nullability
	{
		get => mNullability;
		init => mNullability = value;
	}

	public readonly Persistency Persistency
	{
		get => mPersistency;
		init => mPersistency = value;
	}

	public readonly Sequencability Sequencability
	{
		get => mSequencability;
		init => mSequencability = value;
	}

	public readonly Accessibility Accessibility
	{
		get => mAccessibility;
		init => mAccessibility = value;
	}

	public readonly Typeability Typeability
	{
		get => mTypeability;
		init => mTypeability = value;
	}

	public readonly void Deconstruct(out Nullability nullability, out Persistency persistency, out Sequencability sequencability, out Accessibility accessibility, out Typeability typeability)
	{
		nullability = mNullability;
		persistency = mPersistency;
		sequencability = mSequencability;
		accessibility = mAccessibility;
		typeability = mTypeability;
	}

	public static IEnumerable<PointerCharacteristics> Enumerate(Nullability? nullability = default, Persistency? persistency = default, Sequencability? sequencability = default, Accessibility? accessibility = default, Typeability? typeability = default)
	{
		foreach (var n in nullability switch { Nullability value => [value], _ => Nullability.Values })
		{
			foreach (var p in persistency switch { Persistency value => [value], _ => Persistency.Values })
			{
				foreach (var s in sequencability switch { Sequencability value => [value], _ => Sequencability.Values })
				{
					foreach (var a in accessibility switch { Accessibility value => [value], _ => Accessibility.Values })
					{
						foreach (var t in typeability switch { Typeability value => [value], _ => Typeability.Values })
						{
							yield return new(n, p, s, a, t);
						}
					}
				}
			}
		}
	}

	public readonly override bool Equals(object obj) => obj is PointerCharacteristics other && Equals(other);

	public readonly bool Equals(in PointerCharacteristics other)
		=> mNullability == other.mNullability
		&& mPersistency == other.mPersistency
		&& mSequencability == other.mSequencability
		&& mAccessibility == other.mAccessibility
		&& mTypeability == other.mTypeability;

	readonly bool IEquatable<PointerCharacteristics>.Equals(PointerCharacteristics other) => Equals(other);

	public readonly override int GetHashCode() => HashCode.Combine(
		mNullability,
		mPersistency,
		mSequencability,
		mAccessibility,
		mTypeability
	);

	public readonly override string ToString()
		=> $"{{ {nameof(Nullability)}: {mNullability}, {
			nameof(Persistency)}: {mPersistency}, {
			nameof(Sequencability)}: {mSequencability}, {
			nameof(Accessibility)}: {mAccessibility}, {
			nameof(Typeability)}: {mTypeability} }}";

	public readonly string ToFileNamePrefix(string typeParameterName = Config.GenerationTypeParameterName)
		=> $"{ToTypeNameWithoutTypeParameter()}{mTypeability switch
		{
			Typeability.Typed => $"_{typeParameterName}",
			_ => string.Empty
		}}";

	public readonly string ToMetadataName()
		=> $"{ToTypeNameWithoutTypeParameter()}{mTypeability switch
		{
			Typeability.Typed => $"`1",
			_ => string.Empty
		}}";

	string IPointerCharacteristics<PointerCharacteristics>.ToFileNamePrefix() => ToFileNamePrefix();

	public readonly string ToTypeName(string typeParameterName = Config.GenerationTypeParameterName)
		=> $"{ToTypeNameWithoutTypeParameter()}{mTypeability switch
		{
			Typeability.Typed => $"<{typeParameterName}>",
			_ => string.Empty
		}}";

	string IPointerCharacteristics<PointerCharacteristics>.ToTypeName() => ToTypeName();

	public readonly string ToTypeNameWithoutTypeParameter()
		=> $"{mNullability switch
		{
			Nullability.Nullable => "Nullable",
			_ => string.Empty
		}}{mPersistency switch
		{
			Persistency.Persistent => "Persistent",
			_ => string.Empty
		}}{mSequencability switch
		{
			Sequencability.Sequence => "Sequence",
			_ => string.Empty
		}}Pointer{Accessibility switch
		{
			Accessibility.ReadOnly => "ReadOnly",
			Accessibility.Uninitialized => "Uninitialized",
			_ => string.Empty
		}}";

	public static bool operator ==(in PointerCharacteristics left, in PointerCharacteristics right) => left.Equals(right);
	public static bool operator !=(in PointerCharacteristics left, in PointerCharacteristics right) => !(left == right);

	public static implicit operator PointerCharacteristics((Nullability nullability, Persistency persistency, Sequencability sequencability, Accessibility accessibility, Typeability typeability) tuple)
		=> new(tuple.nullability, tuple.persistency, tuple.sequencability, tuple.accessibility, tuple.typeability);

	public static implicit operator (Nullability nullability, Persistency persistency, Sequencability sequencability, Accessibility accessibility, Typeability typeability)(in PointerCharacteristics characteristics)
		=> (characteristics.mNullability, characteristics.mPersistency, characteristics.mSequencability, characteristics.mAccessibility, characteristics.mTypeability);
}
