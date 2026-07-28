# Bulk Provisioning Preflight

Bulk Provisioning Preflight turns an imported CSV queue into an auditable proposal before any
script is adapted for execution. The public build resolves references against a fictional
directory and does not contact the workstation's domain.

## Checks Performed

- Reuses the local username, name, OU syntax, profile path, group, and privileged-group rules.
- Detects duplicate usernames within the same imported batch.
- Detects usernames that already exist in the selected directory source.
- Confirms that requested OUs, groups, and allowed workstations exist.
- Reminds the reviewer that profile-share availability and permissions require a separately
  approved check.

The application sends all unique references to the provider in one bounded request. This avoids
the slow and noisy pattern of issuing a separate directory query for every CSV row.

## Technician Workflow

1. Import or edit the CSV queue.
2. Select **Validate Rows** for immediate local feedback.
3. Select **Check Demo Directory** to run the batched fictional provider check.
4. Review the **Status**, **Directory**, **Notes**, and **Directory Findings** columns.
5. Select **Generate PowerShell Plan**. DomainPilot reruns preflight and includes only ready rows.
6. Select **Export Approval Package** to save the source, findings, counts, dry-run script, batch
   ID, and rollback guidance as JSON.

The approval package does not contain a password or reusable credential value. The generated
PowerShell asks the eventual operator for a temporary password as a secure string at runtime and
retains `-WhatIf` on every proposed write.

## Production Provider Requirements

A future Active Directory implementation must preserve the
`IReadOnlyProvisioningReferenceGateway` contract and:

- Use delegated, read-only permissions.
- Resolve references in bounded batches rather than one query per row.
- Escape all LDAP filter values.
- Honor cancellation and operation timeouts.
- Identify the actual provider, environment, server, retrieval time, and mode.
- Avoid retrieving unnecessary attributes or secrets.
- Never expose create, update, move, or delete operations through the preflight gateway.

Connecting that provider will be a separate, operator-approved milestone. The current desktop
composition root instantiates only `DemoReadOnlyDirectoryGateway`.
