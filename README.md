# DomainPilot RSAT Console

DomainPilot is a Windows desktop prototype for safer Active Directory help desk operations. It is built as a Visual Studio 2022 WPF app and is intended to demonstrate secure bulk administration workflows, technician training, auditability, and environment readiness checks.

Author: `buffaskunk`

## What It Shows

- Bulk user provisioning review with validation before script generation.
- Profile path, group membership, OU, and workstation restriction planning.
- Dry-run PowerShell generation with `-WhatIf` guardrails.
- User and device lookup concept for last known PC and IP address.
- Approved script/action catalog with risk level and required role.
- Technician-facing environment readiness checklist.
- Audit log viewer and CSV export.

## Safety Design

The current prototype does not directly modify Active Directory. It validates rows and generates reviewed PowerShell plans. This is intentional: an RSAT portfolio app should prove that administrative actions are controlled, explainable, and auditable before adding live execution.

Before adding live execution:

- Require a delegated admin role, never a default Domain Admin workflow.
- Require a change ticket or approval ID for privileged or destructive actions.
- Sign bundled scripts and block unsigned custom scripts by default.
- Keep `-WhatIf` preview available for every supported action.
- Write immutable logs to a protected location or SIEM.
- Pull last-PC data from approved sources such as forwarded domain controller security logs, endpoint inventory, or a SIEM.

## Build

Open `DomainPilot.sln` in Visual Studio Community 2022 and run `DomainPilot.Desktop`.

From a terminal:

```powershell
dotnet build DomainPilot.sln
dotnet run --project .\DomainPilot.Desktop\DomainPilot.Desktop.csproj
```

## Roadmap

- CSV import with schema mapping and row-level error export.
- Active Directory service layer with mock, dry-run, and live providers.
- Role-based action visibility.
- PowerShell execution through constrained runspaces.
- Windows Event Forwarding or SIEM connector for sign-in lookup.
- Computer inventory, lockout triage, and remote support handoff.
- Installer and signed release artifacts.
