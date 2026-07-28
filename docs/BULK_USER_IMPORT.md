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

The Markdown document explains the workflow; it is not an import file. DomainPilot accepts files ending in `.csv`.

1. Select **Save CSV Template** and choose where to save an editable template.
2. Replace the fictional example values in Excel or a text editor and save the CSV.
3. Select **Import CSV** and choose that edited file.
4. Read the import summary and correct structural errors.
5. Review every row marked **Review**.
6. Select **Validate Rows** after making grid edits.
7. Export the review report for change approval when required.
8. Generate the PowerShell plan and confirm every command still includes `-WhatIf`.

**Load Example** imports a separate training file containing two valid rows and one intentionally unsafe row. It exercises the same parser without requiring you to create a file.

DomainPilot limits one file to 5 MB and one batch to 5,000 structurally valid users so large changes can be divided into manageable, auditable approvals. Exported review reports neutralize formula-like cell values before they are opened in spreadsheet software.

## Current Boundary

CSV import, validation, review export, and PowerShell preview are implemented. Script execution is not implemented, and the desktop app remains in Demo mode.
