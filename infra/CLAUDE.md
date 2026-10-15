# Infra

## Layout
- `docker/Dockerfile` multi-stage build; build context is always the repo root
  - `api` target: the published Web host on port 8080
  - `migrator` target: one EF migration bundle per module (Identity, Organization, Menu); runs once and exits
- `docker/Dockerfile.dockerignore` keeps bin/obj, tests, docs and local appsettings out of the images
- `docker/docker-compose.yml` local dev stack: `db` → `migrate` → `api`

## Services
| Service | Image | Host port | Notes |
|---|---|---|---|
| db | postgres:17-alpine | 5434 | 5434 avoids a local PostgreSQL on 5432; data in the `db-data` volume |
| migrate | `migrator` target | - | Starts after db is healthy; api waits for it to exit 0 |
| api | `api` target | 5080 | Swagger at http://localhost:5080/swagger |

## Commands
Run everything from the repo root. Set the compose file once per shell:
`export COMPOSE_FILE=infra/docker/docker-compose.yml`
(otherwise add `-f infra/docker/docker-compose.yml` to every command)

- Start (build + run): `docker compose up --build -d`
- Status: `docker compose ps -a`
- Logs, all services: `docker compose logs -f`
- Logs, one service: `docker compose logs -f api` (or `db`, `migrate`)
- Last lines only: `docker compose logs --tail 100 api`
- Rebuild the API after code changes: `docker compose up --build -d api`
- Re-run migrations after adding one: `docker compose up --build migrate`
- Restart without rebuilding: `docker compose restart api`
- Stop (keeps data): `docker compose down`
- Stop and wipe the database: `docker compose down -v`
- psql shell: `docker compose exec db psql -U postgres -d restaurant_db-development`
- Shell in the API container: `docker compose exec api sh`

Connect from the host (IDE, `dotnet ef`, `dotnet run`):
`Host=localhost;Port=5434;Database=restaurant_db-development;Username=postgres;Password=postgres`

## Configuration
- Credentials are local-only defaults; override with `POSTGRES_USER`, `POSTGRES_PASSWORD`,
  `POSTGRES_DB` environment variables (or a `.env` file next to the compose file; never commit it)
- Email: copy `docker/.env.example` to `docker/.env` and fill `SMTP_USER`/`SMTP_PASS` (Mailtrap
  sandbox). Without them sending fails with "5.7.1 Authentication required"; read the
  verification code from `user_verification_codes` instead
- App settings come from `appsettings.json` baked into the image; override any key with an
  environment variable using `__` for nesting, e.g. `Jwt__Key`, `Sentry__Dsn`
- Never commit real secrets (connection strings, JWT key, Sentry DSN) to these files

## Troubleshooting
- `migrate` exits 1: `docker compose logs migrate`. The bundles boot the Web host, so startup
  validation (Jwt, AuthRateLimit) runs; the image copies `appsettings.json` for this reason
- api not reachable: `docker compose ps -a` to check that `migrate` exited 0 and `api` is up
- Port already allocated: another process uses 5434 or 5080; change the host side of `ports`

## Rules
- Keep the build context at the repo root; the Dockerfile copies `src/`
- A new module needs its name added to the bundle loop in the Dockerfile
- No auto-migration in the app: schema changes reach the database only through `migrate`
