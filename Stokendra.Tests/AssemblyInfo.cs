using Xunit;

// Disable parallel execution to prevent static singleton (AppServices) race conditions during test runs
[assembly: CollectionBehavior(DisableTestParallelization = true)]
