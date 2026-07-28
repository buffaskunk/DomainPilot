# Read-Only Directory Explorer

The Directory Explorer is DomainPilot's shared read-only workspace for users, computers, groups, and organizational units.

## Current Provider

The public build uses `DemoReadOnlyDirectoryGateway`. Every object, server, domain, address, timestamp, and attribute returned by this provider is fictional. Searching or selecting an object does not contact Active Directory.

The UI identifies:

- Provider and environment.
- Source server.
- Demo or DryRun mode.
- Synthetic or environment data.
- Retrieval timestamp and operation duration.

## Search Controls

- Search text must contain at least two characters.
- Requests are capped at 200 results; the desktop currently requests at most 100.
- Operations have a ten-second application timeout.
- A technician can request cancellation.
- User input is normalized before it reaches a provider.
- `LdapFilterValueEscaper` is tested for the RFC 4515 special characters required by the future LDAP provider.

The gateway interface exposes only `SearchAsync` and `GetDetailsAsync`. Write methods cannot be hidden behind the Directory Explorer.

## Supported Demo Details

- Users: enabled/locked state, password metadata, memberships, profile path, workstation restrictions, and replicated last-logon metadata.
- Computers: DNS name, operating system, last-logon metadata, fictional last-known IP, and site.
- Groups: scope, category, membership count, owner, and approval context.
- Organizational units: protection, delegation, and policy summaries.

## Production Direction

A real on-premises provider will use the same contracts after an operator approves domain discovery. It must:

1. Use Windows integrated authentication.
2. Target the displayed site-aware controller.
3. Use bounded LDAP filters and an allowlist of returned attributes.
4. Avoid recursive group expansion and broad enumeration by default.
5. Return source, timing, truncation, and partial-failure information.
6. Contain no write operation.

The LDAP protocol dependency is not silently bundled in the current milestone. Adding it will be an explicit, reviewed change rather than falling back to launching PowerShell.
