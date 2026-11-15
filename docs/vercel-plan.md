# Vercel and Supabase hosting

Status (November 15, 2026): deployed at https://skillsnap-umber.vercel.app with Vercel Authentication enabled for All Deployments. The hosted session listing reads the three starter sessions from Supabase. Hosted account and organizer walkthroughs are in progress. The production container and all 64 tests against both SQLite and PostgreSQL passed GitHub Actions.

## Architecture

Vercel builds Dockerfile.vercel. ASP.NET Core serves the published Blazor WebAssembly files and /api under the same HTTPS origin. The browser uses a relative API address, so each deployment calls its own backend. The container runs as a non-root user.

Supabase stores application data in the private skillsnap PostgreSQL schema. ASP.NET Identity still handles accounts and cookies; Supabase Auth and the browser Data API are not used. Cookie encryption keys are shared through the database, so container replacement can preserve logins. The runtime login owns only the app schema and its objects, with no superuser, role-creation, database-creation, or global RLS-bypass privileges. RLS denies non-owner access; anon, authenticated, and PUBLIC have no app-schema grants. The API enforces user and organizer authorization.

Registration and organizer capacity writes lock the same session row in PostgreSQL before checking availability. SQLite retains its existing atomic writes. Existing SQLite migrations and local accounts remain separate.

## Resources

- GitHub: robcorbisiero/SkillSnap (private).
- Supabase: skillsnap, Rob organization, Canada Central, ref wwovsjuaeeiwpgcwajqq.
- Fresh hosted data: three starter sessions, no local accounts or private key files uploaded.
- corbitpros-dev was paused to free the active free-project slot, preserving its data. It was not deleted. corbitpros was not modified.
- Supabase quoted a $0 monthly project base cost at creation. Usage and future plan changes follow the provider's limits.
- Vercel project: skillsnap in Robert Corbisiero's projects. Created through the website after the connector denied creation. Verified connected deployment: dpl_97xrwnt7PEkTrBiW3bBQdYN7ZCJG, source commit 93d172c.

## Deployment configuration

Use the Container framework, repository root, and Dockerfile.vercel. Project settings need PORT=8080, ASPNETCORE_HTTP_PORTS=8080, Database__Provider=Postgres, and a sensitive server-only ConnectionStrings__SkillSnap variable. This secret is configured for Production only. Do not point unreviewed Preview builds at this database; supply a separate database if enabling previews. All deployments currently require Vercel team sign-in.

The connection string uses the verified Supabase session pooler on port 5432, a restricted application login, SSL Mode=VerifyFull, Root Certificate=/app/Certificates/supabase-prod-ca-2021.crt, and Maximum Pool Size=5. Session mode preserves connection session state across EF operations. Do not disable TLS certificate validation to resolve connection errors. The public CA certificate came from the project's Database Settings download link; it contains no private key.

Never put database credentials in wwwroot, Git, build arguments, or screenshots. Never upload App_Data. The Docker build excludes local databases and keys. Production cookies require HTTPS. /health checks database connectivity; unknown /api routes return 404 instead of the browser app.

## Setup and checks

Local SQLite setup stays in the README. With a disposable PostgreSQL server available, set SKILLSNAP_TEST_POSTGRES privately to its admin connection and run:

```powershell
dotnet test SkillSnap.slnx
Remove-Item Env:SKILLSNAP_TEST_POSTGRES
```

Tests create and drop uniquely named test databases. Do not use a production server for this test suite.

With Docker installed:

```powershell
docker build -f Dockerfile.vercel -t skillsnap .
```

PostgreSQL migrations use a separate context and history in the app schema:

```powershell
dotnet tool restore
dotnet ef migrations add YourPostgresChange --context PostgresSkillSnapDbContext --output-dir Data/PostgresMigrations --project src/SkillSnap.Api
dotnet ef migrations script --context PostgresSkillSnapDbContext --project src/SkillSnap.Api -o artifacts/postgres.sql
```

Review and apply database changes as a controlled deployment step. Startup also checks/applies pending EF migrations using the configured provider. The initial hosted migration was applied through Supabase with the matching EF history entry. Keep both providers' model changes synchronized.

## Readiness limits

Vercel Container Runtime is Beta on all plans. A protected hosted walkthrough must verify signup, login, booking/cancellation, organizer access, attendance, and state after container replacement before calling hosting complete. Hosted organizer access needs explicit approval for the intended hosted account; the earlier local grant does not grant hosted access. Email verification and password recovery remain unfinished, so keep the initial deployment protected.

Sources checked November 15, 2026:

- [Vercel Container Runtime](https://vercel.com/docs/functions/runtimes/container)
- [Supabase connection options](https://supabase.com/docs/guides/database/connecting-to-postgres)
- [Supabase TLS certificate verification](https://supabase.com/docs/guides/platform/ssl-enforcement)

Supabase's advisor reports informational RLS Enabled No Policy notices for the private tables. This is deliberate: the backend schema owner accesses them, while roles used by the browser Data API are denied. [Advisor explanation](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy).
