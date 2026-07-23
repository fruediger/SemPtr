namespace SemPtr.SourceGeneration;

internal static class Config
{
	public const string InternalNamespaceName = "SemPtr.Internal";
	public const string PointerNamespaceName = "SemPtr";
	public const string PointerExtensionsTypeName = "PointerExtensions";
	public const string PointerInterfaceTypeName = "IPointer";
	public const string PointerTypedInterfaceTypeName = "ITypedPointer";
	public const string PointerInterfaceTypeRawPointerPropertyName = "Raw";
	public const string PointerInterfaceTypeAsIntPtrPropertyName = "AsIntPtr";
	public const string PointerInterfaceTypeAsUIntPtrPropertyName = "AsUIntPtr";
	public const string PointerInterfaceTypeFromRawMethodName = "FromRaw";
	public const string PointerInterfaceTypeFromIntPtrMethodName = "FromIntPtr";
	public const string PointerInterfaceTypeFromUIntPtrMethodName = "FromUIntPtr";
	public const string PointerFormatTypeName = "PointerFormat";
	public const string PointerFormatTypeDefaultFormatStringPropertyName = "DefaultFormatString";
	public const string GenerationRawPointerFieldName = "mRaw";
	public const string GenerationTargetPropertyName = "Target";
	public const string GenerationTypeParameterName = "T";
	public const string TypeNameFormattingExtensionsMethodName = "AppendQualifiedCSharpTypeName";
}
