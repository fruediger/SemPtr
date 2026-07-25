using System;

internal interface IPointerCharacteristics<TSelf> : IEquatable<TSelf>
	where TSelf : struct, IPointerCharacteristics<TSelf>
{
	string ToFileNamePrefix();
	string ToMetadataName();
	string ToTypeName();
	string ToTypeNameWithoutTypeParameter();
}