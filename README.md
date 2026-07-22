# Learning Platform API

A backend API for a Udemy-style e-learning platform. It manages user authentication,
user course libraries, and the process of posting, reviewing, and enrolling in courses.

> Learning / CV project — not production-ready. The goal is practice and something to
> show, so it favours simple solutions over heavy "correct" ones.

## Prerequisites

* ASP.NET Core 10 SDK
* PostgreSQL (the setup scripts live in [`db/`](db/))
* A running Supabase instance (used for media storage)

## Environment Variables

Every setting other than `Supabase:Url` and logging is read from environment variables
(not user-secrets or `appsettings.json`).

| Variable           | Purpose                       |
| ------------------ | ----------------------------- |
| `ConnectionString` | PostgreSQL connection string  |
| `JWT_SECRET_KEY`   | Symmetric signing key for JWT |
| `StupidKey`        | Supabase service role key     |

On Windows: search → *Edit the system environment variables* → *Advanced* →
*Environment Variables* → under the user or system section click *New* to add each one.

## Packages

3 projects, 3 `.csproj` files (`Api`, `Business`, `DataAccess`). Verified against actual code usage, not just what's referenced.

### Actively used

| Package | Purpose |
| --- | --- |
| `BCrypt.Net-Next` | Password hashing (signup/login/password change) |
| `supabase-csharp` | Supabase client — storage uploads (avatar/thumbnail/course media), signed URLs for private lesson media |
| `System.IdentityModel.Tokens.Jwt` | JWT creation/validation in `AuthenticationController` |
| `Microsoft.IdentityModel.Tokens` | Token validation parameters |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT bearer auth middleware |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI document generation, paired with Scalar |
| `Scalar.AspNetCore` | `/scalar/v1` API explorer UI |
| `Microsoft.EntityFrameworkCore` / `Npgsql.EntityFrameworkCore.PostgreSQL` | ORM + Postgres provider for `AppDbContext` |
| `Microsoft.Extensions.Configuration` / `.Binder` / `.Json` | Env-var/config binding (connection string, JWT key, Supabase key) |
| `Microsoft.Extensions.DependencyInjection` | `AddDataAccessDI`/`AddBusinessDI` extension methods |

## Database setup

There are **no EF Core migrations** — the schema is managed by the raw SQL scripts in
[`db/`](db/). Run them in order (see [`db/README.md`](db/README.md) for details):

```bash
psql "<your-connection-string>" -f db/01-create-schema.sql   # schema: tables, indexes, triggers
psql "<your-connection-string>" -f db/02-seed.sql            # seed data
```

> `01-create-schema.sql` begins with `DROP SCHEMA public CASCADE` — it resets the
> `public` schema, so only run it on a database you are happy to wipe.

## Run the project

```bash
dotnet restore
dotnet run --project Api
```

Dev URLs:

* HTTP:  `http://localhost:5297`
* HTTPS: `https://localhost:7038`
* API explorer (Scalar UI): `/scalar/v1`

## Tests

```bash
dotnet test
```

`tests/Business.Tests` is a small xUnit test project (Moq + FluentAssertions) covering the
`Business` service layer, with repositories mocked via their `DataAccess.Interfaces`. It's a
curated portfolio of unit-testing techniques rather than a coverage push — repositories and
controllers aren't unit-tested (EF Core repos are integration-test territory, deferred for now).

## Deployment (hosting a live demo)

The live demo is split across three layers, each on its own free host — the standard
"data / API / UI" split, since each layer wants a different kind of hosting:

| Layer                       | Hosted on                          | Status         |
| --------------------------- | ---------------------------------- | -------------- |
| **Database** + media storage | Supabase                           | ✅ see below    |
| **App** (backend API)        | TBD — Render / Azure / Fly (Docker) | ⬜ planned      |
| **Client** (frontend SPA)    | TBD — Vercel / Netlify             | ⬜ planned      |

> Pick your **own** project names, region, passwords, and secrets everywhere below —
> nothing here is shared or fixed.

### Database (Supabase)

Supabase is a hosted **Postgres** provider first (the media buckets this project already
uses are a side feature) — so the same project gives you a free cloud database.

**1. Create a Supabase project.** Sign up and create a project at
[supabase.com](https://supabase.com) — see the
[Supabase getting-started docs](https://supabase.com/docs/guides/getting-started). Choose a
**region close to you** (ideally also close to wherever the backend will be hosted), and set a
**database password** — this is separate from your Supabase login, and you'll need it for the
connection string. If you lose it, reset it under *Project Settings → Database*.

**2. Create the schema + seed it.** In the project's **SQL Editor**:

1. Paste the entire contents of [`db/01-create-schema.supabase.sql`](db/01-create-schema.supabase.sql) → **Run**.
2. New query → paste [`db/02-seed.sql`](db/02-seed.sql) → **Run**.
3. In the **Table Editor**, confirm the 13 tables exist and each shows **"RLS enabled"**.

> **Why a separate `*.supabase.sql` file** (vs `01-create-schema.sql`, which stays the source
> of truth for local dev)? Two Supabase-specific reasons:
> - The local file starts with `DROP SCHEMA public CASCADE`, which resets the role grants
>   Supabase manages. The Supabase variant drops the **tables** instead — still re-runnable.
> - It enables **deny-all Row-Level Security** on every table, so Supabase's auto-generated
>   public REST API can't read your data (users/emails/hashes). Your app connects as the
>   `postgres` **table owner**, which **bypasses RLS**, so app behaviour is unchanged — this
>   only closes the public API you don't use.

**3. Get the connection string.** *Project Settings → Database → Connection string →*
**Session pooler** tab (pick **.NET** in the framework dropdown for the Npgsql format). Fill in
your database password:

```
Host=<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<db-password>;SSL Mode=Require;Trust Server Certificate=true
```

Set this as the `ConnectionString` env var (see [Environment Variables](#environment-variables)).
**Never commit it** — it's a secret; it goes in the env var only, not `appsettings.json`. See
[Supabase's connecting-to-Postgres guide](https://supabase.com/docs/guides/database/connecting-to-postgres)
for the connection-pooler details.

> **Why the Session pooler** (port 5432), not Direct or Transaction? This app is a
> long-running server that keeps a small pool of DB connections open and reuses them across all
> requests (end users never touch the DB directly — only the backend does).
> - **Direct** connection is IPv6-only on Supabase → fails on IPv4-only hosts.
> - **Transaction pooler** (port 6543) is built for serverless functions and drops session
>   features (prepared statements) that EF Core uses.
> - **Session pooler** = a direct connection over IPv4 with full features — the right fit for a
>   persistent .NET server.

### App (backend API) — planned ⬜

To be documented. Plan: containerize the API (`Dockerfile`) and deploy to Render / Azure App
Service / Fly.io. Set `ConnectionString` (the Supabase string above), `JWT_SECRET_KEY`, and
`StupidKey` as env vars on the host, and add the deployed frontend origin to the CORS policy.

### Client (frontend SPA) — planned ⬜

To be documented. Plan: deploy the separate `cheap-udemy-client` repo to Vercel / Netlify /
Cloudflare Pages (static build), pointing its API base URL at the deployed backend.
