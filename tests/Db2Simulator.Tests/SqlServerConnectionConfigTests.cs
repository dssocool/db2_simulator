using Db2Simulator.Config;

namespace Db2Simulator.Tests;

public sealed class SqlServerConnectionConfigTests
{
    [Fact]
    public void IsConfigured_AllowsNamedInstanceWithoutPort()
    {
        var config = new SqlServerConnectionConfig
        {
            Host = @"localhost\SQLEXPRESS",
            Database = "master",
            User = "dev_user",
        };

        Assert.True(config.IsConfigured);
    }

    [Fact]
    public void DataSource_NamedInstance_UsesHostWithoutPortSuffix()
    {
        var config = new SqlServerConnectionConfig
        {
            Host = @"localhost\SQLEXPRESS",
            Port = 0,
        };

        Assert.Equal(@"localhost\SQLEXPRESS", config.DataSource);
    }

    [Fact]
    public void DataSource_ExplicitPort_AppendsPortSuffix()
    {
        var config = new SqlServerConnectionConfig
        {
            Host = "127.0.0.1",
            Port = 1433,
        };

        Assert.Equal("127.0.0.1,1433", config.DataSource);
    }

    [Fact]
    public void DataSource_DynamicPort_AppendsKnownPort()
    {
        var config = new SqlServerConnectionConfig
        {
            Host = @"localhost\SQLEXPRESS",
            Port = 51433,
        };

        Assert.Equal(@"localhost\SQLEXPRESS,51433", config.DataSource);
    }
}
