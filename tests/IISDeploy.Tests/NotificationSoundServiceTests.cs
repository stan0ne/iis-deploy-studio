using IISDeploy.Core.Interfaces;
using IISDeploy.UI.Services;
using Moq;

namespace IISDeploy.Tests;

public class NotificationSoundServiceTests
{
    [Fact]
    public void Default_Success_Sound_FileName_Is_Windows_Background_Wav()
    {
        Assert.Equal("Windows Background.wav", WpfNotificationSoundService.DefaultSuccessSoundFileName);
    }

    [Fact]
    public void Default_Failure_Sound_FileName_Is_Windows_Exclamation_Wav()
    {
        Assert.Equal("Windows Exclamation.wav", WpfNotificationSoundService.DefaultFailureSoundFileName);
    }

    [Fact]
    public void Default_Media_Folder_Is_WINDIR_Media()
    {
        Assert.Equal(@"%WINDIR%\Media", WpfNotificationSoundService.DefaultWindowsMediaFolder);
    }

    [Fact]
    public void PlaySuccess_With_Missing_Sound_File_Logs_Warning_And_Does_Not_Throw()
    {
        var mockLogger = new Mock<ILoggingService>();
        var service = new WpfNotificationSoundService(
            mockLogger.Object,
            successSoundPath: @"C:\NonExistent\Path\Success.wav",
            failureSoundPath: @"C:\NonExistent\Path\Failure.wav");

        service.PlaySuccess();

        mockLogger.Verify(
            l => l.Warning(
                It.IsAny<string>(),
                It.Is<object?[]>(args => args.Length > 0 && Equals(args[0], "success"))),
            Times.AtLeastOnce);
    }

    [Fact]
    public void PlayFailure_With_Missing_Sound_File_Logs_Warning_And_Does_Not_Throw()
    {
        var mockLogger = new Mock<ILoggingService>();
        var service = new WpfNotificationSoundService(
            mockLogger.Object,
            successSoundPath: @"C:\NonExistent\Path\Success.wav",
            failureSoundPath: @"C:\NonExistent\Path\Failure.wav");

        service.PlayFailure();

        mockLogger.Verify(
            l => l.Warning(
                It.IsAny<string>(),
                It.Is<object?[]>(args => args.Length > 0 && Equals(args[0], "failure"))),
            Times.AtLeastOnce);
    }
}
