# Available runtime assemblies

Inventory from the published .NET 11 RC2 build, using SDK
`11.0.100-rc.2.26504.105`. This list is a build snapshot and can change when
the runtime or project dependencies change.

The worker passes every `coreAssembly` and `assembly` asset in the runtime boot
configuration to Roslyn as a metadata reference. These libraries are already
available to C# submissions; use their namespaces without a separate load step.
There is no runtime NuGet/package installer yet.

An assembly being present does **not** mean every API in it works in a browser.
Some DLLs are compatibility facades, and OS-dependent APIs may throw
`PlatformNotSupportedException`. This inventory is not a tested API compatibility
matrix. In particular, the presence of Windows/registry/process/watcher assemblies
does not provide those native facilities in WebAssembly.

This build contains **185 assembly references**, including **164 `System.*` assemblies**.

## System assemblies

- `System.AppContext.dll`
- `System.Buffers.dll`
- `System.Collections.Concurrent.dll`
- `System.Collections.Immutable.dll`
- `System.Collections.NonGeneric.dll`
- `System.Collections.Specialized.dll`
- `System.Collections.dll`
- `System.ComponentModel.Annotations.dll`
- `System.ComponentModel.DataAnnotations.dll`
- `System.ComponentModel.EventBasedAsync.dll`
- `System.ComponentModel.Primitives.dll`
- `System.ComponentModel.TypeConverter.dll`
- `System.ComponentModel.dll`
- `System.Configuration.dll`
- `System.Console.dll`
- `System.Core.dll`
- `System.Data.Common.dll`
- `System.Data.DataSetExtensions.dll`
- `System.Data.dll`
- `System.Diagnostics.Contracts.dll`
- `System.Diagnostics.Debug.dll`
- `System.Diagnostics.DiagnosticSource.dll`
- `System.Diagnostics.FileVersionInfo.dll`
- `System.Diagnostics.Process.dll`
- `System.Diagnostics.StackTrace.dll`
- `System.Diagnostics.TextWriterTraceListener.dll`
- `System.Diagnostics.Tools.dll`
- `System.Diagnostics.TraceSource.dll`
- `System.Diagnostics.Tracing.dll`
- `System.Drawing.Primitives.dll`
- `System.Drawing.dll`
- `System.Dynamic.Runtime.dll`
- `System.Formats.Asn1.dll`
- `System.Formats.Tar.dll`
- `System.Globalization.Calendars.dll`
- `System.Globalization.Extensions.dll`
- `System.Globalization.dll`
- `System.IO.Compression.Brotli.dll`
- `System.IO.Compression.FileSystem.dll`
- `System.IO.Compression.ZipFile.dll`
- `System.IO.Compression.dll`
- `System.IO.FileSystem.AccessControl.dll`
- `System.IO.FileSystem.DriveInfo.dll`
- `System.IO.FileSystem.Primitives.dll`
- `System.IO.FileSystem.Watcher.dll`
- `System.IO.FileSystem.dll`
- `System.IO.IsolatedStorage.dll`
- `System.IO.MemoryMappedFiles.dll`
- `System.IO.Pipelines.dll`
- `System.IO.Pipes.AccessControl.dll`
- `System.IO.Pipes.dll`
- `System.IO.UnmanagedMemoryStream.dll`
- `System.IO.dll`
- `System.Linq.AsyncEnumerable.dll`
- `System.Linq.Expressions.dll`
- `System.Linq.Parallel.dll`
- `System.Linq.Queryable.dll`
- `System.Linq.dll`
- `System.Memory.dll`
- `System.Net.Http.Json.dll`
- `System.Net.Http.dll`
- `System.Net.HttpListener.dll`
- `System.Net.Mail.dll`
- `System.Net.NameResolution.dll`
- `System.Net.NetworkInformation.dll`
- `System.Net.Ping.dll`
- `System.Net.Primitives.dll`
- `System.Net.Quic.dll`
- `System.Net.Requests.dll`
- `System.Net.Security.dll`
- `System.Net.ServerSentEvents.dll`
- `System.Net.ServicePoint.dll`
- `System.Net.Sockets.dll`
- `System.Net.WebClient.dll`
- `System.Net.WebHeaderCollection.dll`
- `System.Net.WebProxy.dll`
- `System.Net.WebSockets.Client.dll`
- `System.Net.WebSockets.dll`
- `System.Net.dll`
- `System.Numerics.Vectors.dll`
- `System.Numerics.dll`
- `System.ObjectModel.dll`
- `System.Private.CoreLib.dll`
- `System.Private.DataContractSerialization.dll`
- `System.Private.Uri.dll`
- `System.Private.Xml.Linq.dll`
- `System.Private.Xml.dll`
- `System.Reflection.DispatchProxy.dll`
- `System.Reflection.Emit.ILGeneration.dll`
- `System.Reflection.Emit.Lightweight.dll`
- `System.Reflection.Emit.dll`
- `System.Reflection.Extensions.dll`
- `System.Reflection.Metadata.dll`
- `System.Reflection.Primitives.dll`
- `System.Reflection.TypeExtensions.dll`
- `System.Reflection.dll`
- `System.Resources.Reader.dll`
- `System.Resources.ResourceManager.dll`
- `System.Resources.Writer.dll`
- `System.Runtime.CompilerServices.Unsafe.dll`
- `System.Runtime.CompilerServices.VisualC.dll`
- `System.Runtime.Extensions.dll`
- `System.Runtime.Handles.dll`
- `System.Runtime.InteropServices.JavaScript.dll`
- `System.Runtime.InteropServices.RuntimeInformation.dll`
- `System.Runtime.InteropServices.dll`
- `System.Runtime.Intrinsics.dll`
- `System.Runtime.Loader.dll`
- `System.Runtime.Numerics.dll`
- `System.Runtime.Serialization.Formatters.dll`
- `System.Runtime.Serialization.Json.dll`
- `System.Runtime.Serialization.Primitives.dll`
- `System.Runtime.Serialization.Xml.dll`
- `System.Runtime.Serialization.dll`
- `System.Runtime.dll`
- `System.Security.AccessControl.dll`
- `System.Security.Claims.dll`
- `System.Security.Cryptography.Algorithms.dll`
- `System.Security.Cryptography.Cng.dll`
- `System.Security.Cryptography.Csp.dll`
- `System.Security.Cryptography.Encoding.dll`
- `System.Security.Cryptography.OpenSsl.dll`
- `System.Security.Cryptography.Primitives.dll`
- `System.Security.Cryptography.X509Certificates.dll`
- `System.Security.Cryptography.dll`
- `System.Security.Principal.Windows.dll`
- `System.Security.Principal.dll`
- `System.Security.SecureString.dll`
- `System.Security.dll`
- `System.ServiceModel.Web.dll`
- `System.ServiceProcess.dll`
- `System.Text.Encoding.CodePages.dll`
- `System.Text.Encoding.Extensions.dll`
- `System.Text.Encoding.dll`
- `System.Text.Encodings.Web.dll`
- `System.Text.Json.dll`
- `System.Text.RegularExpressions.dll`
- `System.Threading.AccessControl.dll`
- `System.Threading.Channels.dll`
- `System.Threading.Overlapped.dll`
- `System.Threading.Tasks.Dataflow.dll`
- `System.Threading.Tasks.Extensions.dll`
- `System.Threading.Tasks.Parallel.dll`
- `System.Threading.Tasks.dll`
- `System.Threading.Thread.dll`
- `System.Threading.ThreadPool.dll`
- `System.Threading.Timer.dll`
- `System.Threading.dll`
- `System.Transactions.Local.dll`
- `System.Transactions.dll`
- `System.ValueTuple.dll`
- `System.Web.HttpUtility.dll`
- `System.Web.dll`
- `System.Windows.dll`
- `System.Xml.Linq.dll`
- `System.Xml.ReaderWriter.dll`
- `System.Xml.Serialization.dll`
- `System.Xml.XDocument.dll`
- `System.Xml.XPath.XDocument.dll`
- `System.Xml.XPath.dll`
- `System.Xml.XmlDocument.dll`
- `System.Xml.XmlSerializer.dll`
- `System.Xml.dll`
- `System.dll`

## Other runtime and application assemblies

- `DotnetDide.Browser.dll`
- `DotnetDide.Core.dll`
- `Microsoft.CSharp.dll`
- `Microsoft.CodeAnalysis.CSharp.dll`
- `Microsoft.CodeAnalysis.dll`
- `Microsoft.Extensions.Caching.Abstractions.dll`
- `Microsoft.Extensions.Configuration.Abstractions.dll`
- `Microsoft.Extensions.DependencyInjection.Abstractions.dll`
- `Microsoft.Extensions.Diagnostics.Abstractions.dll`
- `Microsoft.Extensions.FileProviders.Abstractions.dll`
- `Microsoft.Extensions.Hosting.Abstractions.dll`
- `Microsoft.Extensions.Logging.Abstractions.dll`
- `Microsoft.Extensions.Options.dll`
- `Microsoft.Extensions.Primitives.dll`
- `Microsoft.VisualBasic.Core.dll`
- `Microsoft.VisualBasic.dll`
- `Microsoft.Win32.Primitives.dll`
- `Microsoft.Win32.Registry.dll`
- `WindowsBase.dll`
- `mscorlib.dll`
- `netstandard.dll`
