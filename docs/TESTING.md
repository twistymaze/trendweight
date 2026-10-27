# Testing

Run commands from the repository root after `npm ci` and
`dotnet restore apps/api/TrendWeight.sln`. The suites mock external services and
must not use live account credentials.

## Required checks

```bash
npm run check && npm run test
npm run check:ci
```

`check` runs TypeScript and lint checks; backend lint builds with warnings as
errors. `test` runs the Docker helper and release automation tests, then builds and tests both workspaces.
`check:ci` also verifies formatting. `npm run format` writes formatting changes;
review the diff before committing.

## Focused tests

| Task                  | Command                                                                                  |
| --------------------- | ---------------------------------------------------------------------------------------- |
| Frontend suite        | `npm run -w apps/web test`                                                               |
| One frontend file     | `npm run -w apps/web test -- src/lib/core/dates.test.ts`                                 |
| Frontend coverage     | `npm run -w apps/web test:coverage`                                                      |
| Backend suite         | `npm run -w apps/api test`                                                               |
| One backend class     | `dotnet test --project apps/api/TrendWeight.Tests --filter-class '*ProfileServiceTests'` |
| Docker helper scripts | `npm run test:tooling`                                                                   |

The backend uses xUnit v3 with Microsoft.Testing.Platform (`global.json`), not
legacy VSTest `--filter` syntax. Frontend coverage HTML is written under
`apps/web/coverage` (see Coverage below).

## Test fixtures and conventions

Frontend helpers live in `apps/web/src/test/`:

- `render.tsx`: `renderWithProviders` (real `QueryClient`), `createQueryWrapper`
  for `renderHook`, and `renderWithDashboardData`, which mounts the real
  dashboard context provider so dashboard components are tested without
  mocking `useDashboardData`.
- `msw.ts`: `recordRequests()` captures every request MSW handles (method, path,
  headers, parsed body) so tests assert the `Authorization` header and body
  outside the handler; `json(status, body)` and `noContent()` build responses.
- `auth.ts`: `mockAuth()` / `signedOutAuth()` (each file keeps a one-line
  `vi.mock("@/lib/auth/use-auth")`). `clock.ts`: `freezeClock()` fakes only
  `Date`, which js-joda's `LocalDate.now()` reads. `routes.ts`: `runLoader`,
  `runBeforeLoad`, `expectRedirect` exercise route loaders without a router.
- `fixtures/`: builders with every field populated (`buildProfileResponse`,
  `buildMeasurementsResponse`, `buildProviderLink`, `buildDataPoint`, ...) so
  `toEqual` catches a dropped mapping.

Prefer MSW-backed real hooks over `vi.mock` of `@/lib/api/*`; never mock
`@/lib/core/numbers` or `@/lib/core/dates`. Assert rendered text, roles, and
aria labels rather than class names. ESLint enforces the testing-library,
jest-dom, and vitest rules for test files (no conditional or standalone
`expect`, awaited async utilities, no `container` or DOM traversal).

Backend fixtures live in `apps/api/TrendWeight.Tests/Fixtures/`:

- `FakeSupabaseService`: an in-memory `ISupabaseService` that evaluates the
  real `Where`, `Filter` (including `profile->>Field` JSON paths), and `Limit`
  calls against seeded rows, so a missing or wrong filter returns another
  user's data and fails the test. Seed with `Seed(rows...)`, inspect with
  `Rows<T>()`, inject failures with `ThrowOnQuery/Insert/Update/Delete`.
- `WithingsPayloads`: hand-written JSON matching the documented Withings API,
  never serialized from our own models, so a renamed property attribute fails.
- `RecordingHttpHandler` (`ProviderTestSupport.cs`): records outbound provider
  requests (URL, auth header, body) and throws on unexpected ones.
- `CapturingLoggerProvider` + `LogAssertions`: `ShouldHaveLogged`,
  `ShouldNotHaveLogged`, and `ShouldNotMention(secret)`, which scans messages,
  exceptions, and structured state so sharing codes never reach a log.

Tests of third-party parsing verify that our code reads the documented format
and survives drift (missing or extra fields, nulls, unknown types, non-200s,
malformed bodies); they do not verify that providers send documented data.

## Cross-layer guards

These tests fail when a change needs a deliberate decision, not a code fix:

- `Infrastructure/Startup/EndpointAuthorizationInventoryTests.cs` walks every
  routed `/api` endpoint. Anonymous endpoints must be on its allowlist, `/api/v1`
  must accept only API keys, internal endpoints must reject API keys, and every
  protected endpoint must answer 401 without credentials.
- `Infrastructure/Startup/ServiceRegistrationTests.cs` activates every controller
  from the real Production container and validates the container on build.
- `apps/web/src/lib/routes/route-guards.test.ts` loads every file route. Routes
  that are not on its public list must redirect signed-out visitors to `/login`.
- API response contracts: `Contracts/ApiContractSnapshotTests.cs` serializes each
  response type the web app reads (every property populated, real serializer
  settings) into `apps/web/src/test/contracts/*.json`, and the public v1 OpenAPI
  document into `apps/api/TrendWeight.Tests/Contracts/openapi-v1.json`.
  `apps/web/src/test/contracts/contracts.test.ts` checks that every field the web
  types declare exists in those files with the same JSON type. After an
  intentional contract change, regenerate the files and review the diff:

  ```bash
  UPDATE_API_CONTRACTS=1 dotnet test --project apps/api/TrendWeight.Tests --filter-class '*ApiContractSnapshotTests'
  ```

## Coverage

Coverage is reported, not gated:

| Task            | Command                             |
| --------------- | ----------------------------------- |
| Both workspaces | `npm run test:coverage`             |
| Frontend only   | `npm run -w apps/web test:coverage` |
| Backend only    | `npm run -w apps/api test:coverage` |

The frontend report (`apps/web/coverage`) measures every file under `src/`,
so untested modules appear at 0%. The backend writes `apps/api/TestResults/coverage.cobertura.xml` through the
Microsoft.Testing.Platform code-coverage extension.

## Where to add a regression

- Frontend tests are colocated `*.test.ts(x)` files using Vitest, Testing Library,
  and MSW. Cover user-visible behavior, including malformed input, empty data,
  account transitions, and editing during refetches.
- Backend tests mirror feature/service folders under `apps/api/TrendWeight.Tests`.
  Use `Infrastructure/Startup/RequestPipelineTests.cs` for middleware ordering,
  authentication scheme selection, redirects, and HTTP response behavior. These
  tests run real middleware with external identity/database services mocked.
- `scripts/docker-scripts.test.mjs` runs the Docker helpers with a stub executable
  and synthetic configuration. It does not build or run containers.

Tests do not establish live provider connectivity, production database policy,
or successful deployment. Use the [deployment checks](DEPLOYMENT.md#verify-a-release)
for those boundaries.

On sandboxed macOS, .NET can fail when named-pipe sockets or NuGet access are
blocked. Use an execution environment permitting those operations; an infrastructure
failure is not a passing or failing application test.

- `.github/scripts/release-please.test.mjs` verifies dependency summaries, version bumps,
  preserved release notes, and read-only preview behavior using the pinned library
  with synthetic commits and no network access.
