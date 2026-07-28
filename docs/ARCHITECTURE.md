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
Use-case services, CSV import, validation and review reporting, PowerShell plan generation, and interfaces.

DomainPilot.Infrastructure
Gateway implementations for demo data, audit logging, files, PowerShell, AD, and logs.

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
```

Core never depends on Desktop or Infrastructure. This keeps the business rules testable and prevents UI button handlers from becoming the source of security policy.

## First Production-Ready Milestones

1. Keep demo mode as the default.
2. Add real CSV import and row-level validation export. (Implemented)
3. Add configuration profiles for Demo, DryRun, and future Live.
4. Add read-only environment readiness checks.
5. Add a dry-run Active Directory gateway.
6. Add live actions only after approval, audit, and rollback workflows are implemented.
