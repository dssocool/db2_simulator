using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: TestCollectionOrderer(
    SizzlingDb.SqlServer.Tests.NumberedCollectionOrderer.TypeName,
    SizzlingDb.SqlServer.Tests.TestOrdering.AssemblyName)]

namespace SizzlingDb.SqlServer.Tests;

internal static class TestOrdering
{
    public const string AssemblyName = "SizzlingDb.SqlServer.Tests";
}

public sealed class NumberedCollectionOrderer : ITestCollectionOrderer
{
    public const string TypeName = "SizzlingDb.SqlServer.Tests.NumberedCollectionOrderer";

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase);
}

public sealed class NumberedTestCaseOrderer : ITestCaseOrderer
{
    public const string TypeName = "SizzlingDb.SqlServer.Tests.NumberedTestCaseOrderer";

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase =>
        testCases.OrderBy(tc => tc.TestMethod.Method.Name, StringComparer.OrdinalIgnoreCase);
}
