using System;

internal interface IPointerCharacteristics<TSelf> : IEquatable<TSelf>
	where TSelf : struct, IPointerCharacteristics<TSelf>
{
	string ToFileNamePrefix();
	string ToTypeName();
	string ToTypeNameWithoutTypeParameter();
}