using System;
using System.IO;
using DotnetDide;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

Directory.CreateDirectory("site");
File.WriteAllText("site/index.html", """
<!doctype html><html><head><title>Wire notebook</title><link rel="stylesheet" href="style.css"></head>
<body><h1>My C# notebook</h1><p>ASP.NET Core is running inside this browser.</p>
<button id="hello">Call C#</button><output id="answer">Ready</output>
<form id="note"><label>Your note <input name="text" value="Hello from the iframe"></label><button>Save note</button></form>
<p id="saved"></p><script src="app.js"></script></body></html>
""");
File.WriteAllText("site/style.css", "body{font:18px system-ui;background:#161a26;color:#eef1ff;margin:0;padding:32px}h1{color:#9cd5ff}button,input{font:inherit;padding:8px;margin:8px;border-radius:8px}output{display:block;margin:16px}form{margin-top:24px}");
File.WriteAllText("site/app.js", """
document.querySelector('#hello').onclick=async()=>{
  const response=await fetch('/api/hello');const data=await response.json();
  document.querySelector('#answer').textContent=data.message;
};
document.querySelector('#note').onsubmit=async event=>{
  event.preventDefault();const input=document.querySelector('input[name=text]');
  const response=await fetch('/api/note',{method:'POST',body:input.value});
  document.querySelector('#saved').textContent=await response.text();
};
""");
var app = Wire.CreateApplication();
app.Use(async (context, next) => { context.Response.Headers["X-Served-By"] = "DotnetDide.Wire"; await next(context); });
app.UseRouting();
app.UseEndpoints(routes => {
    routes.MapGet("/api/hello", async context => {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new {
            message = "Hello from C# through IServer", version = Environment.Version.ToString()
        }));
    });
    routes.MapPost("/api/note", async context => {
        using var reader = new StreamReader(context.Request.Body);
        var note = await reader.ReadToEndAsync(context.RequestAborted);
        File.WriteAllText("note.txt", note);
        await context.Response.WriteAsync("Saved note.txt: " + note);
    });
});
// Returns immediately. Do not call app.Run(), WaitForShutdown or an infinite loop.
var server = await Wire.ListenAsync(8080, app, "site");
Console.WriteLine("Site ready on virtual port 8080. Open Sites to interact with it.");
