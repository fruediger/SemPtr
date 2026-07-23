namespace SemPtr.SourceGeneration;

internal static class ConversionExtensions
{
	extension(Conversion)
	{
		// Data pointer <-> data pointer conversions
		public static Conversion Get(in PointerCharacteristics toCharacteristics, in PointerCharacteristics fromCharacteristics)
		{
			// These are the rules for pointer to pointer conversion:
			//
			//   Let `to` be the target pointer type and `from` be the source pointer type.
			//   Then the following three predicates on `(to, from)` define the conversion rules:
			//
			//     hasConversion(to, from) := nonnull(to) ⇒ nonnull(from)
			//                             ∧ persistent(to) ⇒ persistent(from)
			//                             ∧ sequence(to) ⇒ sequence(from)
			//                             ∧ (access(to) = access(from) ∨ access(from) = random)
			//
			//     hasExplicitConversion(to, from) := hasConversion(to, from)
			//                                     ∧ ¬(typed(to) ⇒ typed(from))
			//
			//     hasImplicitConversion(to, from) := hasConversion(to, from)
			//                                     ∧ typed(to) ⇒ typed(from)
			//
			//   where `hasConversion` determines whether a conversion from `from`to `to` exists (regardless of whether it is implicit or explicit),
			//   `hasImplicitConversion` determines whether an *implicit* conversion from `from` to `to` exists,
			//   and `hasExplicitConversion` determines whether an *explicit* conversion from `from` to `to` exists.
			//
			//   Furthermore, `nonnull(x)` is a predicate determining whether the given pointer type `x` is non-nullable,
			//   `persistent(x)` is a predicate determining whether the given pointer type `x` is persistent,
			//   `sequence(x)` is a predicate determining whether the given pointer type `x` is a sequence pointer type,
			//   `typed(x)` is a predicate determining whether the given pointer type `x` is typed,
			//   and `access(x)` maps the given pointer type `x` to its accessibility kind, where `access(x) ∈ { random, read-only, uninitialized }`.
			//
			// These rules are the condensed way of describing the conversion rules between pointer types,
			// and should be enough to correctly state each possible conversion between any two pointer types.
			//
			// Note that, if a conversion exists, the only thing that differentiates an implicit conversion from an explicit one
			// is whether `typed(to) ⇒ typed(from)` yields true or false.
			// We will make use of this fact in the implementation of this method, rather than having two separate methods determining whether an implicit or explicit conversion exists.

			if ((toCharacteristics.Nullability is Nullability.NonNull).Implies(fromCharacteristics.Nullability is Nullability.NonNull)
				&& (toCharacteristics.Persistency is Persistency.Persistent).Implies(fromCharacteristics.Persistency is Persistency.Persistent)
				&& (toCharacteristics.Sequencability is Sequencability.Sequence).Implies(fromCharacteristics.Sequencability is Sequencability.Sequence)
				&& (toCharacteristics.Accessibility == fromCharacteristics.Accessibility || fromCharacteristics.Accessibility is Accessibility.Random))
			{
				return (toCharacteristics.Typeability is Typeability.Typed).Implies(fromCharacteristics.Typeability is Typeability.Typed)
					? Conversion.Implicit
					: Conversion.Explicit;
			}

			return Conversion.None;
		}

		// Function pointer <-> function pointer conversions
		public static Conversion Get(in FunctionPointerCharacteristics toCharacteristics, in FunctionPointerCharacteristics fromCharacteristics)
		{
			// The rules for function pointer to function pointer conversion are essentially the same as for data pointer to data pointer conversion,
			// except that these rules do not consider sequencability nor accessibility, as those characteristics are not applicable to function pointers.
			// This should still form a sound ruleset for function pointer conversions.

			if (   (toCharacteristics.Nullability is Nullability.NonNull).Implies(fromCharacteristics.Nullability is Nullability.NonNull)
				&& (toCharacteristics.Persistency is Persistency.Persistent).Implies(fromCharacteristics.Persistency is Persistency.Persistent))
			{
				return (toCharacteristics.Typeability is Typeability.Typed).Implies(fromCharacteristics.Typeability is Typeability.Typed)
					? Conversion.Implicit
					: Conversion.Explicit;
			}

			return Conversion.None;
		}

		// Data pointer -> function pointer conversions
		public static Conversion Get(in FunctionPointerCharacteristics toCharacteristics, in PointerCharacteristics fromCharacteristics)
		{
			// The rules for data pointer to function pointer conversion are again very similar to the rules for data pointer to data pointer conversion.
			// In this case we can ignore the sequencability of the source pointer type, as sequence pointers as well as object pointers can convert to function pointers.
			// Also we must make sure that the source pointer type is readable (i.e. not uninitialized), as function pointers treat their target as read-only.
			// Cross-conversion between data pointers and function pointers are always explicit!

			if (   (toCharacteristics.Nullability is Nullability.NonNull).Implies(fromCharacteristics.Nullability is Nullability.NonNull)
				&& (toCharacteristics.Persistency is Persistency.Persistent).Implies(fromCharacteristics.Persistency is Persistency.Persistent)
				&& fromCharacteristics.Accessibility is not Accessibility.Uninitialized)
			{
				return Conversion.Explicit; // always explicit: reinterpreting data as code
			}

			return Conversion.None;
		}

		public static Conversion Get(in PointerCharacteristics toCharacteristics, in FunctionPointerCharacteristics fromCharacteristics)
		{
			// The rules for function pointer to data pointer conversion are again very similar to the rules for data pointer to data pointer conversion.
			// In this case we must make sure that the target pointer type is a read-only object pointer type,
			// as function pointers always treat their target as a single read-only object (the executable code of the function),
			// and we must make sure that those properties are preserved when converting to a data pointer type.
			// Cross-conversion between data pointers and function pointers are always explicit!

			if (   (toCharacteristics.Nullability is Nullability.NonNull).Implies(fromCharacteristics.Nullability is Nullability.NonNull)
				&& (toCharacteristics.Persistency is Persistency.Persistent).Implies(fromCharacteristics.Persistency is Persistency.Persistent)
				&& toCharacteristics.Sequencability is Sequencability.Object
				&& toCharacteristics.Accessibility is Accessibility.ReadOnly)
			{
				return Conversion.Explicit; // always explicit: reinterpreting code as data
			}

			return Conversion.None;
		}
	}

	extension(in PointerCharacteristics characteristics)
	{
		public Conversion GetConversionFrom(in PointerCharacteristics other)
			=> Conversion.Get(in characteristics, in other);

		public Conversion GetConversionFrom(in FunctionPointerCharacteristics other)
			=> Conversion.Get(in characteristics, in other);

		public Conversion GetConversionTo(in PointerCharacteristics other)
			=> Conversion.Get(in other, in characteristics);

		public Conversion GetConversionTo(in FunctionPointerCharacteristics other)
			=> Conversion.Get(in other, in characteristics);
	}

	extension (in FunctionPointerCharacteristics characteristics)
	{
		public Conversion GetConversionFrom(in FunctionPointerCharacteristics other)
			=> Conversion.Get(in characteristics, in other);

		public Conversion GetConversionFrom(in PointerCharacteristics other)
			=> Conversion.Get(in characteristics, in other);

		public Conversion GetConversionTo(in FunctionPointerCharacteristics other)
			=> Conversion.Get(in other, in characteristics);

		public Conversion GetConversionTo(in PointerCharacteristics other)
			=> Conversion.Get(in other, in characteristics);
	}
}