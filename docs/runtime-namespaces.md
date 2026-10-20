# Runtime namespace inventory

Namespaces containing public types or public type forwarders in the published
.NET 11 RC2 assemblies. Generated from DLL metadata; this is not an API support
matrix. Some types require browser-specific implementations or are unsupported.
See [llm.md](../llm.md) for imports, examples and execution constraints.

This build exposes **242 namespace names**. Assembly names and namespace
names are not interchangeable; a `using` directive imports a namespace, not a DLL.

| Namespace | Assembly metadata |
| --- | --- |
| `Concentus` | `Concentus.dll` |
| `Concentus.Common` | `Concentus.dll` |
| `Concentus.Enums` | `Concentus.dll` |
| `Concentus.Oggfile` | `Concentus.Oggfile.dll` |
| `Concentus.Structs` | `Concentus.dll` |
| `DotnetDide` | `DotnetDide.Core.dll` |
| `DotnetDide.Browser` | `DotnetDide.Browser.dll` |
| `Dotnetdide` | `DotnetDide.Core.dll` |
| `Internal` | `System.Private.CoreLib.dll` |
| `Microsoft.AspNetCore.Antiforgery` | `Microsoft.AspNetCore.Http.Abstractions.dll`, `Microsoft.AspNetCore.Http.Features.dll` |
| `Microsoft.AspNetCore.Authorization` | `Microsoft.AspNetCore.Authorization.dll`, `Microsoft.AspNetCore.Metadata.dll` |
| `Microsoft.AspNetCore.Authorization.Infrastructure` | `Microsoft.AspNetCore.Authorization.dll` |
| `Microsoft.AspNetCore.Builder` | `Microsoft.AspNetCore.Http.Abstractions.dll`, `Microsoft.AspNetCore.Http.dll`, `Microsoft.AspNetCore.Http.Extensions.dll`, `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Builder.Extensions` | `Microsoft.AspNetCore.Http.Abstractions.dll` |
| `Microsoft.AspNetCore.Connections` | `Microsoft.AspNetCore.Connections.Abstractions.dll` |
| `Microsoft.AspNetCore.Connections.Abstractions` | `Microsoft.AspNetCore.Connections.Abstractions.dll` |
| `Microsoft.AspNetCore.Connections.Features` | `Microsoft.AspNetCore.Connections.Abstractions.dll`, `Microsoft.AspNetCore.Http.Abstractions.dll` |
| `Microsoft.AspNetCore.Cors.Infrastructure` | `Microsoft.AspNetCore.Http.Abstractions.dll` |
| `Microsoft.AspNetCore.Hosting.Server` | `Microsoft.AspNetCore.Hosting.Server.Abstractions.dll` |
| `Microsoft.AspNetCore.Hosting.Server.Abstractions` | `Microsoft.AspNetCore.Hosting.Server.Abstractions.dll` |
| `Microsoft.AspNetCore.Hosting.Server.Features` | `Microsoft.AspNetCore.Hosting.Server.Abstractions.dll` |
| `Microsoft.AspNetCore.Http` | `Microsoft.AspNetCore.Http.Abstractions.dll`, `Microsoft.AspNetCore.Http.dll`, `Microsoft.AspNetCore.Http.Extensions.dll`, `Microsoft.AspNetCore.Http.Features.dll`, `Microsoft.AspNetCore.Routing.Abstractions.dll`, `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Http.Extensions` | `Microsoft.AspNetCore.Http.Extensions.dll` |
| `Microsoft.AspNetCore.Http.Features` | `Microsoft.AspNetCore.Http.Abstractions.dll`, `Microsoft.AspNetCore.Http.dll`, `Microsoft.AspNetCore.Http.Features.dll`, `Microsoft.AspNetCore.Routing.Abstractions.dll`, `Microsoft.Extensions.Features.dll` |
| `Microsoft.AspNetCore.Http.Features.Authentication` | `Microsoft.AspNetCore.Http.dll`, `Microsoft.AspNetCore.Http.Features.dll` |
| `Microsoft.AspNetCore.Http.Headers` | `Microsoft.AspNetCore.Http.Extensions.dll` |
| `Microsoft.AspNetCore.Http.HttpResults` | `Microsoft.AspNetCore.Http.Abstractions.dll` |
| `Microsoft.AspNetCore.Http.Json` | `Microsoft.AspNetCore.Http.Extensions.dll` |
| `Microsoft.AspNetCore.Http.Metadata` | `Microsoft.AspNetCore.Http.Abstractions.dll` |
| `Microsoft.AspNetCore.Http.Timeouts` | `Microsoft.AspNetCore.Http.dll` |
| `Microsoft.AspNetCore.Mvc` | `Microsoft.AspNetCore.Http.Abstractions.dll`, `Microsoft.AspNetCore.Http.Extensions.dll` |
| `Microsoft.AspNetCore.Routing` | `Microsoft.AspNetCore.Http.Abstractions.dll`, `Microsoft.AspNetCore.Routing.Abstractions.dll`, `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Routing.Constraints` | `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Routing.Internal` | `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Routing.Matching` | `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Routing.Patterns` | `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Routing.Template` | `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.Routing.Tree` | `Microsoft.AspNetCore.Routing.dll` |
| `Microsoft.AspNetCore.WebUtilities` | `Microsoft.AspNetCore.WebUtilities.dll` |
| `Microsoft.CSharp` | `System.dll` |
| `Microsoft.CSharp.RuntimeBinder` | `Microsoft.CSharp.dll` |
| `Microsoft.CodeAnalysis` | `Microsoft.CodeAnalysis.CSharp.dll`, `Microsoft.CodeAnalysis.dll` |
| `Microsoft.CodeAnalysis.CSharp` | `Microsoft.CodeAnalysis.CSharp.dll` |
| `Microsoft.CodeAnalysis.CSharp.Syntax` | `Microsoft.CodeAnalysis.CSharp.dll` |
| `Microsoft.CodeAnalysis.Diagnostics` | `Microsoft.CodeAnalysis.dll` |
| `Microsoft.CodeAnalysis.Diagnostics.Telemetry` | `Microsoft.CodeAnalysis.dll` |
| `Microsoft.CodeAnalysis.Emit` | `Microsoft.CodeAnalysis.dll` |
| `Microsoft.CodeAnalysis.FlowAnalysis` | `Microsoft.CodeAnalysis.dll` |
| `Microsoft.CodeAnalysis.Operations` | `Microsoft.CodeAnalysis.dll` |
| `Microsoft.CodeAnalysis.Text` | `Microsoft.CodeAnalysis.dll` |
| `Microsoft.Extensions.Caching.Distributed` | `Microsoft.Extensions.Caching.Abstractions.dll` |
| `Microsoft.Extensions.Caching.Hybrid` | `Microsoft.Extensions.Caching.Abstractions.dll` |
| `Microsoft.Extensions.Caching.Memory` | `Microsoft.Extensions.Caching.Abstractions.dll` |
| `Microsoft.Extensions.Configuration` | `Microsoft.Extensions.Configuration.Abstractions.dll`, `Microsoft.Extensions.Configuration.Binder.dll`, `Microsoft.Extensions.Configuration.dll` |
| `Microsoft.Extensions.Configuration.Memory` | `Microsoft.Extensions.Configuration.dll` |
| `Microsoft.Extensions.DependencyInjection` | `Microsoft.AspNetCore.Authorization.dll`, `Microsoft.AspNetCore.Http.dll`, `Microsoft.AspNetCore.Http.Extensions.dll`, `Microsoft.AspNetCore.Routing.dll`, `Microsoft.Extensions.DependencyInjection.Abstractions.dll`, `Microsoft.Extensions.DependencyInjection.dll`, `Microsoft.Extensions.Diagnostics.dll`, `Microsoft.Extensions.Hosting.Abstractions.dll`, `Microsoft.Extensions.Logging.dll`, `Microsoft.Extensions.Options.ConfigurationExtensions.dll`, `Microsoft.Extensions.Options.dll`, `Microsoft.Extensions.Validation.dll` |
| `Microsoft.Extensions.DependencyInjection.Extensions` | `Microsoft.Extensions.DependencyInjection.Abstractions.dll` |
| `Microsoft.Extensions.Diagnostics.Metrics` | `Microsoft.Extensions.Diagnostics.Abstractions.dll`, `Microsoft.Extensions.Diagnostics.dll` |
| `Microsoft.Extensions.Diagnostics.Metrics.Configuration` | `Microsoft.Extensions.Diagnostics.dll` |
| `Microsoft.Extensions.Diagnostics.Tracing` | `Microsoft.Extensions.Diagnostics.Abstractions.dll`, `Microsoft.Extensions.Diagnostics.dll` |
| `Microsoft.Extensions.FileProviders` | `Microsoft.Extensions.FileProviders.Abstractions.dll` |
| `Microsoft.Extensions.Hosting` | `Microsoft.Extensions.Hosting.Abstractions.dll` |
| `Microsoft.Extensions.Internal` | `Microsoft.Extensions.Caching.Abstractions.dll` |
| `Microsoft.Extensions.Localization` | `Microsoft.Extensions.Localization.Abstractions.dll` |
| `Microsoft.Extensions.Logging` | `Microsoft.Extensions.Logging.Abstractions.dll`, `Microsoft.Extensions.Logging.dll` |
| `Microsoft.Extensions.Logging.Abstractions` | `Microsoft.Extensions.Logging.Abstractions.dll` |
| `Microsoft.Extensions.Logging.Abstractions.Internal` | `Microsoft.Extensions.Logging.Abstractions.dll` |
| `Microsoft.Extensions.Logging.Internal` | `Microsoft.Extensions.Logging.Abstractions.dll` |
| `Microsoft.Extensions.ObjectPool` | `Microsoft.Extensions.ObjectPool.dll` |
| `Microsoft.Extensions.Options` | `Microsoft.Extensions.Options.ConfigurationExtensions.dll`, `Microsoft.Extensions.Options.dll` |
| `Microsoft.Extensions.Primitives` | `Microsoft.Extensions.Primitives.dll` |
| `Microsoft.Extensions.Validation` | `Microsoft.Extensions.Validation.dll` |
| `Microsoft.Net.Http.Headers` | `Microsoft.Net.Http.Headers.dll` |
| `Microsoft.SqlServer.Server` | `System.Data.dll` |
| `Microsoft.VisualBasic` | `Microsoft.VisualBasic.Core.dll`, `Microsoft.VisualBasic.dll`, `System.dll` |
| `Microsoft.VisualBasic.CompilerServices` | `Microsoft.VisualBasic.Core.dll`, `Microsoft.VisualBasic.dll` |
| `Microsoft.VisualBasic.FileIO` | `Microsoft.VisualBasic.Core.dll`, `Microsoft.VisualBasic.dll` |
| `Microsoft.Win32` | `Microsoft.Win32.Registry.dll`, `mscorlib.dll`, `System.dll` |
| `Microsoft.Win32.SafeHandles` | `Microsoft.Win32.Registry.dll`, `mscorlib.dll`, `netstandard.dll`, `System.Core.dll`, `System.Diagnostics.Process.dll`, `System.dll`, `System.IO.FileSystem.dll`, `System.IO.FileSystem.Primitives.dll`, `System.IO.MemoryMappedFiles.dll`, `System.IO.Pipes.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Handles.dll`, `System.Security.Cryptography.Cng.dll`, `System.Security.Cryptography.dll`, `System.Security.Cryptography.X509Certificates.dll`, `System.Security.Principal.Windows.dll` |
| `NAudio.Codecs` | `NAudio.Core.dll` |
| `NAudio.Dsp` | `NAudio.Core.dll` |
| `NAudio.Effects` | `NAudio.Core.dll` |
| `NAudio.Sequencing` | `NAudio.Core.dll` |
| `NAudio.Sfz` | `NAudio.Core.dll` |
| `NAudio.SoundFont` | `NAudio.Core.dll` |
| `NAudio.Utils` | `NAudio.Core.dll` |
| `NAudio.Wave` | `NAudio.Core.dll` |
| `NAudio.Wave.SampleProviders` | `NAudio.Core.dll` |
| `NuGet.Frameworks` | `NuGet.Frameworks.dll` |
| `NuGet.Versioning` | `NuGet.Versioning.dll` |
| `StbImageSharp` | `StbImageSharp.dll` |
| `StbImageWriteSharp` | `StbImageWriteSharp.dll` |
| `System` | `Microsoft.CodeAnalysis.dll`, `mscorlib.dll`, `netstandard.dll`, `System.AppContext.dll`, `System.ComponentModel.dll`, `System.ComponentModel.TypeConverter.dll`, `System.Console.dll`, `System.Core.dll`, `System.Data.Common.dll`, `System.dll`, `System.Globalization.Extensions.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Private.Uri.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.Serialization.Formatters.dll`, `System.Threading.Tasks.dll`, `System.Threading.Thread.dll`, `System.ValueTuple.dll` |
| `System.Buffers` | `mscorlib.dll`, `netstandard.dll`, `System.Buffers.dll`, `System.Memory.dll`, `System.Numerics.Tensors.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Buffers.Binary` | `mscorlib.dll`, `netstandard.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll` |
| `System.Buffers.Text` | `mscorlib.dll`, `netstandard.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.CodeDom` | `System.dll` |
| `System.CodeDom.Compiler` | `netstandard.dll`, `System.Diagnostics.Tools.dll`, `System.dll`, `System.IO.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll` |
| `System.Collections` | `mscorlib.dll`, `netstandard.dll`, `System.Collections.dll`, `System.Collections.NonGeneric.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll` |
| `System.Collections.Concurrent` | `mscorlib.dll`, `netstandard.dll`, `System.Collections.Concurrent.dll`, `System.dll`, `System.Private.CoreLib.dll` |
| `System.Collections.Frozen` | `System.Collections.Immutable.dll` |
| `System.Collections.Generic` | `Microsoft.CodeAnalysis.dll`, `mscorlib.dll`, `netstandard.dll`, `System.Collections.dll`, `System.Core.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Collections.Immutable` | `System.Collections.Immutable.dll` |
| `System.Collections.ObjectModel` | `mscorlib.dll`, `netstandard.dll`, `System.Collections.dll`, `System.dll`, `System.ObjectModel.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Windows.dll`, `WindowsBase.dll` |
| `System.Collections.Specialized` | `netstandard.dll`, `System.Collections.NonGeneric.dll`, `System.Collections.Specialized.dll`, `System.dll`, `System.ObjectModel.dll`, `System.Windows.dll`, `WindowsBase.dll` |
| `System.ComponentModel` | `Microsoft.Win32.Primitives.dll`, `netstandard.dll`, `System.ComponentModel.dll`, `System.ComponentModel.EventBasedAsync.dll`, `System.ComponentModel.Primitives.dll`, `System.ComponentModel.TypeConverter.dll`, `System.dll`, `System.ObjectModel.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Windows.dll` |
| `System.ComponentModel.DataAnnotations` | `System.ComponentModel.Annotations.dll`, `System.ComponentModel.DataAnnotations.dll` |
| `System.ComponentModel.DataAnnotations.Schema` | `System.ComponentModel.Annotations.dll`, `System.ComponentModel.DataAnnotations.dll` |
| `System.ComponentModel.Design` | `netstandard.dll`, `System.ComponentModel.TypeConverter.dll`, `System.dll` |
| `System.ComponentModel.Design.Serialization` | `netstandard.dll`, `System.ComponentModel.Primitives.dll`, `System.ComponentModel.TypeConverter.dll`, `System.dll` |
| `System.Configuration` | `System.Configuration.dll`, `System.dll` |
| `System.Configuration.Assemblies` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Configuration.Internal` | `System.Configuration.dll` |
| `System.Configuration.Provider` | `System.Configuration.dll` |
| `System.Data` | `netstandard.dll`, `System.Data.Common.dll`, `System.Data.DataSetExtensions.dll`, `System.Data.dll` |
| `System.Data.Common` | `netstandard.dll`, `System.Data.Common.dll`, `System.Data.dll` |
| `System.Data.Odbc` | `System.Data.dll` |
| `System.Data.OleDb` | `System.Data.dll` |
| `System.Data.Sql` | `System.Data.dll` |
| `System.Data.SqlClient` | `System.Data.dll` |
| `System.Data.SqlTypes` | `netstandard.dll`, `System.Data.Common.dll`, `System.Data.dll` |
| `System.Diagnostics` | `mscorlib.dll`, `netstandard.dll`, `System.Diagnostics.Debug.dll`, `System.Diagnostics.DiagnosticSource.dll`, `System.Diagnostics.FileVersionInfo.dll`, `System.Diagnostics.Process.dll`, `System.Diagnostics.StackTrace.dll`, `System.Diagnostics.TextWriterTraceListener.dll`, `System.Diagnostics.TraceSource.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll` |
| `System.Diagnostics.CodeAnalysis` | `Microsoft.CodeAnalysis.dll`, `mscorlib.dll`, `netstandard.dll`, `System.Diagnostics.Tools.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Diagnostics.Contracts` | `mscorlib.dll`, `netstandard.dll`, `System.Diagnostics.Contracts.dll`, `System.Private.CoreLib.dll` |
| `System.Diagnostics.Eventing.Reader` | `System.Core.dll` |
| `System.Diagnostics.Metrics` | `System.Diagnostics.DiagnosticSource.dll` |
| `System.Diagnostics.PerformanceData` | `System.Core.dll` |
| `System.Diagnostics.SymbolStore` | `mscorlib.dll`, `netstandard.dll`, `System.Diagnostics.StackTrace.dll`, `System.Private.CoreLib.dll` |
| `System.Diagnostics.Tracing` | `mscorlib.dll`, `netstandard.dll`, `System.Diagnostics.Tracing.dll`, `System.Private.CoreLib.dll` |
| `System.Drawing` | `netstandard.dll`, `System.ComponentModel.TypeConverter.dll`, `System.Drawing.dll`, `System.Drawing.Primitives.dll` |
| `System.Drawing.Configuration` | `System.Drawing.dll` |
| `System.Drawing.Design` | `System.Drawing.dll` |
| `System.Drawing.Drawing2D` | `System.Drawing.dll` |
| `System.Drawing.Imaging` | `System.Drawing.dll` |
| `System.Drawing.Printing` | `System.Drawing.dll` |
| `System.Drawing.Text` | `System.Drawing.dll` |
| `System.Dynamic` | `netstandard.dll`, `System.Core.dll`, `System.Dynamic.Runtime.dll`, `System.Linq.Expressions.dll` |
| `System.Formats.Asn1` | `System.Formats.Asn1.dll` |
| `System.Formats.Tar` | `System.Formats.Tar.dll` |
| `System.Globalization` | `mscorlib.dll`, `netstandard.dll`, `System.Globalization.Calendars.dll`, `System.Globalization.dll`, `System.Globalization.Extensions.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll` |
| `System.IO` | `mscorlib.dll`, `netstandard.dll`, `System.Core.dll`, `System.dll`, `System.IO.dll`, `System.IO.FileSystem.AccessControl.dll`, `System.IO.FileSystem.dll`, `System.IO.FileSystem.DriveInfo.dll`, `System.IO.FileSystem.Primitives.dll`, `System.IO.FileSystem.Watcher.dll`, `System.IO.UnmanagedMemoryStream.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.Handles.dll`, `System.Runtime.InteropServices.dll`, `WindowsBase.dll` |
| `System.IO.Compression` | `netstandard.dll`, `System.dll`, `System.IO.Compression.Brotli.dll`, `System.IO.Compression.dll`, `System.IO.Compression.FileSystem.dll`, `System.IO.Compression.ZipFile.dll` |
| `System.IO.Enumeration` | `mscorlib.dll`, `netstandard.dll`, `System.IO.FileSystem.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.IO.IsolatedStorage` | `mscorlib.dll`, `netstandard.dll`, `System.IO.IsolatedStorage.dll` |
| `System.IO.MemoryMappedFiles` | `netstandard.dll`, `System.Core.dll`, `System.IO.MemoryMappedFiles.dll` |
| `System.IO.Packaging` | `WindowsBase.dll` |
| `System.IO.Pipelines` | `System.IO.Pipelines.dll` |
| `System.IO.Pipes` | `netstandard.dll`, `System.Core.dll`, `System.IO.Pipes.AccessControl.dll`, `System.IO.Pipes.dll` |
| `System.IO.Ports` | `System.dll` |
| `System.Linq` | `netstandard.dll`, `System.Collections.Immutable.dll`, `System.Core.dll`, `System.Linq.AsyncEnumerable.dll`, `System.Linq.dll`, `System.Linq.Expressions.dll`, `System.Linq.Parallel.dll`, `System.Linq.Queryable.dll` |
| `System.Linq.Expressions` | `netstandard.dll`, `System.Core.dll`, `System.Dynamic.Runtime.dll`, `System.Linq.Expressions.dll` |
| `System.Linq.Expressions.Interpreter` | `System.Linq.Expressions.dll` |
| `System.Media` | `System.dll` |
| `System.Net` | `netstandard.dll`, `System.dll`, `System.Net.dll`, `System.Net.HttpListener.dll`, `System.Net.NameResolution.dll`, `System.Net.Primitives.dll`, `System.Net.Requests.dll`, `System.Net.ServicePoint.dll`, `System.Net.WebClient.dll`, `System.Net.WebHeaderCollection.dll`, `System.Net.WebProxy.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll` |
| `System.Net.Cache` | `netstandard.dll`, `System.dll`, `System.Net.Primitives.dll`, `System.Net.Requests.dll` |
| `System.Net.Http` | `netstandard.dll`, `System.Net.Http.dll` |
| `System.Net.Http.Headers` | `netstandard.dll`, `System.Net.Http.dll` |
| `System.Net.Http.Json` | `System.Net.Http.Json.dll` |
| `System.Net.Http.Metrics` | `System.Net.Http.dll` |
| `System.Net.Mail` | `netstandard.dll`, `System.dll`, `System.Net.Mail.dll` |
| `System.Net.Mime` | `netstandard.dll`, `System.dll`, `System.Net.Mail.dll` |
| `System.Net.NetworkInformation` | `netstandard.dll`, `System.dll`, `System.Net.dll`, `System.Net.NetworkInformation.dll`, `System.Net.Ping.dll`, `System.Net.Primitives.dll` |
| `System.Net.PeerToPeer` | `System.Net.dll` |
| `System.Net.PeerToPeer.Collaboration` | `System.Net.dll` |
| `System.Net.Quic` | `System.Net.Quic.dll` |
| `System.Net.Security` | `netstandard.dll`, `System.dll`, `System.Net.Primitives.dll`, `System.Net.Security.dll` |
| `System.Net.ServerSentEvents` | `System.Net.ServerSentEvents.dll` |
| `System.Net.Sockets` | `netstandard.dll`, `System.dll`, `System.Net.dll`, `System.Net.Primitives.dll`, `System.Net.Sockets.dll` |
| `System.Net.WebSockets` | `netstandard.dll`, `System.dll`, `System.Net.HttpListener.dll`, `System.Net.WebSockets.Client.dll`, `System.Net.WebSockets.dll` |
| `System.Numerics` | `mscorlib.dll`, `netstandard.dll`, `System.Numerics.dll`, `System.Numerics.Vectors.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.Numerics.dll` |
| `System.Numerics.Colors` | `System.Numerics.Vectors.dll`, `System.Private.CoreLib.dll` |
| `System.Numerics.Tensors` | `System.Numerics.Tensors.dll` |
| `System.Reflection` | `mscorlib.dll`, `netstandard.dll`, `System.dll`, `System.ObjectModel.dll`, `System.Private.CoreLib.dll`, `System.Reflection.DispatchProxy.dll`, `System.Reflection.dll`, `System.Reflection.Extensions.dll`, `System.Reflection.Metadata.dll`, `System.Reflection.Primitives.dll`, `System.Reflection.TypeExtensions.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll` |
| `System.Reflection.Emit` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Reflection.Emit.dll`, `System.Reflection.Emit.ILGeneration.dll`, `System.Reflection.Emit.Lightweight.dll`, `System.Reflection.Primitives.dll` |
| `System.Reflection.Metadata` | `System.Private.CoreLib.dll`, `System.Reflection.Metadata.dll`, `System.Runtime.Loader.dll` |
| `System.Reflection.Metadata.Ecma335` | `System.Reflection.Metadata.dll` |
| `System.Reflection.PortableExecutable` | `System.Reflection.Metadata.dll` |
| `System.Resources` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Resources.Reader.dll`, `System.Resources.ResourceManager.dll`, `System.Resources.Writer.dll`, `System.Runtime.dll` |
| `System.Runtime` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll` |
| `System.Runtime.CompilerServices` | `Microsoft.CodeAnalysis.dll`, `Microsoft.Extensions.Caching.Abstractions.dll`, `mscorlib.dll`, `netstandard.dll`, `System.Core.dll`, `System.Diagnostics.Contracts.dll`, `System.Diagnostics.DiagnosticSource.dll`, `System.Dynamic.Runtime.dll`, `System.Linq.Expressions.dll`, `System.Private.CoreLib.dll`, `System.Runtime.CompilerServices.Unsafe.dll`, `System.Runtime.CompilerServices.VisualC.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.Loader.dll`, `System.Text.Json.dll`, `System.Threading.Tasks.dll`, `System.Threading.Tasks.Extensions.dll`, `System.ValueTuple.dll` |
| `System.Runtime.ConstrainedExecution` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Runtime.ExceptionServices` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Runtime.InteropServices` | `mscorlib.dll`, `netstandard.dll`, `System.Collections.Immutable.dll`, `System.Core.dll`, `System.Diagnostics.Process.dll`, `System.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Handles.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.InteropServices.RuntimeInformation.dll`, `System.Text.Json.dll` |
| `System.Runtime.InteropServices.ComTypes` | `mscorlib.dll`, `netstandard.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.InteropServices.dll` |
| `System.Runtime.InteropServices.Java` | `System.Private.CoreLib.dll`, `System.Runtime.InteropServices.dll` |
| `System.Runtime.InteropServices.JavaScript` | `System.Runtime.InteropServices.JavaScript.dll` |
| `System.Runtime.InteropServices.Marshalling` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.InteropServices.dll` |
| `System.Runtime.InteropServices.ObjectiveC` | `System.Private.CoreLib.dll`, `System.Runtime.InteropServices.dll` |
| `System.Runtime.InteropServices.Swift` | `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Runtime.Intrinsics` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Intrinsics.Arm` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Intrinsics.Wasm` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Intrinsics.X86` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Loader` | `System.Private.CoreLib.dll`, `System.Runtime.Loader.dll` |
| `System.Runtime.Remoting` | `mscorlib.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Runtime.Serialization` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Private.DataContractSerialization.dll`, `System.Runtime.dll`, `System.Runtime.Serialization.dll`, `System.Runtime.Serialization.Formatters.dll`, `System.Runtime.Serialization.Json.dll`, `System.Runtime.Serialization.Primitives.dll`, `System.Runtime.Serialization.Xml.dll` |
| `System.Runtime.Serialization.DataContracts` | `System.Private.DataContractSerialization.dll`, `System.Runtime.Serialization.Xml.dll` |
| `System.Runtime.Serialization.Formatters` | `mscorlib.dll`, `netstandard.dll`, `System.Runtime.Serialization.Formatters.dll` |
| `System.Runtime.Serialization.Formatters.Binary` | `mscorlib.dll`, `netstandard.dll`, `System.Runtime.Serialization.Formatters.dll` |
| `System.Runtime.Serialization.Json` | `netstandard.dll`, `System.Private.DataContractSerialization.dll`, `System.Runtime.Serialization.dll`, `System.Runtime.Serialization.Json.dll`, `System.ServiceModel.Web.dll` |
| `System.Runtime.Versioning` | `mscorlib.dll`, `netstandard.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll` |
| `System.Security` | `mscorlib.dll`, `netstandard.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll`, `System.Security.SecureString.dll` |
| `System.Security.AccessControl` | `Microsoft.Win32.Registry.dll`, `mscorlib.dll`, `System.dll`, `System.IO.FileSystem.AccessControl.dll`, `System.Security.AccessControl.dll`, `System.Threading.AccessControl.dll` |
| `System.Security.Authentication` | `netstandard.dll`, `System.dll`, `System.Net.Primitives.dll`, `System.Net.Security.dll` |
| `System.Security.Authentication.ExtendedProtection` | `netstandard.dll`, `System.ComponentModel.TypeConverter.dll`, `System.dll`, `System.Net.Primitives.dll`, `System.Net.Security.dll` |
| `System.Security.Claims` | `mscorlib.dll`, `netstandard.dll`, `System.Security.Claims.dll` |
| `System.Security.Cryptography` | `mscorlib.dll`, `netstandard.dll`, `System.Core.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Security.Cryptography.Algorithms.dll`, `System.Security.Cryptography.Cng.dll`, `System.Security.Cryptography.Csp.dll`, `System.Security.Cryptography.dll`, `System.Security.Cryptography.Encoding.dll`, `System.Security.Cryptography.OpenSsl.dll`, `System.Security.Cryptography.Primitives.dll`, `System.Security.dll` |
| `System.Security.Cryptography.Pkcs` | `System.Security.dll` |
| `System.Security.Cryptography.X509Certificates` | `mscorlib.dll`, `netstandard.dll`, `System.Core.dll`, `System.dll`, `System.Security.Cryptography.dll`, `System.Security.Cryptography.X509Certificates.dll`, `System.Security.dll` |
| `System.Security.Cryptography.Xml` | `System.Security.dll` |
| `System.Security.Permissions` | `mscorlib.dll`, `netstandard.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Extensions.dll`, `System.Security.dll`, `WindowsBase.dll` |
| `System.Security.Policy` | `mscorlib.dll`, `System.Security.AccessControl.dll` |
| `System.Security.Principal` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Security.Claims.dll`, `System.Security.Principal.dll`, `System.Security.Principal.Windows.dll` |
| `System.ServiceModel.Syndication` | `System.ServiceModel.Web.dll` |
| `System.ServiceProcess` | `System.ServiceProcess.dll` |
| `System.Text` | `mscorlib.dll`, `netstandard.dll`, `System.Globalization.Extensions.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Text.Encoding.CodePages.dll`, `System.Text.Encoding.dll`, `System.Text.Encoding.Extensions.dll` |
| `System.Text.Encodings.Web` | `System.Text.Encodings.Web.dll` |
| `System.Text.Json` | `System.Text.Json.dll` |
| `System.Text.Json.Nodes` | `System.Text.Json.dll` |
| `System.Text.Json.Schema` | `System.Text.Json.dll` |
| `System.Text.Json.Serialization` | `System.Text.Json.dll` |
| `System.Text.Json.Serialization.Metadata` | `System.Text.Json.dll` |
| `System.Text.RegularExpressions` | `netstandard.dll`, `System.dll`, `System.Text.RegularExpressions.dll` |
| `System.Text.Unicode` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Text.Encodings.Web.dll` |
| `System.Threading` | `mscorlib.dll`, `netstandard.dll`, `System.Core.dll`, `System.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Runtime.Handles.dll`, `System.Threading.AccessControl.dll`, `System.Threading.dll`, `System.Threading.Overlapped.dll`, `System.Threading.Tasks.dll`, `System.Threading.Thread.dll`, `System.Threading.ThreadPool.dll`, `System.Threading.Timer.dll` |
| `System.Threading.Channels` | `System.Threading.Channels.dll` |
| `System.Threading.Tasks` | `mscorlib.dll`, `netstandard.dll`, `System.Core.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Threading.Tasks.dll`, `System.Threading.Tasks.Extensions.dll`, `System.Threading.Tasks.Parallel.dll` |
| `System.Threading.Tasks.Dataflow` | `System.Threading.Tasks.Dataflow.dll` |
| `System.Threading.Tasks.Sources` | `mscorlib.dll`, `netstandard.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Threading.Tasks.Extensions.dll` |
| `System.Timers` | `netstandard.dll`, `System.ComponentModel.TypeConverter.dll`, `System.dll` |
| `System.Transactions` | `netstandard.dll`, `System.Transactions.dll`, `System.Transactions.Local.dll` |
| `System.Web` | `netstandard.dll`, `System.dll`, `System.Web.dll`, `System.Web.HttpUtility.dll` |
| `System.Windows.Input` | `netstandard.dll`, `System.dll`, `System.ObjectModel.dll`, `System.Windows.dll` |
| `System.Windows.Markup` | `System.dll`, `System.ObjectModel.dll`, `WindowsBase.dll` |
| `System.Xml` | `netstandard.dll`, `System.Data.Common.dll`, `System.Data.dll`, `System.Private.DataContractSerialization.dll`, `System.Private.Xml.dll`, `System.Runtime.Serialization.dll`, `System.Runtime.Serialization.Xml.dll`, `System.Xml.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.XmlDocument.dll`, `System.Xml.XPath.dll` |
| `System.Xml.Linq` | `netstandard.dll`, `System.Private.Xml.Linq.dll`, `System.Xml.Linq.dll`, `System.Xml.XDocument.dll` |
| `System.Xml.Resolvers` | `netstandard.dll`, `System.Private.Xml.dll`, `System.Xml.dll`, `System.Xml.ReaderWriter.dll` |
| `System.Xml.Schema` | `netstandard.dll`, `System.Private.Xml.dll`, `System.Private.Xml.Linq.dll`, `System.Xml.dll`, `System.Xml.Linq.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.XDocument.dll` |
| `System.Xml.Serialization` | `netstandard.dll`, `System.Private.Xml.dll`, `System.Xml.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.Serialization.dll`, `System.Xml.XmlSerializer.dll` |
| `System.Xml.XPath` | `netstandard.dll`, `System.Private.Xml.dll`, `System.Private.Xml.Linq.dll`, `System.Xml.dll`, `System.Xml.Linq.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.XPath.dll`, `System.Xml.XPath.XDocument.dll` |
| `System.Xml.Xsl` | `netstandard.dll`, `System.Private.Xml.dll`, `System.Xml.dll`, `System.Xml.ReaderWriter.dll` |
| `System.Xml.Xsl.Runtime` | `System.Private.Xml.dll` |
