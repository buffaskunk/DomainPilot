# Security Policy

DomainPilot is designed around least privilege, predictable technician workflows, and auditability.

## Prototype Status

This repository currently contains a dry-run prototype. It does not directly modify Active Directory. Generated PowerShell uses `-WhatIf` and should be reviewed before production adaptation.

## Production Requirements

- Use delegated AD groups for each action category.
- Separate daily user accounts from admin accounts.
- Require MFA and Privileged Access Workstation guidance for admin use.
- Log every action with actor, target, ticket/change ID, timestamp, source machine, and result.
- Validate all imported CSV data before command generation.
- Block high-risk groups such as Domain Admins from bulk workflows unless an explicit privileged workflow is implemented.
- Execute custom scripts only from trusted, signed locations.
- Protect exported logs because they may contain usernames, device names, and IP addresses.

## Reporting Issues

For portfolio use, open an issue in GitHub describing the unsafe behavior, expected behavior, and steps to reproduce. Do not include real domain names, production usernames, IP addresses, or secrets.
