# DomainPilot Admin Setup Guide

Use this as the checklist for turning the prototype into a controlled lab or production-ready tool.

## Workstations

- Install RSAT Active Directory tools.
- Confirm the `ActiveDirectory` PowerShell module imports successfully.
- Run DomainPilot as a delegated admin account.
- Keep the operator workstation patched and protected.

## Active Directory

- Create OUs for managed users, disabled users, service accounts, and test users.
- Define standard role groups such as help desk, finance apps, VPN, MFA enforcement, and remote support.
- Delegate only the rights required for each supported workflow.
- Document password, lockout, logon-hours, and workstation-restriction policies.

## File Servers

- Create profile/home directory shares before provisioning users.
- Use least-privilege share and NTFS permissions.
- Validate profile paths with a service check before enabling live execution.

## Logging

- Enable auditing for account creation, group membership changes, account lockouts, and logon events.
- Forward domain controller security logs to Windows Event Forwarding or a SIEM.
- Define how long audit data is retained.
- Ensure technicians understand that last-PC lookup is only as trustworthy as the configured event source.

## Safe Release Steps

1. Build and test in a lab domain.
2. Run every action in dry-run mode.
3. Review generated commands with a senior admin.
4. Enable live execution only for one low-risk workflow at a time.
5. Keep rollback instructions beside every high-risk action.
