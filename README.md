# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" \
  --project apps/api/src/SupportDesk.Presentation   # once: the key that signs access tokens
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection` |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket (runs triage) |
| `POST /api/tickets/{id}/escalate` · `GET /api/tickets/{id}/escalations` | Escalate / escalation history |
| `POST /api/auth/login` · `GET /api/auth/me` | Sign in / current agent |
| `GET /health` | Database health (no token needed) |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |

## Implementation

### 1.1 DEFECT-117: filters sometimes do not take effect

**Root cause:** the `useTicketList` effect debounced all fetching and only listed `filters.search`, `filters.page` and `filters.pageSize` as dependencies, so changing a dropdown filter (status, priority, category etc.) never triggered a request until the search text changed.

**Fix:** the fetch now depends on all filters, so any change sends a request straight away. Only the search text is debounced (300 ms), and responses from outdated requests are ignored.

**How to verify:** `npm test` (the regression tests fail on the old code), or change Status in the app and watch the Network tab.

### 1.2 Server-side filtering

**Root cause:** `GET /api/tickets` bound the filters into `TicketQuery`, but `TicketQueries.GetPagedAsync` never used them, so it counted and paged every ticket.

**Fix:** `ApplyFilters` adds one `.Where` per filter (combined with AND) before `CountAsync` and paging, so `totalCount` and `totalPages` match the filtered set and nothing is filtered in C#.

### 2.1 Business rules

**Root cause:** the first (AI-generated) version worked but was hard to follow: an interface with one implementation, SLA hours in three places, duplicated escalation checks, and rules that depended on `AgentDto`. It also had two bugs: an agent over their limit could keep a ticket (`>` instead of `>=`), and At risk was never calculated because the evaluator never got the SLA window.

**Fix:** the rules are a few small domain classes shared by create and escalate:
- `SlaPolicy`: SLA hours from config, checked at startup (BR-2, BR-3, BR-6)
- `SlaEvaluator`: the SLA status, always calculated, never stored (BR-8)
- `AgentSelector`: picks the agent (BR-4, BR-5)
- `TicketTriage`: combines them and returns the decision with reasons (BR-1)
- `Ticket.EnsureCanBeEscalated()`: the one place that blocks an escalation (BR-7)

**Decisions:**
- Ties between agents go to the lowest id, so the result is stable.
- On escalation the current owner can keep the ticket even at their limit, since it's already counted in their load. A new ticket needs an agent below the limit.
- At exactly the due time a ticket is At risk, not Breached.
- The 1-hour floor only applies to Premium, as BR-3 is written.
- Category behaviour comes from the `RequiresSpecialist` and `ForcesCriticalPriority` flags. No names are hard-coded.

### 2.2 Backend

- `POST /api/tickets` runs the triage and returns the ticket plus a `triage` object with the reasons. If nobody is eligible, the ticket is still created, unassigned, with a reason.
- `POST /api/tickets/{id}/escalate` with `{ reason }` returns `{ ticket, escalation }`: 400 for a reason outside 5–500 characters (after trimming), 404 for an unknown ticket, 409 for Critical, Resolved or Closed.
- `GET /api/tickets/{id}/escalations` returns the history, newest first.
- `GET /api/tickets?slaStatus=AtRisk` (also `NotApplicable`, `WithinSla`, `Breached`, `Met`) is filtered in SQL like the other filters.

The migration `AddTicketEscalations` adds the `TicketEscalations` table. It has check constraints (priority 1–4, `ToPriority = FromPriority + 1`) and an index on `(TicketId, EscalatedAtUtc DESC)`, which covers the foreign key, the history query and the "latest escalation" lookup.

**Decisions:**
- After an escalation, the SLA window starts from the escalation (BR-7). I read that with a `MAX(EscalatedAtUtc)` subquery instead of adding a column to `Tickets`, so the history table is the only source.
- The SLA rule exists twice: in C# for the displayed status, and in SQL for the filter, with a comment in each pointing to the other. `DATEDIFF` works in whole seconds, so at the at-risk boundary the two can differ by up to a second.
- The create response is the existing ticket detail with an extra `triage` field, so the existing form kept working.
- Foreign keys on `TicketEscalations` restrict deletes, because it's an audit trail.

**Known limitations:**
- Two tickets created at the same moment can both go to an agent with room for only one, because there's no locking.
- SQLite doesn't support `DATEDIFF`, so I tested the SLA filter by hand against SQL Server.

### 2.3 Frontend

- `SlaIndicator` is used on both the list and the ticket page. Each state has its own label and symbol, not just a colour.
- There's an SLA dropdown in the filter bar. Because of the DEFECT-117 fix, it only needed the new field.
- The escalation panel on the ticket page validates the same way as the server, shows a pending state, and shows server errors (including 409) under the form. On success the ticket and the history update without a page reload.
- The escalation history has loading, error and empty states.
- After creating a ticket, the triage result is shown with its reasons.
- The escalate form is disabled for Critical, Resolved and Closed tickets, with a note saying why.

On a 409, the ticket stays as it was and the typed reason is kept. After an escalation, the history is fetched again rather than updated locally.

### 2.4 Tests

`npm test` runs everything. Backend tests are in `apps/api/tests/SupportDesk.UnitTests` (using `FixedClock` and `TicketBuilder`); frontend tests sit next to the components.

- **Backend:**
  - SLA windows for every priority and tier, including the floor
  - Forced-Critical categories
  - Agent selection: load, specialists, limits, inactive agents, ties, nobody eligible
  - Escalation by one level, with the due date recalculated
  - Keeping, reassigning or unassigning the owner
  - The history row and the rejections
  - Reason length at 4, 5, 500 and 501 characters
  - SLA status at the exact boundaries
- **Frontend:**
  - A short reason blocks submission
  - A 409 is shown and the ticket is left unchanged
  - All five SLA states
  - The disabled form
  - History and the triage result
  - The SLA filter sends a request immediately

Not covered by automated tests: the SQL SLA filter (see 2.2).

### 3.1 Authentication: backend

**Dev login:** any seeded agent's email (e.g. `alex.turner@northwind-support.example`) with the password **`Test_password`**. Yuki Tanaka is inactive and can't sign in. If your database was seeded with an older password, run `docker compose down -v` to re-seed it.

- `POST /api/auth/login` returns a token, its expiry and the agent. A wrong email, a wrong password and an inactive agent all get the same 401.
- `GET /api/auth/me` returns the current agent, checked against the database.
- Everything else needs a token. This comes from a fallback authorization policy, so new endpoints are protected by default.
- Escalate takes the agent's name from the token, not from the request body.

**Decisions:**
- The password hash is a column on `Agents` (migration `AddAgentPasswordHash`), not a separate Users table. Every user is an agent, so a second table would only duplicate the email and the active flag.
- Passwords are hashed with Identity's `PasswordHasher` (PBKDF2, salted).
- An unknown email is still checked against a dummy hash, so response times don't reveal which emails exist.
- The JWT uses HMAC-SHA256, lasts 60 minutes, and carries the claims `sub`, `name` and `email`. There are no refresh tokens (out of scope).
- The signing key isn't in any config file. It lives in user-secrets locally and in the `Jwt__SigningKey` environment variable elsewhere. The API won't start without a key of at least 32 bytes.

**Known limitations:**
- There's no rate limiting or lockout.
- Tokens can't be revoked before they expire. Only `/me` re-checks the agent.
- `EscalatedBy` is limited to 100 characters, but names can be up to 200.

For production, I'd add a secret store with key rotation, refresh tokens with revocation, account lockout, and HTTPS only.

### 3.2 Authentication: frontend

- The `/login` page has validation, a pending state, and shows the server's error.
- `api/client.ts` adds the token to every request.
- Every route except `/login` is behind `RequireAuth`, and after signing in you're sent back to the page you asked for.
- The header shows who is signed in, with a Sign out button.
- Any 401 clears the session and sends you to the login page. The app also calls `/api/auth/me` on load, so an expired token is caught straight away.

**Token storage:** `sessionStorage`. It survives a refresh and is cleared when the tab closes, but any script on the page can read it, so an XSS bug could leak it. An HttpOnly cookie would be safer but needs CSRF protection on the API. That's the first thing I'd change for production.

### 3.3 Tests

The integration tests in `apps/api/tests/SupportDesk.IntegrationTests` run the real API in memory (`WebApplicationFactory`), with SQLite and a random signing key on every run. They cover:
- A successful login.
- A wrong password, an unknown email and an inactive agent all getting the same 401.
- Protected endpoints returning 401 without a token and 200 with one.
- A tampered token being rejected.
- An escalation being recorded under the agent from the token.
- `/health` being reachable without a token.

To check the tests actually catch something, I removed the fallback policy, and 6 tests failed.

Not tested: token expiry over HTTP, which would need the server clock to be faked.

## Docker

```bash
cp .env.example .env    # set Jwt__SigningKey, e.g. openssl rand -base64 48
docker compose up --build
```

The API waits for SQL Server to be healthy, then migrates, seeds, and listens on http://localhost:5080 (`/swagger`, `/health`). To use the web app with it, run `VITE_API_PROXY_TARGET=http://localhost:5080 npm run web`.

