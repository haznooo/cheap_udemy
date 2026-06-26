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
dotnet run --project learning_platform
```

Dev URLs:

* HTTP:  `http://localhost:5297`
* HTTPS: `https://localhost:7038`
* API explorer (Scalar UI): `/scalar/v1`
