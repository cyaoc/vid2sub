using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Vid2Sub.Infrastructure.Workflow;
using Vid2Sub.WorkflowMcp;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<WorkbookInspector>();
builder.Services.AddSingleton<GlossaryReader>();
builder.Services.AddSingleton<Vid2SubProcessRunner>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WorkflowTools>();

await builder.Build().RunAsync();
