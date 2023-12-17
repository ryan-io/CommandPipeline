# rio-logging

A Serilog wrapper for .NET environments.

```csharp
// include the riolog namespace
using riolog;

// =======> Using riolog
// invoke the static factory method, SetupAndStart()
// pass in a bit flag to define what outputs the logger should use
var log = InternalLogFactory.SetupAndStart(Output.All);


// example usage
log.Debug("Debug	");
log.Information("Information	");
log.Error("Error");

// manually invoke log.CloseAndFlush() at the end of your program's lifetime
// this method will invoke Dispose() on the logger, which will flush any pending log messages
log.CloseAndFlush();

// =======> 
// include the riolog namespace
using riolog;
using Microsoft.Extensions.Logging;

// =======> Using riolog as Microsoft.Extensions.Logging.ILogger
// invoke the static factory method, SetupAndStart()
// pass in a bit flag to define what outputs the logger should use
// then invoke the AsLogger<T>() extension method to convert the Serilog.ILogger to a Microsoft.Extensions.Logging.ILogger
//		replace type 'Program' with an appropriate type
var log = InternalLogFactory.SetupAndStart(Output.All).AsLogger<Program>();


// example usage, log in same manner, but with the ILogger API 
log.LogDebug("Debug	");
log.LogInformation("Information	");
log.LogError("Error");

// manually invoke log.CloseAndFlush() at the end of your program's lifetime
// this method will invoke Dispose() on the logger, which will flush any pending log messages
log.CloseAndFlush();
```