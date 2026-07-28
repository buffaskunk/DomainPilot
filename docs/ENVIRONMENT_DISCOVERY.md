# Environment Readiness and Discovery

DomainPilot separates local inspection from domain discovery so technicians can see what has happened and what has only been proposed.

## Current Milestone

The **Environment** tab provides two actions:

### Run Local Checks

This action reads:

- Windows version and computer name.
- Current Windows identity.
- Local computer domain-join status through the Windows `NetGetJoinInformation` API.
- Locally configured DNS suffix.
- Known local RSAT `ActiveDirectory.psd1` file locations.

It does not resolve DNS, run PowerShell, contact a domain controller, open an event log, enumerate directory objects, or connect to another computer. Results remain in memory unless the operator explicitly exports the audit log.

### Preview Domain Discovery

This action creates a non-executable plan describing the future read-only workflow:

1. Reconfirm local state.
2. Request normal Active Directory DNS service records.
3. Use Windows DC Locator to prefer a site-aware controller.
4. Perform one RootDSE base-scope read for naming contexts and server capabilities.
5. Display the selected controller, site, timing, and failures.

Previewing does not perform those operations. DomainPilot remains in Demo mode and records only that a preview was displayed.

## Security and Monitoring Expectations

Future discovery will use low-volume, standard Windows domain traffic. It will not scan address ranges or ports, repeatedly poll controllers, enumerate users or computers, or test write permissions by making changes.

Normal DNS, Kerberos, and LDAP activity may still appear in server logs or security-monitoring products. DomainPilot will show the current identity, proposed target, expected traffic, and timeout behavior before that capability can be enabled.

## Multi-Site Direction

For organizations with multiple buildings, domain controllers, and WAN links, DomainPilot will prefer Windows site-aware discovery. A later profile milestone will support approved controller preferences and controlled failover without querying every controller by default.
