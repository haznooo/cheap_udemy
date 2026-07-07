# Database scripts

Raw PostgreSQL scripts for setting up the database. There are **no EF Core migrations** —
the schema is managed entirely by these files. Run them in numeric order:

| Order | Script               | Purpose                                                  |
| ----- | -------------------- | -------------------------------------------------------- |
| 1     | `01-create-schema.sql` | Full schema: tables, constraints, indexes, triggers.   |
| 2     | `02-seed.sql`          | Seed data (categories, an initial admin).              |

> `01-create-schema.sql` starts with `DROP SCHEMA public CASCADE` — running it wipes the
> existing `public` schema. Only run it against a database you are happy to reset.

## Run them

```bash
psql "<your-connection-string>" -f db/01-create-schema.sql
psql "<your-connection-string>" -f db/02-seed.sql
```
