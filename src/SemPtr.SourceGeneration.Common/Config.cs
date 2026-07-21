namespace SemPtr.SourceGeneration;

internal static class Config
{
	public const string InternalNamespaceName = "SemPtr.Internal";
	public const string PointerNamespaceName = "SemPtr";
	public const string PointerExtensionsTypeName = "PointerExtensions";
	public const string PointerInterfaceTypeName = "IPointer";
	public const string PointerInterfaceTypeRawPointerPropertyName = "Raw";
	public const string PointerInterfaceTypeDefaultFormatStringPropertyName = "DefaultFormatString";
	public const string GenerationRawPointerFieldName = "mRaw";
	public const string GenerationTargetPropertyName = "Target";
	public const string GenerationTypeParameterName = "T";
	public const string GenerationUncheckedConstructorDispatcherParameterName = "uncheckedConstructor";
	public const string TypeNameFormattingExtensionsMethodName = "AppendQualifiedCSharpTypeName";
}
