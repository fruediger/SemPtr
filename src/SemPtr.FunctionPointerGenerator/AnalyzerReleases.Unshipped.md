; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SEMPTRFP0101 | SemPtr.FunctionPointerGenerator | Warning | Later C# version is required
SEMPTRFP0201 | SemPtr.FunctionPointerGenerator | Error | Function pointer generation specified more than once
SEMPTRFP0202 | SemPtr.FunctionPointerGenerator | Error | Conflicting function pointer calling conventions specified
SEMPTRFP0301 | SemPtr.FunctionPointerGenerator | Error | Could not create raw function pointer type
