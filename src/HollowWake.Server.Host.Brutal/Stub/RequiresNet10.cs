// Compiled only when the .NET 10 SDK is absent. See the csproj for the switch.
Console.Error.WriteLine("hollowwake-server-brutal requires the .NET 10 SDK (BRUTAL packages target net10.0).");
Console.Error.WriteLine("Install it (winget install Microsoft.DotNet.SDK.10) and rebuild, or run hollowwake-server, the pure .NET 8 host.");
return 2;
