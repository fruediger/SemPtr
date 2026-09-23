namespace SemPtr.Analyzers;

internal static class Diagnostics
{
	public const string DiagnosticIdPrefix = "SEMPTR";

	public const string ImplicitConversionFromRawPointerToNonNullPointerDiagnosticId = $"{DiagnosticIdPrefix}2101";

	public const string LaterCSharpVersionRequiredDiagnosticId = $"{DiagnosticIdPrefix}4101";
	public const string MultipleFunctionPointerGenerationAttributesDiagnosticId = $"{DiagnosticIdPrefix}4201";
	public const string ConflictingFunctionPointerCallingConventionsDiagnosticId = $"{DiagnosticIdPrefix}4202";
	public const string CouldNotCreateRawFunctionPointerTypeDiagnosticId = $"{DiagnosticIdPrefix}4301";
}