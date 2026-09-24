; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SEMPTR4101 | SemPtr.FunctionPointerGenerator | Warning | Later C# version is required
SEMPTR4201 | SemPtr.FunctionPointerGenerator | Error | Function pointer generation specified more than once
SEMPTR4202 | SemPtr.FunctionPointerGenerator | Error | Conflicting function pointer calling conventions specified
SEMPTR4301 | SemPtr.FunctionPointerGenerator | Error | Could not create raw function pointer type
