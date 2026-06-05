using IISDeploy.Infrastructure.Iis;

namespace IISDeploy.Tests;

public class IisImportSkipTests
{
    [Fact]
    public void BuildSkipReportEntry_Returns_Entry_With_Skipped_Message()
    {
        var entry = IisImportService.BuildSkipReportEntry("Site", "Site1");

        Assert.Equal("Site", entry.Category);
        Assert.Equal("Site1", entry.Item);
        Assert.True(entry.Success);
        Assert.Equal("Skipped (already exists)", entry.Message);
    }
}
