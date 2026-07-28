# DomainPilot RSAT Console

DomainPilot is a Windows desktop prototype for safer Active Directory help desk operations. It is built as a Visual Studio 2022 WPF app and is intended to demonstrate secure bulk administration workflows, technician training, auditability, and environment readiness checks.

Author and copyright holder: **Rick Linville** (`buffaskunk`)

## Current Status

DomainPilot is in early active development. The public build starts in **Demo Mode**, uses sample data, and does not query or modify the current Active Directory domain. This is especially important because the project is being developed on a work computer with access to a real network.

## Important Disclaimer

DomainPilot is provided for portfolio, educational, lab, and authorized administrative use only. Active Directory and Windows administration tools can affect user access, security policies, computers, servers, and production environments. By downloading, inspecting, modifying, building, or using this project, you are responsible for understanding the code and testing it safely before use.

Rick Linville provides this software as-is and is not responsible or liable for damage, data loss, downtime, misconfiguration, security incidents, account lockouts, unauthorized changes, or any other harm caused by use or misuse of this project. See [DISCLAIMER.md](DISCLAIMER.md) and [LICENSE](LICENSE).

## What It Shows

- Bulk user provisioning review with validation before script generation.
- UTF-8 CSV import with quoted-field support, required-schema checks, source-line tracking, and a 5,000-row batch limit.
- Downloadable CSV template with fictional values for guided technician testing.
- Technician review-report export for correcting and approving bulk changes.
- Profile path, group membership, OU, and workstation restriction planning.
- Dry-run PowerShell generation with `-WhatIf` guardrails.
- User and device lookup concept for last known PC and IP address.
- Approved script/action catalog with risk level and required role.
- Technician-facing environment readiness checklist.
- Audit log viewer and CSV export.
- Layered C# architecture with validation tests.

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
dotnet run --project .\DomainPilot.Tests\DomainPilot.Tests.csproj
```

## Architecture

DomainPilot is split into focused projects:

- `DomainPilot.Core`: domain models and shared contracts.
- `DomainPilot.App`: validation, use-case services, and gateway interfaces.
- `DomainPilot.Infrastructure`: demo gateway and audit logging implementations.
- `DomainPilot.Desktop`: WPF technician interface.
- `DomainPilot.Tests`: lightweight validation and safety checks.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/BULK_USER_IMPORT.md](docs/BULK_USER_IMPORT.md), and [docs/AI_COLLABORATION.md](docs/AI_COLLABORATION.md).

## Roadmap

- CSV template download and optional column mapping.
- Active Directory service layer with mock, dry-run, and live providers.
- Role-based action visibility.
- PowerShell execution through constrained runspaces.
- Windows Event Forwarding or SIEM connector for sign-in lookup.
- Computer inventory, lockout triage, and remote support handoff.
- Installer and signed release artifacts.
