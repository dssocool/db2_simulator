using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: TestCollectionOrderer(
    SizzlingDb.Tests.NumberedCollectionOrderer.TypeName,
    SizzlingDb.Tests.TestOrdering.AssemblyName)]

namespace SizzlingDb.Tests;

internal static class TestOrdering
{
    public const string AssemblyName = "SizzlingDb.Tests";
}

/// <summary>Runs test classes in name order: Test01 → Test02 → Test03.</summary>
public sealed class NumberedCollectionOrderer : ITestCollectionOrderer
{
    public const string TypeName = "SizzlingDb.Tests.NumberedCollectionOrderer";

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Runs test methods within a class in name order: T01_, T02_, ...</summary>
public sealed class NumberedTestCaseOrderer : ITestCaseOrderer
{
    public const string TypeName = "SizzlingDb.Tests.NumberedTestCaseOrderer";

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase =>
        testCases.OrderBy(tc => tc.TestMethod.Method.Name, StringComparer.OrdinalIgnoreCase);
}
