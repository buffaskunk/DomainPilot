using System.ComponentModel;
using System.Runtime.InteropServices;
using DomainPilot.App;
using DomainPilot.Core;

namespace DomainPilot.Infrastructure;

/// <summary>
/// Uses one cache-friendly Windows DC Locator call. This provider is prepared for a future DryRun workflow
/// but is intentionally not connected to the current desktop UI.
/// </summary>
public sealed class WindowsDomainControllerDiscovery : IReadOnlyDomainControllerDiscovery
{
    private const uint DirectoryServiceRequired = 0x00000010;
    private const uint IpRequired = 0x00000200;
    private const uint TryNextClosestSite = 0x00040000;
    private const uint ReturnDnsName = 0x40000000;
    private const uint GlobalCatalogFlag = 0x00000004;
    private const uint KeyDistributionCenterFlag = 0x00000020;
    private const uint WritableFlag = 0x00000100;

    private readonly DomainControllerDiscoveryPolicy _policy = new();

    public DomainControllerDiscoveryResult Discover(DomainControllerDiscoveryRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Domain-controller discovery requires Windows.");
        }

        var issues = _policy.Validate(request);
        if (issues.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", issues), nameof(request));
        }

        var flags = DirectoryServiceRequired | IpRequired | ReturnDnsName;
        if (request.PreferLocalSite)
        {
            flags |= TryNextClosestSite;
        }

        var result = DsGetDcName(
            null,
            request.DomainName,
            IntPtr.Zero,
            null,
            flags,
            out var infoBuffer);
        if (result != 0)
        {
            if (infoBuffer != IntPtr.Zero)
            {
                NetApiBufferFree(infoBuffer);
            }

            throw new Win32Exception(result, "Windows DC Locator could not select a domain controller.");
        }

        try
        {
            var info = Marshal.PtrToStructure<DomainControllerInfo>(infoBuffer);
            return new DomainControllerDiscoveryResult(
                info.DomainName ?? request.DomainName,
                info.DnsForestName ?? string.Empty,
                (info.DomainControllerName ?? string.Empty).TrimStart('\\'),
                info.DomainControllerAddress ?? string.Empty,
                info.DomainControllerSiteName ?? string.Empty,
                info.ClientSiteName ?? string.Empty,
                (info.Flags & GlobalCatalogFlag) != 0,
                (info.Flags & KeyDistributionCenterFlag) != 0,
                (info.Flags & WritableFlag) != 0,
                DateTimeOffset.Now,
                "Windows DsGetDcName (cached, no force rediscovery)");
        }
        finally
        {
            if (infoBuffer != IntPtr.Zero)
            {
                NetApiBufferFree(infoBuffer);
            }
        }
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int DsGetDcName(
        string? computerName,
        string domainName,
        IntPtr domainGuid,
        string? siteName,
        uint flags,
        out IntPtr domainControllerInfo);

    [DllImport("Netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DomainControllerInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DomainControllerName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DomainControllerAddress;

        public uint DomainControllerAddressType;
        public Guid DomainGuid;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DomainName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DnsForestName;

        public uint Flags;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DomainControllerSiteName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ClientSiteName;
    }
}
