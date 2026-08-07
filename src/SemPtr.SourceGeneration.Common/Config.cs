namespace SemPtr.SourceGeneration;

internal static class Config
{
	public const string FunctionPointerInterfaceTypeName = "IFunctionPointer";
	public const string FunctionPointerTypedInterfaceTypeName = "ITypedFunctionPointer";
	public const string FunctionPointerInterfaceFromDelegateMethodName = "FromDelegate";
	public const string FunctionPointerInterfaceToDelegateMethodName = "ToDelegate";
	public const string GenerationDelegateTypeParameterName = "TDelegate";
	public const string GenerationExtensionArgumentName = "__ptr__";
	public const string GenerationRawPointerFieldName = "mRaw";
	public const string GenerationTargetPropertyName = "Target";
	public const string GenerationTypeParameterName = "T";
	public const string InternalNamespaceName = "SemPtr.Internal";
	public const string PointerDispatchInterfaceTypeName = "IPointerDispatch";
	public const string PointerExtensionsTypeName = "PointerExtensions";
	public const string PointerInterfaceTypeAsIntPtrPropertyName = "AsIntPtr";
	public const string PointerInterfaceTypeAsUIntPtrPropertyName = "AsUIntPtr";
	public const string PointerInterfaceTypeFromRawMethodName = "FromRaw";
	public const string PointerInterfaceTypeFromIntPtrMethodName = "FromIntPtr";
	public const string PointerInterfaceTypeFromUIntPtrMethodName = "FromUIntPtr";
	public const string PointerInterfaceTypeName = "IPointer";
	public const string PointerInterfaceTypeRawPointerPropertyName = "Raw";
	public const string PointerNamespaceName = "SemPtr";
	public const string PointerTypedInterfaceTypeName = "ITypedPointer";
	public const string PointerFormatTypeDefaultFormatStringPropertyName = "DefaultFormatString";
	public const string PointerFormatTypeName = "PointerFormat";
	public const string TypeNameFormattingExtensionsMethodName = "AppendQualifiedCSharpTypeName";
}
