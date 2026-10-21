using Xunit;

// Every database-backed integration test class in this assembly shares one physical
// `stasis_test` Postgres database via TestWebApplicationFactory (see
// TestWebApplicationFactory.cs). TestWebApplicationFactory.ResetDatabaseAsync() performs a
// full-schema `TRUNCATE ... CASCADE`. xUnit v2 runs distinct test classes in parallel by
// default (though test methods within one class run serially), so without this attribute,
// concurrently-running classes would race: one class's reset could truncate tables out from
// under another class's test that's mid-execution.
//
// Disabling parallelization here makes every test class in STASIS.Tests run serially,
// eliminating that race. Any new test class added by later tasks that uses
// TestWebApplicationFactory (via IClassFixture<TestWebApplicationFactory>) relies on this
// attribute for correctness — do not remove it or scope it away (e.g. via a
// [CollectionDefinition] that some classes opt out of) without first giving each such class
// its own isolated database.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
