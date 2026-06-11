using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: TestCollectionOrderer(
    Db2Simulator.Tests.NumberedCollectionOrderer.TypeName,
    Db2Simulator.Tests.TestOrdering.AssemblyName)]

namespace Db2Simulator.Tests;

internal static class TestOrdering
{
    public const string AssemblyName = "Db2Simulator.Tests";
}

/// <summary>Runs test classes in name order: Test01 → Test02 → Test03.</summary>
public sealed class NumberedCollectionOrderer : ITestCollectionOrderer
{
    public const string TypeName = "Db2Simulator.Tests.NumberedCollectionOrderer";

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Runs test methods within a class in name order: T01_, T02_, ...</summary>
public sealed class NumberedTestCaseOrderer : ITestCaseOrderer
{
    public const string TypeName = "Db2Simulator.Tests.NumberedTestCaseOrderer";

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase =>
        testCases.OrderBy(tc => tc.TestMethod.Method.Name, StringComparer.OrdinalIgnoreCase);
}
