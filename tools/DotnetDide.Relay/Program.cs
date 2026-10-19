using DotnetDide.Relay;

await RelayHost.Create(RelayOptions.FromEnvironment(), args).RunAsync();
