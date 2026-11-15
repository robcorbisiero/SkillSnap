# Vercel deployment design

Status: architecture decision in progress. No Vercel project, database, public deployment, or paid resource has been created for SkillSnap.

Vercel's Container Runtime can run the existing ASP.NET Core HTTP server in a Docker image. It is currently Beta on all plans. The Blazor WebAssembly frontend can remain unchanged as a static build. The app does not need to be rewritten into JavaScript.

Sources checked November 14, 2026:

- [Container Runtime](https://vercel.com/docs/functions/runtimes/container)
- [Services](https://vercel.com/docs/services)
- [SQLite on Vercel](https://vercel.com/kb/guide/is-sqlite-supported-in-vercel)

## Recommended full Vercel setup

Serve the browser app and container API under one Vercel origin and use hosted PostgreSQL for persistent application data. Preserve SQLite as the local-development provider. Keep login-cookie Data Protection keys in durable shared storage rather than an instance-local directory.

Before deployment, implement and verify:

1. A repeatable frontend/container build with a relative API address. Preview deployments must call their own API, not localhost or the production URL.
2. PostgreSQL-specific EF migrations while preserving the existing SQLite migrations and local data.
3. Provider-aware transactional locking for booking/capacity updates. The current single-statement SQLite safety argument must not simply be assumed to hold under PostgreSQL concurrency.
4. Durable shared Data Protection key storage so cookie validation survives container replacement and scaling.
5. Production configuration, health checks, HTTPS/proxy handling, isolated preview data, and a deliberate organizer-provisioning process.
6. Tests against real PostgreSQL as well as the local SQLite suite, then an actual protected preview walkthrough.

Database provisioning/provider choice and any billing or terms need to be resolved before creating resources. Do not upload the local accounts database or cookie keys into the source repository or an image. Use fresh hosted demo data unless an explicit data migration is requested.

## Alternative retaining the backend unchanged

Deploy the Blazor frontend on Vercel and host the ASP.NET Core/SQLite API on a server with durable storage. Prefer routing API requests through the same public site to preserve cookie behavior. The backend still needs a selected host, persistent database/key storage, HTTPS, and deployment verification. This uses two hosting services but avoids a production database-provider migration.

## Current limitations

A local SQLite file and local key directory are not durable application state on Vercel Functions, including its container runtime. Vercel Daemons are a background-worker product, not a direct replacement for the HTTP API host. No hosted version is claimed ready until its state, identity, and concurrency behavior are tested. Public signup also needs an explicit decision about the currently deferred email verification/recovery work.
