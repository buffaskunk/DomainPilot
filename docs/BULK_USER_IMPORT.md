# Bulk User CSV Import

DomainPilot imports bulk-user requests as data for review. Importing a file never creates an account or contacts Active Directory.

## Required Columns

```text
SamAccountName
FirstName
LastName
OrganizationalUnit
Groups
ProfilePath
AllowedWorkstations
```

Column names are case-insensitive and may appear in any order. Fields containing commas must use standard CSV quotes. Separate multiple groups or workstations with semicolons.

The public sample uses fictional names, domains, servers, IP addresses, and computers. Do not commit production exports or workplace identifiers to this repository.

## Review Workflow

1. Select **Import CSV** and choose a UTF-8 CSV file.
2. Read the import summary and correct structural errors.
3. Review every row marked **Review**.
4. Select **Validate Rows** after making grid edits.
5. Export the review report for change approval when required.
6. Generate the PowerShell plan and confirm every command still includes `-WhatIf`.

DomainPilot limits one file to 5 MB and one batch to 5,000 structurally valid users so large changes can be divided into manageable, auditable approvals. Exported review reports neutralize formula-like cell values before they are opened in spreadsheet software.

## Current Boundary

CSV import, validation, review export, and PowerShell preview are implemented. Script execution is not implemented, and the desktop app remains in Demo mode.
