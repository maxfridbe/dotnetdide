import type {Runspace, SubmissionResult} from "../dotnetdide.js";
export interface Example {
  id: string; title: string; category: string; description: string; expected: string;
  steps: string[]; expectsFailure?: boolean;
  verify(results: SubmissionResult[], runtime: Runspace): Promise<boolean> | boolean;
}
// The same examples power the editor and real browser checks. No mocked results.
const siteSource = await (await fetch(new URL('../examples/site.cs', import.meta.url))).text();
export const examples: Example[] = [
  {
    id: 'files', title: 'Create a file', category: 'SYSTEM.IO',
    description: 'Write a text file with System.IO, then download it from the workspace below.',
    expected: 'Returns 42 and creates outputs/answer.txt.',
    steps: [`int answer = 40;
Directory.CreateDirectory("outputs");
File.WriteAllText("outputs/answer.txt", (answer + 2).ToString());
Console.WriteLine("Saved outputs/answer.txt");
answer + 2`],
    verify: async (results, runtime) => results[0]!.value === '42' && await runtime.FS.readFile('outputs/answer.txt', { encoding: 'utf8' }) === '42',
  },
  {
    id: 'state', title: 'Keep variables between runs', category: 'RUNSPACE',
    description: 'Run step 1, then step 2. The second submission uses a variable and function from the first.',
    expected: 'Step 1 returns 40. Step 2 returns 42.',
    steps: ['int counter = 40;\nint Add(int left, int right) => left + right;\ncounter', 'counter = Add(counter, 2);\ncounter'],
    verify: async results => results[0]!.value === '40' && results[1]!.value === '42',
  },
  {
    id: 'linq', title: 'Query data with LINQ', category: 'COLLECTIONS',
    description: 'Filter and transform a sequence using the standard .NET libraries.',
    expected: 'Returns 4, 16, 36, 64, 100.',
    steps: [`var squares = Enumerable.Range(1, 10)
    .Where(number => number % 2 == 0)
    .Select(number => number * number);
string.Join(", ", squares)`],
    verify: async results => results[0]!.value === '4, 16, 36, 64, 100',
  },
  {
    id: 'json', title: 'Read and write JSON', category: 'SYSTEM.TEXT.JSON',
    description: 'Serialize data to the virtual filesystem, read it back and inspect a property.',
    expected: 'Returns dotnetdide and creates outputs/info.json.',
    steps: [`using System.Text.Json;
Directory.CreateDirectory("outputs");
var info = new { name = "dotnetdide", answer = 42 };
File.WriteAllText("outputs/info.json", JsonSerializer.Serialize(info));
string projectName;
using (var document = JsonDocument.Parse(File.ReadAllText("outputs/info.json")))
{
    projectName = document.RootElement.GetProperty("name").GetString();
}
projectName`],
    verify: async (results, runtime) => results[0]!.value === 'dotnetdide' && JSON.parse(await runtime.FS.readFile('outputs/info.json', { encoding: 'utf8' })).answer === 42,
  },
  {
    id: 'zip', title: 'Build a ZIP archive', category: 'COMPRESSION',
    description: 'Create an archive in memory, save it as a binary file and read back its entry.',
    expected: 'Returns Hello from .NET! and creates outputs/example.zip.',
    steps: [`using System.IO.Compression;
Directory.CreateDirectory("outputs");
File.Delete("outputs/example.zip");
using (var archive = ZipFile.Open("outputs/example.zip", ZipArchiveMode.Create))
{
    using var writer = new StreamWriter(archive.CreateEntry("hello.txt").Open());
    writer.Write("Hello from .NET!");
}
string archiveText;
using (var savedArchive = ZipFile.OpenRead("outputs/example.zip"))
using (var reader = new StreamReader(savedArchive.GetEntry("hello.txt").Open()))
{
    archiveText = reader.ReadToEnd();
}
archiveText`],
    verify: async (results, runtime) => results[0]!.value === 'Hello from .NET!' && (await runtime.FS.readFile('outputs/example.zip'))[0] === 0x50,
  },
  {
    id: 'async', title: 'Await asynchronous code', category: 'TASKS',
    description: 'Await a task inside a submission while the browser stays responsive.',
    expected: 'Prints Starting… and Finished., then returns 42.',
    steps: ['Console.WriteLine("Starting…");\nawait Task.Delay(250);\nConsole.WriteLine("Finished.");\n6 * 7'],
    verify: async results => results[0]!.value === '42' && results[0]!.stdout.includes('Finished.'),
  },
  {
    id: 'diagnostics', title: 'Inspect compiler diagnostics', category: 'ROSLYN',
    description: 'This deliberately invalid submission demonstrates a compiler error with a source location.',
    expected: 'Compilation fails with CS0103; the runspace remains usable.',
    steps: ['thisVariableDoesNotExist + 1'], expectsFailure: true,
    verify: async results => !results[0]!.success && results[0]!.diagnostics.some(d => d.id === 'CS0103' && d.line === 1),
  },
  {
    id: 'errors', title: 'Capture runtime errors', category: 'EXCEPTIONS',
    description: 'Output written before an exception is preserved along with the exception details.',
    expected: 'Captures stderr and reports an intentional InvalidOperationException.',
    steps: ['Console.Error.WriteLine("Before the exception");\nthrow new InvalidOperationException("Intentional demo error");'], expectsFailure: true,
    verify: async results => !results[0]!.success && results[0]!.error?.message === 'Intentional demo error' && results[0]!.stderr.includes('Before the exception'),
  },
  {
    id: 'build-file', title: 'Compile and run a C# file', category: 'DOTNET.BUILD / RUN',
    description: 'Save a complete C# program, compile downloadable DLL/PDB files, then run its async entry point with arguments.',
    expected: 'Prints Hello, browser! and returns True. Download hello.dll and hello.pdb below.',
    steps: [`File.WriteAllText("hello.cs", """
await Task.Delay(10);
Console.WriteLine($"Hello, {args[0]}!");
return 0;
""");
var build = Dotnet.Build("hello.cs");
var program = await Dotnet.Run("hello.cs", new[] { "browser" });
program.Success`],
    verify: async (results, runtime) => results[0]!.value === 'True' && results[0]!.stdout.includes('Hello, browser!') &&
      (await runtime.FS.readFile('bin/hello.dll')).length > 0 && (await runtime.FS.readFile('bin/hello.pdb')).length > 0,
  },
  {
    id: 'build-project', title: 'Build a multi-file project', category: 'CSPROJ',
    description: 'Create an SDK-style project. Dotnet.Build() discovers it in the working directory; Dotnet.Run executes the program.',
    expected: 'Prints 42, returns True and creates app/result.txt plus app/bin/Demo.dll and its symbols.',
    steps: [`Directory.CreateDirectory("app");
File.WriteAllText("app/Demo.csproj", """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""");
File.WriteAllText("app/Helper.cs", "public static class Helper { public static int Value => 42; }");
File.WriteAllText("app/Program.cs", """
Console.WriteLine(Helper.Value);
File.WriteAllText("result.txt", Helper.Value.ToString());
""");
Directory.SetCurrentDirectory("app");
var build = Dotnet.Build();
var program = await Dotnet.Run("Demo.csproj");
program.Success`],
    verify: async (results, runtime) => results[0]!.value === 'True' && results[0]!.stdout.includes('Build succeeded') &&
      await runtime.FS.readFile('app/result.txt', { encoding: 'utf8' }) === '42' && (await runtime.FS.readFile('app/bin/Demo.dll')).length > 0,
  },
  {
    id: 'site', title: 'Host an interactive site', category: 'ASP.NET CORE · ISERVER',
    description: 'Enable Interactive sites below, run the code, then open the virtual port. The page calls C# GET/POST routes and saves a note into workspace files.',
    expected: 'Creates site/index.html, CSS and JavaScript; registers port 8080. No network permission or real socket is needed.',
    steps: [siteSource],
    verify: async (_results, runtime) => (await runtime.listSites()).some(site => site.port === 8080) &&
      (await runtime.requestSite({port:8080,method:'GET',path:'/api/hello'})).status === 200,
  },
];