**The image** (`apps/api/Dockerfile`, built from the repo root):
- It's multi-stage: the SDK builds the app and the aspnet image runs it. The project files are restored before the source is copied, so a code change reuses the restore layer.
- It runs as the non-root `app` user, over plain HTTP on port 8080.
- `.dockerignore` excludes build output, `node_modules`, `.git`, `.env`, the web app, the tests and `appsettings.Development.json`.
- It's 485 MB, compared with 1.36 GB for the SDK image.

**Config** comes from environment variables in `docker-compose.yml`: `ConnectionStrings__Default`, `Jwt__SigningKey`, `ASPNETCORE_ENVIRONMENT` (default `Development`) and `MSSQL_SA_PASSWORD`. I moved the SLA and JWT settings (issuer, audience, lifetime) from `appsettings.Development.json` to `appsettings.json`, because the Development file isn't copied into the image. They aren't secrets, and environment variables can still override them.

**Health:** `GET /health` returns 200 when the database is reachable and 503 when it isn't, and needs no token. The database check has a 5-second timeout; without it, the retry policy kept the request hanging for over a minute.

**Migrations:** the container migrates on startup only because it runs as Development. In production I'd run a migration bundle (`dotnet ef migrations bundle`) as a separate release step, so several instances don't race each other and the app doesn't need permission to change the schema. I'd also use a chiseled base image, a read-only filesystem, and secrets from a secret store.
