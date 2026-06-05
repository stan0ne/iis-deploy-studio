# Security Policy

## Administrator Privileges

IISDeploy Studio requires administrator privileges to read and modify IIS configuration. The application validates elevation at startup and refuses to run without it.

## Credential Handling

- App Pool custom identity passwords are **never exported** — only the username is stored
- Credentials are requested interactively during import via a secure dialog
- Passwords are held in `SecureString` where possible and never logged
- No credentials are persisted to disk

## SSL Certificates

- SSL certificates are **excluded** from exports by default
- When enabled, certificates are exported as encrypted PFX (PKCS#12)
- Password is required for both export and import
- Private keys are never stored in plaintext

## Package Security

- All `.iispackage` files include a SHA-256 checksum in the manifest
- Packages are validated for integrity before import (ZIP header check, manifest validation, checksum verification)
- Corrupted or tampered packages are rejected

## PowerShell Execution

- All PowerShell scripts run through a secure execution service
- Parameters are validated and sanitized to prevent injection
- Script execution is logged with full detail

## Logging

- Sensitive data (passwords, connection strings) is never written to logs
- Log files are stored in the application directory with rolling retention
- Debug mode can be enabled for troubleshooting but defaults to Information level

## Reporting

Contact the maintainers for security issues.
