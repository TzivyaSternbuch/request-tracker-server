namespace RequestTracker.IntegrationTests.TestHost;

// One shared host for all test classes; from task 05 on it also means one SQL Server container.
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
