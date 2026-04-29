using System.Security.Principal;

namespace BAKeySmith.App.Services;

public interface IElevationStatusService
{
    bool IsElevated { get; }
}

public sealed class WindowsElevationStatusService : IElevationStatusService
{
    public bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
