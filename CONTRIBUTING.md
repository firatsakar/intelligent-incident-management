# Contributing

Thanks for your interest in Intelligent Incident Management. Bug reports, fixes and improvements
are welcome.

## Before you start

- **Bugs:** open an issue with steps to reproduce, what you expected, and what happened. Include
  service logs where they help.
- **Larger changes** (a new service, a new integration, a change to an event contract): open an
  issue first so the approach can be agreed before you write the code.
- **Security problems:** see [SECURITY.md](SECURITY.md). Do not open a public issue.

## Development setup

You need the .NET 10 SDK, Node.js 24 and Docker. The [README](README.md#development) walks through
the infrastructure, the user secrets each service needs, and running the services, the gateway and
the console.

## Checks

Run these before opening a pull request. They must pass.

```bash
dotnet build
dotnet test
```

`dotnet test` includes `tests/Integration.Tests`, which start PostgreSQL and RabbitMQ in Docker, so
Docker must be running.

```bash
cd web
npm ci
npm run build
npm run lint
npm run check:i18n
```

## Conventions

- **Clean Architecture per service:** Domain → Application → Infrastructure → API. Domain and
  Application never reference Infrastructure; interfaces live with their consumer.
- **Services never reference each other.** They talk through integration events in
  `BuildingBlocks.Contracts` over RabbitMQ, or through the gateway.
- **External side effects go through the outbox,** in the same transaction as the change that
  caused them. Consumers must be idempotent.
- **Organisation scope:** every query for organisation data is filtered by organisation. Add a test
  when you add a new kind of data.
- **Console text lives in the dictionaries.** Every string a user reads goes in both
  `web/src/lib/i18n/en.ts` and `tr.ts`; `npm run check:i18n` finds text that did not.
- **Tests:** a change in a layer that has tests comes with tests.
- **English** for code, comments, commit messages and log output.

## Pull requests

- Branch from `develop` and open the pull request against `develop`. `master` holds releases.
- Keep one topic per pull request, and say what changed and why.
- By contributing, you agree that your contribution is licensed under the
  [Apache License 2.0](LICENSE).
