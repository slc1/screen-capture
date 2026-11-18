namespace ScreenCapture.Platform;

public interface IAutostartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}
