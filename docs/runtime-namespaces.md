# Runtime namespace inventory

Namespaces containing public types or public type forwarders in the published
.NET 11 RC2 assemblies. Generated from DLL metadata; this is not an API support
matrix. Some types require browser-specific implementations or are unsupported.
See [llm.md](../llm.md) for imports, examples and execution constraints.

This build exposes **206 namespace names**. Assembly names and namespace
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
| `Microsoft.Extensions.Configuration` | `Microsoft.Extensions.Configuration.Abstractions.dll` |
| `Microsoft.Extensions.DependencyInjection` | `Microsoft.Extensions.DependencyInjection.Abstractions.dll`, `Microsoft.Extensions.Hosting.Abstractions.dll`, `Microsoft.Extensions.Options.dll` |
| `Microsoft.Extensions.DependencyInjection.Extensions` | `Microsoft.Extensions.DependencyInjection.Abstractions.dll` |
| `Microsoft.Extensions.Diagnostics.Metrics` | `Microsoft.Extensions.Diagnostics.Abstractions.dll` |
| `Microsoft.Extensions.Diagnostics.Tracing` | `Microsoft.Extensions.Diagnostics.Abstractions.dll` |
| `Microsoft.Extensions.FileProviders` | `Microsoft.Extensions.FileProviders.Abstractions.dll` |
| `Microsoft.Extensions.Hosting` | `Microsoft.Extensions.Hosting.Abstractions.dll` |
| `Microsoft.Extensions.Internal` | `Microsoft.Extensions.Caching.Abstractions.dll` |
| `Microsoft.Extensions.Logging` | `Microsoft.Extensions.Logging.Abstractions.dll` |
| `Microsoft.Extensions.Logging.Abstractions` | `Microsoft.Extensions.Logging.Abstractions.dll` |
| `Microsoft.Extensions.Logging.Abstractions.Internal` | `Microsoft.Extensions.Logging.Abstractions.dll` |
| `Microsoft.Extensions.Logging.Internal` | `Microsoft.Extensions.Logging.Abstractions.dll` |
| `Microsoft.Extensions.Options` | `Microsoft.Extensions.Options.dll` |
| `Microsoft.Extensions.Primitives` | `Microsoft.Extensions.Primitives.dll` |
| `Microsoft.SqlServer.Server` | `System.Data.dll` |
| `Microsoft.VisualBasic` | `Microsoft.VisualBasic.Core.dll`, `Microsoft.VisualBasic.dll`, `System.dll` |
| `Microsoft.VisualBasic.CompilerServices` | `Microsoft.VisualBasic.Core.dll`, `Microsoft.VisualBasic.dll` |
| `Microsoft.VisualBasic.FileIO` | `Microsoft.VisualBasic.Core.dll`, `Microsoft.VisualBasic.dll` |
| `Microsoft.Win32` | `Microsoft.Win32.Registry.dll`, `System.dll`, `mscorlib.dll` |
| `Microsoft.Win32.SafeHandles` | `Microsoft.Win32.Registry.dll`, `System.Core.dll`, `System.Diagnostics.Process.dll`, `System.IO.FileSystem.Primitives.dll`, `System.IO.FileSystem.dll`, `System.IO.MemoryMappedFiles.dll`, `System.IO.Pipes.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Handles.dll`, `System.Runtime.dll`, `System.Security.Cryptography.Cng.dll`, `System.Security.Cryptography.X509Certificates.dll`, `System.Security.Cryptography.dll`, `System.Security.Principal.Windows.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
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
| `System` | `Microsoft.CodeAnalysis.dll`, `System.AppContext.dll`, `System.ComponentModel.TypeConverter.dll`, `System.ComponentModel.dll`, `System.Console.dll`, `System.Core.dll`, `System.Data.Common.dll`, `System.Globalization.Extensions.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Private.Uri.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.Serialization.Formatters.dll`, `System.Runtime.dll`, `System.Threading.Tasks.dll`, `System.Threading.Thread.dll`, `System.ValueTuple.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Buffers` | `System.Buffers.dll`, `System.Memory.dll`, `System.Numerics.Tensors.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Buffers.Binary` | `System.Memory.dll`, `System.Private.CoreLib.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Buffers.Text` | `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.CodeDom` | `System.dll` |
| `System.CodeDom.Compiler` | `System.Diagnostics.Tools.dll`, `System.IO.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `System.dll`, `netstandard.dll` |
| `System.Collections` | `System.Collections.NonGeneric.dll`, `System.Collections.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Collections.Concurrent` | `System.Collections.Concurrent.dll`, `System.Private.CoreLib.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Collections.Frozen` | `System.Collections.Immutable.dll` |
| `System.Collections.Generic` | `Microsoft.CodeAnalysis.dll`, `System.Collections.dll`, `System.Core.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Collections.Immutable` | `System.Collections.Immutable.dll` |
| `System.Collections.ObjectModel` | `System.Collections.dll`, `System.ObjectModel.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Windows.dll`, `System.dll`, `WindowsBase.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Collections.Specialized` | `System.Collections.NonGeneric.dll`, `System.Collections.Specialized.dll`, `System.ObjectModel.dll`, `System.Windows.dll`, `System.dll`, `WindowsBase.dll`, `netstandard.dll` |
| `System.ComponentModel` | `Microsoft.Win32.Primitives.dll`, `System.ComponentModel.EventBasedAsync.dll`, `System.ComponentModel.Primitives.dll`, `System.ComponentModel.TypeConverter.dll`, `System.ComponentModel.dll`, `System.ObjectModel.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Windows.dll`, `System.dll`, `netstandard.dll` |
| `System.ComponentModel.DataAnnotations` | `System.ComponentModel.Annotations.dll`, `System.ComponentModel.DataAnnotations.dll` |
| `System.ComponentModel.DataAnnotations.Schema` | `System.ComponentModel.Annotations.dll`, `System.ComponentModel.DataAnnotations.dll` |
| `System.ComponentModel.Design` | `System.ComponentModel.TypeConverter.dll`, `System.dll`, `netstandard.dll` |
| `System.ComponentModel.Design.Serialization` | `System.ComponentModel.Primitives.dll`, `System.ComponentModel.TypeConverter.dll`, `System.dll`, `netstandard.dll` |
| `System.Configuration` | `System.Configuration.dll`, `System.dll` |
| `System.Configuration.Assemblies` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Configuration.Internal` | `System.Configuration.dll` |
| `System.Configuration.Provider` | `System.Configuration.dll` |
| `System.Data` | `System.Data.Common.dll`, `System.Data.DataSetExtensions.dll`, `System.Data.dll`, `netstandard.dll` |
| `System.Data.Common` | `System.Data.Common.dll`, `System.Data.dll`, `netstandard.dll` |
| `System.Data.Odbc` | `System.Data.dll` |
| `System.Data.OleDb` | `System.Data.dll` |
| `System.Data.Sql` | `System.Data.dll` |
| `System.Data.SqlClient` | `System.Data.dll` |
| `System.Data.SqlTypes` | `System.Data.Common.dll`, `System.Data.dll`, `netstandard.dll` |
| `System.Diagnostics` | `System.Diagnostics.Debug.dll`, `System.Diagnostics.DiagnosticSource.dll`, `System.Diagnostics.FileVersionInfo.dll`, `System.Diagnostics.Process.dll`, `System.Diagnostics.StackTrace.dll`, `System.Diagnostics.TextWriterTraceListener.dll`, `System.Diagnostics.TraceSource.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Diagnostics.CodeAnalysis` | `Microsoft.CodeAnalysis.dll`, `System.Diagnostics.Tools.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Diagnostics.Contracts` | `System.Diagnostics.Contracts.dll`, `System.Private.CoreLib.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Diagnostics.Eventing.Reader` | `System.Core.dll` |
| `System.Diagnostics.Metrics` | `System.Diagnostics.DiagnosticSource.dll` |
| `System.Diagnostics.PerformanceData` | `System.Core.dll` |
| `System.Diagnostics.SymbolStore` | `System.Diagnostics.StackTrace.dll`, `System.Private.CoreLib.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Diagnostics.Tracing` | `System.Diagnostics.Tracing.dll`, `System.Private.CoreLib.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Drawing` | `System.ComponentModel.TypeConverter.dll`, `System.Drawing.Primitives.dll`, `System.Drawing.dll`, `netstandard.dll` |
| `System.Drawing.Configuration` | `System.Drawing.dll` |
| `System.Drawing.Design` | `System.Drawing.dll` |
| `System.Drawing.Drawing2D` | `System.Drawing.dll` |
| `System.Drawing.Imaging` | `System.Drawing.dll` |
| `System.Drawing.Printing` | `System.Drawing.dll` |
| `System.Drawing.Text` | `System.Drawing.dll` |
| `System.Dynamic` | `System.Core.dll`, `System.Dynamic.Runtime.dll`, `System.Linq.Expressions.dll`, `netstandard.dll` |
| `System.Formats.Asn1` | `System.Formats.Asn1.dll` |
| `System.Formats.Tar` | `System.Formats.Tar.dll` |
| `System.Globalization` | `System.Globalization.Calendars.dll`, `System.Globalization.Extensions.dll`, `System.Globalization.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.IO` | `System.Core.dll`, `System.IO.FileSystem.AccessControl.dll`, `System.IO.FileSystem.DriveInfo.dll`, `System.IO.FileSystem.Primitives.dll`, `System.IO.FileSystem.Watcher.dll`, `System.IO.FileSystem.dll`, `System.IO.UnmanagedMemoryStream.dll`, `System.IO.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.Handles.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.dll`, `System.dll`, `WindowsBase.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.IO.Compression` | `System.IO.Compression.Brotli.dll`, `System.IO.Compression.FileSystem.dll`, `System.IO.Compression.ZipFile.dll`, `System.IO.Compression.dll`, `System.dll`, `netstandard.dll` |
| `System.IO.Enumeration` | `System.IO.FileSystem.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.IO.IsolatedStorage` | `System.IO.IsolatedStorage.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.IO.MemoryMappedFiles` | `System.Core.dll`, `System.IO.MemoryMappedFiles.dll`, `netstandard.dll` |
| `System.IO.Packaging` | `WindowsBase.dll` |
| `System.IO.Pipelines` | `System.IO.Pipelines.dll` |
| `System.IO.Pipes` | `System.Core.dll`, `System.IO.Pipes.AccessControl.dll`, `System.IO.Pipes.dll`, `netstandard.dll` |
| `System.IO.Ports` | `System.dll` |
| `System.Linq` | `System.Collections.Immutable.dll`, `System.Core.dll`, `System.Linq.AsyncEnumerable.dll`, `System.Linq.Expressions.dll`, `System.Linq.Parallel.dll`, `System.Linq.Queryable.dll`, `System.Linq.dll`, `netstandard.dll` |
| `System.Linq.Expressions` | `System.Core.dll`, `System.Dynamic.Runtime.dll`, `System.Linq.Expressions.dll`, `netstandard.dll` |
| `System.Linq.Expressions.Interpreter` | `System.Linq.Expressions.dll` |
| `System.Media` | `System.dll` |
| `System.Net` | `System.Net.HttpListener.dll`, `System.Net.NameResolution.dll`, `System.Net.Primitives.dll`, `System.Net.Requests.dll`, `System.Net.ServicePoint.dll`, `System.Net.WebClient.dll`, `System.Net.WebHeaderCollection.dll`, `System.Net.WebProxy.dll`, `System.Net.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `System.dll`, `netstandard.dll` |
| `System.Net.Cache` | `System.Net.Primitives.dll`, `System.Net.Requests.dll`, `System.dll`, `netstandard.dll` |
| `System.Net.Http` | `System.Net.Http.dll`, `netstandard.dll` |
| `System.Net.Http.Headers` | `System.Net.Http.dll`, `netstandard.dll` |
| `System.Net.Http.Json` | `System.Net.Http.Json.dll` |
| `System.Net.Http.Metrics` | `System.Net.Http.dll` |
| `System.Net.Mail` | `System.Net.Mail.dll`, `System.dll`, `netstandard.dll` |
| `System.Net.Mime` | `System.Net.Mail.dll`, `System.dll`, `netstandard.dll` |
| `System.Net.NetworkInformation` | `System.Net.NetworkInformation.dll`, `System.Net.Ping.dll`, `System.Net.Primitives.dll`, `System.Net.dll`, `System.dll`, `netstandard.dll` |
| `System.Net.PeerToPeer` | `System.Net.dll` |
| `System.Net.PeerToPeer.Collaboration` | `System.Net.dll` |
| `System.Net.Quic` | `System.Net.Quic.dll` |
| `System.Net.Security` | `System.Net.Primitives.dll`, `System.Net.Security.dll`, `System.dll`, `netstandard.dll` |
| `System.Net.ServerSentEvents` | `System.Net.ServerSentEvents.dll` |
| `System.Net.Sockets` | `System.Net.Primitives.dll`, `System.Net.Sockets.dll`, `System.Net.dll`, `System.dll`, `netstandard.dll` |
| `System.Net.WebSockets` | `System.Net.HttpListener.dll`, `System.Net.WebSockets.Client.dll`, `System.Net.WebSockets.dll`, `System.dll`, `netstandard.dll` |
| `System.Numerics` | `System.Numerics.Vectors.dll`, `System.Numerics.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.Numerics.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Numerics.Colors` | `System.Numerics.Vectors.dll`, `System.Private.CoreLib.dll` |
| `System.Numerics.Tensors` | `System.Numerics.Tensors.dll` |
| `System.Reflection` | `System.ObjectModel.dll`, `System.Private.CoreLib.dll`, `System.Reflection.DispatchProxy.dll`, `System.Reflection.Extensions.dll`, `System.Reflection.Metadata.dll`, `System.Reflection.Primitives.dll`, `System.Reflection.TypeExtensions.dll`, `System.Reflection.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Reflection.Emit` | `System.Private.CoreLib.dll`, `System.Reflection.Emit.ILGeneration.dll`, `System.Reflection.Emit.Lightweight.dll`, `System.Reflection.Emit.dll`, `System.Reflection.Primitives.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Reflection.Metadata` | `System.Private.CoreLib.dll`, `System.Reflection.Metadata.dll`, `System.Runtime.Loader.dll` |
| `System.Reflection.Metadata.Ecma335` | `System.Reflection.Metadata.dll` |
| `System.Reflection.PortableExecutable` | `System.Reflection.Metadata.dll` |
| `System.Resources` | `System.Private.CoreLib.dll`, `System.Resources.Reader.dll`, `System.Resources.ResourceManager.dll`, `System.Resources.Writer.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime` | `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.CompilerServices` | `Microsoft.CodeAnalysis.dll`, `Microsoft.Extensions.Caching.Abstractions.dll`, `System.Core.dll`, `System.Diagnostics.Contracts.dll`, `System.Diagnostics.DiagnosticSource.dll`, `System.Dynamic.Runtime.dll`, `System.Linq.Expressions.dll`, `System.Private.CoreLib.dll`, `System.Runtime.CompilerServices.Unsafe.dll`, `System.Runtime.CompilerServices.VisualC.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.Loader.dll`, `System.Runtime.dll`, `System.Text.Json.dll`, `System.Threading.Tasks.Extensions.dll`, `System.Threading.Tasks.dll`, `System.ValueTuple.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.ConstrainedExecution` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.ExceptionServices` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.InteropServices` | `System.Collections.Immutable.dll`, `System.Core.dll`, `System.Diagnostics.Process.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Handles.dll`, `System.Runtime.InteropServices.RuntimeInformation.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.dll`, `System.Text.Json.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.InteropServices.ComTypes` | `System.Private.CoreLib.dll`, `System.Runtime.InteropServices.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.InteropServices.Java` | `System.Private.CoreLib.dll`, `System.Runtime.InteropServices.dll` |
| `System.Runtime.InteropServices.JavaScript` | `System.Runtime.InteropServices.JavaScript.dll` |
| `System.Runtime.InteropServices.Marshalling` | `System.Private.CoreLib.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.dll` |
| `System.Runtime.InteropServices.ObjectiveC` | `System.Private.CoreLib.dll`, `System.Runtime.InteropServices.dll` |
| `System.Runtime.InteropServices.Swift` | `System.Private.CoreLib.dll`, `System.Runtime.dll` |
| `System.Runtime.Intrinsics` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Intrinsics.Arm` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Intrinsics.Wasm` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Intrinsics.X86` | `System.Private.CoreLib.dll`, `System.Runtime.Intrinsics.dll` |
| `System.Runtime.Loader` | `System.Private.CoreLib.dll`, `System.Runtime.Loader.dll` |
| `System.Runtime.Remoting` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `mscorlib.dll` |
| `System.Runtime.Serialization` | `System.Private.CoreLib.dll`, `System.Private.DataContractSerialization.dll`, `System.Runtime.Serialization.Formatters.dll`, `System.Runtime.Serialization.Json.dll`, `System.Runtime.Serialization.Primitives.dll`, `System.Runtime.Serialization.Xml.dll`, `System.Runtime.Serialization.dll`, `System.Runtime.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.Serialization.DataContracts` | `System.Private.DataContractSerialization.dll`, `System.Runtime.Serialization.Xml.dll` |
| `System.Runtime.Serialization.Formatters` | `System.Runtime.Serialization.Formatters.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.Serialization.Formatters.Binary` | `System.Runtime.Serialization.Formatters.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Runtime.Serialization.Json` | `System.Private.DataContractSerialization.dll`, `System.Runtime.Serialization.Json.dll`, `System.Runtime.Serialization.dll`, `System.ServiceModel.Web.dll`, `netstandard.dll` |
| `System.Runtime.Versioning` | `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Security` | `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.InteropServices.dll`, `System.Runtime.dll`, `System.Security.SecureString.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Security.AccessControl` | `Microsoft.Win32.Registry.dll`, `System.IO.FileSystem.AccessControl.dll`, `System.Security.AccessControl.dll`, `System.Threading.AccessControl.dll`, `System.dll`, `mscorlib.dll` |
| `System.Security.Authentication` | `System.Net.Primitives.dll`, `System.Net.Security.dll`, `System.dll`, `netstandard.dll` |
| `System.Security.Authentication.ExtendedProtection` | `System.ComponentModel.TypeConverter.dll`, `System.Net.Primitives.dll`, `System.Net.Security.dll`, `System.dll`, `netstandard.dll` |
| `System.Security.Claims` | `System.Security.Claims.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Security.Cryptography` | `System.Core.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Security.Cryptography.Algorithms.dll`, `System.Security.Cryptography.Cng.dll`, `System.Security.Cryptography.Csp.dll`, `System.Security.Cryptography.Encoding.dll`, `System.Security.Cryptography.OpenSsl.dll`, `System.Security.Cryptography.Primitives.dll`, `System.Security.Cryptography.dll`, `System.Security.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Security.Cryptography.Pkcs` | `System.Security.dll` |
| `System.Security.Cryptography.X509Certificates` | `System.Core.dll`, `System.Security.Cryptography.X509Certificates.dll`, `System.Security.Cryptography.dll`, `System.Security.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Security.Cryptography.Xml` | `System.Security.dll` |
| `System.Security.Permissions` | `System.Private.CoreLib.dll`, `System.Runtime.Extensions.dll`, `System.Runtime.dll`, `System.Security.dll`, `System.dll`, `WindowsBase.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Security.Policy` | `System.Security.AccessControl.dll`, `mscorlib.dll` |
| `System.Security.Principal` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Security.Claims.dll`, `System.Security.Principal.Windows.dll`, `System.Security.Principal.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.ServiceModel.Syndication` | `System.ServiceModel.Web.dll` |
| `System.ServiceProcess` | `System.ServiceProcess.dll` |
| `System.Text` | `System.Globalization.Extensions.dll`, `System.Memory.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Text.Encoding.CodePages.dll`, `System.Text.Encoding.Extensions.dll`, `System.Text.Encoding.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Text.Encodings.Web` | `System.Text.Encodings.Web.dll` |
| `System.Text.Json` | `System.Text.Json.dll` |
| `System.Text.Json.Nodes` | `System.Text.Json.dll` |
| `System.Text.Json.Schema` | `System.Text.Json.dll` |
| `System.Text.Json.Serialization` | `System.Text.Json.dll` |
| `System.Text.Json.Serialization.Metadata` | `System.Text.Json.dll` |
| `System.Text.RegularExpressions` | `System.Text.RegularExpressions.dll`, `System.dll`, `netstandard.dll` |
| `System.Text.Unicode` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Text.Encodings.Web.dll` |
| `System.Threading` | `System.Core.dll`, `System.Private.CoreLib.dll`, `System.Runtime.Handles.dll`, `System.Runtime.dll`, `System.Threading.AccessControl.dll`, `System.Threading.Overlapped.dll`, `System.Threading.Tasks.dll`, `System.Threading.Thread.dll`, `System.Threading.ThreadPool.dll`, `System.Threading.Timer.dll`, `System.Threading.dll`, `System.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Threading.Channels` | `System.Threading.Channels.dll` |
| `System.Threading.Tasks` | `System.Core.dll`, `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Threading.Tasks.Extensions.dll`, `System.Threading.Tasks.Parallel.dll`, `System.Threading.Tasks.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Threading.Tasks.Dataflow` | `System.Threading.Tasks.Dataflow.dll` |
| `System.Threading.Tasks.Sources` | `System.Private.CoreLib.dll`, `System.Runtime.dll`, `System.Threading.Tasks.Extensions.dll`, `mscorlib.dll`, `netstandard.dll` |
| `System.Timers` | `System.ComponentModel.TypeConverter.dll`, `System.dll`, `netstandard.dll` |
| `System.Transactions` | `System.Transactions.Local.dll`, `System.Transactions.dll`, `netstandard.dll` |
| `System.Web` | `System.Web.HttpUtility.dll`, `System.Web.dll`, `System.dll`, `netstandard.dll` |
| `System.Windows.Input` | `System.ObjectModel.dll`, `System.Windows.dll`, `System.dll`, `netstandard.dll` |
| `System.Windows.Markup` | `System.ObjectModel.dll`, `System.dll`, `WindowsBase.dll` |
| `System.Xml` | `System.Data.Common.dll`, `System.Data.dll`, `System.Private.DataContractSerialization.dll`, `System.Private.Xml.dll`, `System.Runtime.Serialization.Xml.dll`, `System.Runtime.Serialization.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.XPath.dll`, `System.Xml.XmlDocument.dll`, `System.Xml.dll`, `netstandard.dll` |
| `System.Xml.Linq` | `System.Private.Xml.Linq.dll`, `System.Xml.Linq.dll`, `System.Xml.XDocument.dll`, `netstandard.dll` |
| `System.Xml.Resolvers` | `System.Private.Xml.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.dll`, `netstandard.dll` |
| `System.Xml.Schema` | `System.Private.Xml.Linq.dll`, `System.Private.Xml.dll`, `System.Xml.Linq.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.XDocument.dll`, `System.Xml.dll`, `netstandard.dll` |
| `System.Xml.Serialization` | `System.Private.Xml.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.Serialization.dll`, `System.Xml.XmlSerializer.dll`, `System.Xml.dll`, `netstandard.dll` |
| `System.Xml.XPath` | `System.Private.Xml.Linq.dll`, `System.Private.Xml.dll`, `System.Xml.Linq.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.XPath.XDocument.dll`, `System.Xml.XPath.dll`, `System.Xml.dll`, `netstandard.dll` |
| `System.Xml.Xsl` | `System.Private.Xml.dll`, `System.Xml.ReaderWriter.dll`, `System.Xml.dll`, `netstandard.dll` |
| `System.Xml.Xsl.Runtime` | `System.Private.Xml.dll` |
