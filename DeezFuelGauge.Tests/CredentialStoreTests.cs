using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class CredentialStoreTests
{
    [Fact]
    public void Store_and_retrieve_round_trip()
    {
        var id = CredentialStore.Store("test-provider", "secret-value-123");

        try
        {
            var retrieved = CredentialStore.Retrieve(id);
            Assert.Equal("secret-value-123", retrieved);
        }
        finally
        {
            CredentialStore.Delete(id);
        }
    }

    [Fact]
    public void Delete_removes_credential()
    {
        var id = CredentialStore.Store("test-provider", "temporary-secret");
        CredentialStore.Delete(id);
        Assert.Null(CredentialStore.Retrieve(id));
    }
}
