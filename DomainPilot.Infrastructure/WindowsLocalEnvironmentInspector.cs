using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using DomainPilot.App;
using DomainPilot.Core;

namespace DomainPilot.Infrastructure;

/// <summary>
/// Reads workstation state through local Windows APIs and known RSAT file locations.
/// This implementation does not resolve DNS, contact a domain controller, or start PowerShell.
/// </summary>
public sealed class WindowsLocalEnvironmentInspector : ILocalEnvironmentInspector
{
    public LocalEnvironmentSnapshot Inspect()
    {
        var (isDomainJoined, joinedDomain) = ReadLocalDomainJoin();
        var modulePath = FindActiveDirectoryModule();
        var dnsSuffix = ReadLocalDnsSuffix();
        var identityDomain = OperatingSystem.IsWindows() ? Environment.UserDomainName : string.Empty;
        var identity = string.IsNullOrWhiteSpace(identityDomain)
            ? Environment.UserName
            : $"{identityDomain}\\{Environment.UserName}";

        return new LocalEnvironmentSnapshot(
            DateTimeOffset.Now,
            RuntimeInformation.OSDescription,
            OperatingSystem.IsWindows(),
            Environment.MachineName,
            identity,
            isDomainJoined,
            joinedDomain,
            dnsSuffix,
            modulePath is not null,
            modulePath is null ? "ActiveDirectory.psd1 was not found in standard RSAT locations." : modulePath,
            NetworkActivityPerformed: false);
    }

    private static string ReadLocalDnsSuffix()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties().DomainName;
        }
        catch (NetworkInformationException)
        {
            return string.Empty;
        }
    }

    private static string? FindActiveDirectoryModule()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidateDirectories = new[]
        {
            Path.Combine(windowsDirectory, "System32", "WindowsPowerShell", "v1.0", "Modules", "ActiveDirectory"),
            Path.Combine(programFiles, "WindowsPowerShell", "Modules", "ActiveDirectory")
        };

        foreach (var directory in candidateDirectories.Where(Directory.Exists))
        {
            try
            {
                var manifest = Directory
                    .EnumerateFiles(directory, "ActiveDirectory.psd1", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (manifest is not null)
                {
                    return manifest;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // A protected custom module directory is reported as unavailable instead of failing the workflow.
            }
            catch (IOException)
            {
                // A transient local file-system error should become a readiness warning, not an application crash.
            }
        }

        return null;
    }

    private static (bool IsDomainJoined, string DomainName) ReadLocalDomainJoin()
    {
        if (!OperatingSystem.IsWindows())
        {
            return (false, string.Empty);
        }

        var result = NetGetJoinInformation(null, out var nameBuffer, out var joinStatus);
        if (result != 0)
        {
            if (nameBuffer != IntPtr.Zero)
            {
                NetApiBufferFree(nameBuffer);
            }

            return (false, string.Empty);
        }

        try
        {
            var joinedName = Marshal.PtrToStringUni(nameBuffer) ?? string.Empty;
            return (joinStatus == NetJoinStatus.DomainName, joinedName);
        }
        finally
        {
            if (nameBuffer != IntPtr.Zero)
            {
                NetApiBufferFree(nameBuffer);
            }
        }
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetGetJoinInformation(
        string? server,
        out IntPtr nameBuffer,
        out NetJoinStatus bufferType);

    [DllImport("Netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);

    private enum NetJoinStatus
    {
        Unknown,
        Unjoined,
        WorkgroupName,
        DomainName
    }
}
