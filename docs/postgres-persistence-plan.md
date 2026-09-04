# Plan: Persist Transactions to PostgreSQL

## Context

Today, `Program.cs` reads a CSV file path from the command-line args, parses it via
`TransactionReader.CsvHelper.ReadFile` (using a `TransactionMap`), and holds the resulting
`List<Transaction>` in memory only — printing each row to the console. Nothing is persisted.

Goal: persist parsed `Transaction` records into a PostgreSQL database.

## Decision: Postgres via Docker (not a local service)

Chosen approach: run Postgres in Docker (e.g. via a `docker-compose.yml` checked into the repo),
rather than installing a local Postgres service (e.g. via Homebrew).

Why:
- **Disposable/repeatable** — while iterating on schema/mapping, easy to reset with
  `docker compose down -v && docker compose up -d`.
- **No host pollution** — doesn't touch global machine state (launchd services, version conflicts).
- **Reproducible for others** — anyone cloning the repo can `docker compose up` and get the same
  Postgres version with no manual setup.
- **Version pinning** — trivial to pin an exact image tag (e.g. `postgres:16`).

## Step-by-step plan

1. **Run Postgres in Docker**
   Add a `docker-compose.yml` (postgres image, exposed port, named volume, env vars for
   db/user/password). Start it and confirm connectivity (e.g. via `psql` or a GUI client).

2. **Pick the .NET data-access approach**
   Likely **Npgsql** directly (or **Dapper** on top of it) rather than EF Core, to stay close to
   the actual SQL while learning. Decide before moving on.

3. **Design the `transactions` table**
   Map `Transaction`'s fields (dates, currency, amount, nullable description/status/merchant/
   country/area, the `CreditOrDebit` enum) to SQL types/constraints. Decide on a primary key
   strategy (serial/identity vs natural key) and whether duplicate-import protection is needed.

4. **Add the connection string safely**
   Via `appsettings.json` (gitignored for secrets) or an environment variable — never hardcoded.

5. **Add the DB package(s)**
   Add `Npgsql` (and possibly `Dapper`) to `TransactionReader.csproj`.

6. **Write a small repository/access layer**
   E.g. `TransactionRepository.SaveAll(IEnumerable<Transaction>)` that performs a batched insert.

7. **Create the schema**
   Either a plain SQL script run once, or a lightweight migration tool (e.g. DbUp) — to be
   discussed.

8. **Wire it into `Program.cs`**
   After reading the CSV, call the repository to persist the transactions, and report success /
   row count.

9. **Test end-to-end**
   Run against a real CSV, verify rows land in Postgres, and check re-run behavior (duplicates?).

## Status

- [ ] 1. Postgres running in Docker
- [ ] 2. Data-access approach decided
- [ ] 3. `transactions` table designed
- [ ] 4. Connection string wired up safely
- [ ] 5. DB package(s) added
- [ ] 6. Repository/access layer written
- [ ] 7. Schema creation mechanism chosen and run
- [ ] 8. `Program.cs` wired up to persist
- [ ] 9. End-to-end test passed
