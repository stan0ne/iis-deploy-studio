using IISDeploy.Core.Models;
using IISDeploy.Infrastructure.Iis;

namespace IISDeploy.Tests;

public class IisImportPostValidationTests
{
    [Fact]
    public void BuildPostValidationEntries_All_Started_Returns_Empty()
    {
        var poolStates = new Dictionary<string, string> { ["Pool1"] = "Started", ["Pool2"] = "Started" };
        var siteStates = new Dictionary<string, string> { ["Site1"] = "present" };

        var entries = IisImportService.BuildPostValidationEntries(
            ["Pool1", "Pool2"], ["Site1"], poolStates, siteStates);

        Assert.Empty(entries);
    }

    [Fact]
    public void BuildPostValidationEntries_Missing_Pool_Creates_Entry()
    {
        var poolStates = new Dictionary<string, string> { ["Pool1"] = "Started" };
        var siteStates = new Dictionary<string, string>();

        var entries = IisImportService.BuildPostValidationEntries(
            ["Pool1", "PoolMissing"], [], poolStates, siteStates);

        Assert.Single(entries);
        Assert.Equal("PostValidation", entries[0].Category);
        Assert.Equal("PoolMissing", entries[0].Item);
        Assert.False(entries[0].Success);
        Assert.Contains("not found", entries[0].Message);
    }

    [Fact]
    public void BuildPostValidationEntries_Stopped_Pool_Creates_Entry()
    {
        var poolStates = new Dictionary<string, string> { ["Pool1"] = "Stopped" };
        var siteStates = new Dictionary<string, string>();

        var entries = IisImportService.BuildPostValidationEntries(
            ["Pool1"], [], poolStates, siteStates);

        Assert.Single(entries);
        Assert.False(entries[0].Success);
        Assert.Contains("Stopped", entries[0].Message);
        Assert.Contains("expected Started", entries[0].Message);
    }

    [Fact]
    public void BuildPostValidationEntries_Missing_Site_Creates_Entry()
    {
        var poolStates = new Dictionary<string, string>();
        var siteStates = new Dictionary<string, string> { ["Site1"] = "present" };

        var entries = IisImportService.BuildPostValidationEntries(
            [], ["Site1", "SiteMissing"], poolStates, siteStates);

        Assert.Single(entries);
        Assert.Equal("SiteMissing", entries[0].Item);
        Assert.False(entries[0].Success);
        Assert.Contains("not found", entries[0].Message);
    }

    [Fact]
    public void BuildPostValidationEntries_Mixed_States_Returns_All_Issues()
    {
        var poolStates = new Dictionary<string, string>
        {
            ["Good"] = "Started",
            ["Bad"] = "Stopped"
        };
        var siteStates = new Dictionary<string, string>
        {
            ["GoodSite"] = "present"
        };

        var entries = IisImportService.BuildPostValidationEntries(
            ["Good", "Bad"], ["GoodSite", "MissingSite"], poolStates, siteStates);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Item == "Bad" && !e.Success);
        Assert.Contains(entries, e => e.Item == "MissingSite" && !e.Success);
    }
}
