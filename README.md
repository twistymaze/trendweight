# TrendWeight

[TrendWeight](https://trendweight.com) tracks weight trends, smoothing daily
fluctuations so changes over time are easier to see. It supports manual readings,
scale imports, charts, sharing, CSV downloads, and a personal API.

This repository contains the React/TypeScript frontend and ASP.NET Core backend.
They build into one Docker image backed by Supabase PostgreSQL and Clerk login.
Withings and Fitbit integration code lives here; Fitbit syncing can be disabled
by the deployment while retaining imported history.

## Run it locally

You need Node 26, the .NET 10 SDK, and development Supabase and Clerk configuration.
Start with [local setup](docs/SETUP.md), which covers credentials, the database,
and provider callbacks. An npm install alone does not configure these services.

```bash
git clone https://github.com/twistymaze/trendweight.git
cd trendweight
npm ci
dotnet restore apps/api/TrendWeight.sln
```

After configuring your environment, run these in separate terminals:

```bash
npm run -w apps/web dev  # http://localhost:5173
npm run -w apps/api dev  # http://localhost:5199
```

`npm run dev` starts both through tmuxinator if tmux and tmuxinator are installed.

## Contributor documentation

- [Setup](docs/SETUP.md): local environment, database, authentication, and scale linking.
- [Architecture](docs/ARCHITECTURE.md): code map, data flow, and implementation constraints.
- [Testing](docs/TESTING.md): full checks, focused tests, and coverage.
- [Deployment](docs/DEPLOYMENT.md): container configuration, DigitalOcean, and release checks.
- [AGENTS.md](AGENTS.md): coding conventions and agent instructions.

Before committing, run `npm run check && npm run test`. CI also checks formatting
with `npm run check:ci`.

Please open an issue to discuss substantial contributions before implementing them.

## License

[MIT](LICENSE).

## Contributors

See the [contributors page](https://github.com/twistymaze/trendweight/graphs/contributors) for everyone who has contributed.
