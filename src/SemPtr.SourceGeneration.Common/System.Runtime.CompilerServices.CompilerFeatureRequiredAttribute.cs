namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
public sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
	public string FeatureName => featureName;

	public bool IsOptional { get; init; }

	public const string RefStructs = nameof(RefStructs);

	public const string RequiredMembers = nameof(RequiredMembers);
}
