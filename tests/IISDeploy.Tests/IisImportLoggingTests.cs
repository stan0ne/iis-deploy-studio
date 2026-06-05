using IISDeploy.Core.Models.Enums;
using IISDeploy.Infrastructure.Iis;

namespace IISDeploy.Tests;

public class IisImportLoggingTests
{
    [Fact]
    public void BuildConflictStrategyLogMessage_Skip_Includes_Skipped_Text()
    {
        var msg = IisImportService.BuildConflictStrategyLogMessage(
            "App pool", "Pool1", null, ConflictResolutionStrategy.Skip);
        Assert.Contains("App pool", msg);
        Assert.Contains("Pool1", msg);
        Assert.Contains("skipped", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildConflictStrategyLogMessage_Rename_Includes_NewName()
    {
        var msg = IisImportService.BuildConflictStrategyLogMessage(
            "App pool", "Pool1", "Pool1_Imported_2024", ConflictResolutionStrategy.Rename);
        Assert.Contains("renamed", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pool1_Imported_2024", msg);
    }

    [Fact]
    public void BuildConflictStrategyLogMessage_Clone_Includes_NewName_And_Cloned()
    {
        var msg = IisImportService.BuildConflictStrategyLogMessage(
            "Site", "Site1", "Site1_Clone_2024", ConflictResolutionStrategy.Clone);
        Assert.Contains("cloned", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Site1_Clone_2024", msg);
    }

    [Fact]
    public void BuildConflictStrategyLogMessage_Overwrite_Includes_Overwritten()
    {
        var msg = IisImportService.BuildConflictStrategyLogMessage(
            "Site", "Site1", null, ConflictResolutionStrategy.Overwrite);
        Assert.Contains("overwritten", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildConflictStrategyLogMessage_ChangeBinding_Includes_Changed()
    {
        var msg = IisImportService.BuildConflictStrategyLogMessage(
            "Site", "Site1", null, ConflictResolutionStrategy.ChangeBinding);
        Assert.Contains("binding changed", msg, StringComparison.OrdinalIgnoreCase);
    }
}
