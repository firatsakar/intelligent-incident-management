# Security Policy

## Reporting a vulnerability

Please do **not** open a public issue for a security problem. Report it privately through GitHub:
**Security → Report a vulnerability** on this repository. Include what is affected, how to
reproduce it, and what an attacker could do with it.

You will get an answer within a week. Once a fix is released, the advisory is published with
credit to the reporter, unless you would rather stay anonymous.

## Supported versions

Only the latest release on `master` receives security fixes.

## Scope

In scope: the services, the gateway, the web console, and the Docker Compose installation in this
repository.

Out of scope:

- `docker-compose.dev.yml` and the passwords in the tracked `appsettings.json` files. They are
  development-only values bound to `127.0.0.1`, and a Docker install generates its own secrets.
- Weaknesses that need an already compromised host or database.
