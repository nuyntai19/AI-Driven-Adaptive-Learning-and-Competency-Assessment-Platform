using Xunit;

namespace EduTwin.BLL.Tests;

/// <summary>
/// Smoke test proving the test project compiles and xUnit runner works.
/// Required by P01 Definition of Done §15.
/// </summary>
public class SmokeTest
{
    [Fact]
    public void Solution_BuildsAndTestsRun()
    {
        Assert.True(true);
    }
}
