using Xunit;

// Each test starts Chrome and local HTTP servers. Serialize these resource-heavy
// fixtures so concurrent browser startup cannot starve their request handlers.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
