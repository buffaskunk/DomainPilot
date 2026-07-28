# Security Policy

DomainPilot is designed around least privilege, predictable technician workflows, and auditability.

DomainPilot is an administrative automation project. Do not run generated scripts or future live actions against any environment unless you are authorized to do so and have tested the action in a lab first.

## Prototype Status

This repository currently contains a dry-run prototype. It does not directly modify Active Directory. Generated PowerShell uses `-WhatIf` and should be reviewed before production adaptation.

The Environment tab can perform explicitly requested local workstation checks. These checks use local Windows APIs and file-system reads and do not contact DNS, a domain controller, an event-log source, or another computer. The domain-discovery screen is currently a non-executable preview.

The Directory Explorer currently uses fictional demo data. Its shared gateway contract exposes read operations only, enforces bounded searches and timeouts, and records source information. The prepared Windows DC Locator provider is not connected to the desktop UI and requires an explicit approval flag before it can run.

Bulk provisioning preflight also uses the fictional provider. It de-duplicates directory
references and resolves one bounded batch, detects duplicate and existing usernames, and blocks
missing OUs, groups, or workstations. Approval packages contain proposed dry-run commands and
review findings but do not store a password, token, or reusable credential value.

## Production Requirements

- Use delegated AD groups for each action category.
- Separate daily user accounts from admin accounts.
- Require MFA and Privileged Access Workstation guidance for admin use.
- Log every action with actor, target, ticket/change ID, timestamp, source machine, and result.
- Validate all imported CSV data before command generation.
- Block high-risk groups such as Domain Admins from bulk workflows unless an explicit privileged workflow is implemented.
- Execute custom scripts only from trusted, signed locations.
- Protect exported logs because they may contain usernames, device names, and IP addresses.
- Protect approval packages because they describe proposed accounts, memberships, paths, and workstation restrictions.

## Reporting Issues

For portfolio use, open an issue in GitHub describing the unsafe behavior, expected behavior, and steps to reproduce. Do not include real domain names, production usernames, IP addresses, or secrets.
