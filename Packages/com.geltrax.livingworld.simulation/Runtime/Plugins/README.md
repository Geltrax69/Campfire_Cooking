# Unity JSON runtime

The simulation already uses System.Text.Json. Unity's .NET Standard 2.1 profile
does not ship that serializer, so these upstream NuGet assemblies are retained:

| Assembly | Version | Source |
|---|---|---|
| System.Text.Json | 8.0.6 | https://www.nuget.org/packages/System.Text.Json/8.0.6 |
| System.Text.Encodings.Web | 8.0.0 | https://www.nuget.org/packages/System.Text.Encodings.Web/8.0.0 |
| Microsoft.Bcl.AsyncInterfaces | 8.0.0 | https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces/8.0.0 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0 |

All are MIT licensed; original license files are adjacent. These are the
netstandard2.0 binaries, copied from the official NuGet packages. Unity supplies
System.Memory, System.Buffers and Tasks.Extensions through its profile; do not
add duplicate platform assemblies. The dotnet test project continues to use its
.NET 8 framework serializer rather than loading these Unity plugins.

Editor compilation and tests must pass. IL2CPP/AOT on an iPad remains a separate
U-09 validation gate; this setup does not claim device compatibility yet.
