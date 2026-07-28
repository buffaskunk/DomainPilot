# DomainPilot Architecture

DomainPilot is being built as a production-minded Windows administration tool that starts safely in demo mode.

## Production Safety Principle

DomainPilot must never query or modify the current Active Directory domain merely because the app was launched. All domain-facing behavior belongs behind explicit gateway interfaces and must advertise its mode:

- `Demo`: uses sample data only.
- `DryRun`: may read approved sources but must not write changes.
- `Live`: may perform approved writes after role, validation, logging, and confirmation controls exist.

The public build currently uses `DemoActiveDirectoryGateway`, so development on a work computer does not touch the workplace domain.

## Projects

```text
DomainPilot.Core
Domain models and value objects with no UI or infrastructure dependencies.

DomainPilot.App
Use-case services, environment-readiness policy, CSV import, provisioning preflight, validation and review reporting, approval-package generation, PowerShell plan generation, and interfaces.

DomainPilot.Infrastructure
Gateway implementations for local Windows inspection, credential-free profile storage, demo directory data, audit logging, files, PowerShell, AD, and logs.

DomainPilot.Desktop
WPF user interface and technician workflows.

DomainPilot.Tests
Lightweight safety and validation checks that run without external test packages.
```

## Dependency Direction

```text
Desktop -> App -> Core
Desktop -> Infrastructure -> App/Core
Tests -> App/Core
Tests -> Infrastructure
```

Core never depends on Desktop or Infrastructure. This keeps the business rules testable and prevents UI button handlers from becoming the source of security policy.

## First Production-Ready Milestones

1. Keep demo mode as the default.
2. Add real CSV import and row-level validation export. (Implemented)
3. Add configuration profiles for Demo, DryRun, and future Live. (Credential-free local profile implemented)
4. Add read-only environment readiness checks. (Local-only checks and network preview implemented)
5. Add a dry-run Active Directory gateway. (Read-only contracts and full demo provider implemented)
6. Add batched provisioning-reference checks and approval artifacts. (Demo provider and JSON approval package implemented)
7. Add live actions only after role authorization, durable audit, confirmation, and tested rollback workflows are implemented.

## Provisioning Read Boundary

`BulkProvisioningPreflightService` depends on `IReadOnlyProvisioningReferenceGateway`. The
interface can resolve existing account names, OUs, groups, and workstations, but cannot mutate
them. It receives de-duplicated references for an entire batch so providers can use bounded,
efficient directory queries.

The WPF composition root currently supplies `DemoReadOnlyDirectoryGateway`, which serves both the
Directory Explorer and provisioning preflight from the same fictional catalog. A future on-premises
Active Directory implementation belongs in Infrastructure and must not be activated implicitly at
application startup.
