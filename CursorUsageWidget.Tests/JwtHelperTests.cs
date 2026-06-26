using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class JwtHelperTests
{
    [Fact]
    public void GetSessionUserId_extracts_suffix_after_pipe()
    {
        const string jwt =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9."
            + "eyJzdWIiOiJnb29nbGUtb2F1dGgyfHVzZXJfMDEyMyJ9."
            + "signature";

        Assert.Equal("user_0123", JwtHelper.GetSessionUserId(jwt));
    }
}
