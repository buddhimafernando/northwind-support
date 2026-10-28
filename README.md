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
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. **Filters are accepted but not applied yet** (Task 1). |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
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

## Defect fix and server-side filtering

### 1.1. DEFECT-117: filters sometimes do not take effect

**Root cause:** The useTicketList effect debounced all fetching and restricted its dependency array to [filters.search, filters.page, filters.pageSize], omitting dropdown filter properties (status, priority, category etc). BEcause of this, updating a dropdown filter never triggered an API request until a search term change forced a reload.
**Fix:** the fetch now depends on the complete set of filters, so any single filter change sends an immediate request (including future ones such as SLA status). Only the search text is debounced (300 ms), and responses from outdated requests are ignored.
**How to verify:** run `npm test` (the regression tests fail on the original code), or open DevTools - Network, change Status, and see a request at once.

### Server-side filtering (Task 1.2)

**Root cause:** `GET /api/tickets` bound the filter values (search, status, priority, categoryId, customerId, assignedAgentId, unassignedOnly) into `TicketQuery`, but `TicketQueries.GetPagedAsync` never used them, so it counted, sorted and paged every ticket.
**Fix:** Added `ApplyFilters`, which chains one `.Where` per supplied filter (so they combine with AND) on the `IQueryable` before `CountAsync` and paging. totalCount and totalPages now reflect the filtered set, and nothing is filtered in C#. A new filter, such as SLA status, is one more `if` block in `ApplyFilters`.
**How to verify:** Run `npm run api` and test endpoints with filter combinations in Swagger (`https://localhost:7043/swagger`) or the web app.

## Ticket escalation & assignment

### 2.1. Business rules

**Root cause:** The first AI-generated version worked but was hard to follow. It had an interface with only one implementation. The SLA hours were written in three places (defaults in a class, a fallback `switch` and a second copy in `appsettings.json`). The escalation checks existed twice. The rules depended on `AgentDto`, which is an API response object. It also had two bugs:
- An agent over their open-ticket limit still counted as eligible to keep a ticket (`>` where `>=` was needed).
- At-risk status was never calculated, because the SLA evaluator was called without an SLA window.

**Fix:** The rules now live in a few small, pure classes in the domain layer, and ticket creation and escalation share them:
- `SlaPolicy` (BR-2, BR-3, BR-6) is read from the `Sla` section of `appsettings.Development.json` and checked at startup. A missing or invalid value stops the app instead of falling back to numbers hidden in code.
- `SlaEvaluator` (BR-8) takes "now" and the at-risk threshold as parameters, so the status is always derived and never stored.
- `AgentSelector` (BR-4, BR-5) has `Choose`, used on create, and `KeepOrChoose`, used on escalation. It works on `AgentCandidate`, a small domain record, not on API objects.
- `TicketTriage` (BR-1) puts the rules together and returns a `TriageDecision` with a readable reason for the priority, the SLA window and the assignment.
- `Ticket.EnsureCanBeEscalated()` (BR-7) is the only place that blocks escalating a Critical, Resolved or Closed ticket.

**Decisions made:**
- **Tie-break:** when agents have the same number of open tickets, the one with the lowest id wins, so the result doesn't depend on load order.
- **Keeping the current owner on escalation:** they may keep the ticket even at their limit (for example 6/6), because this ticket is already one of their open tickets. To take a new ticket, an agent must be strictly below their limit.
- **Due vs Breached:** a ticket is Breached only once "now" is strictly after the due time. At the exact due time it is At risk.
- **1-hour floor:** applies only to Premium customers, as BR-3 is written.
- **Category rules:** come from the `RequiresSpecialist` and `ForcesCriticalPriority` flags on the category. No category names are hard-coded.

**Skipped (will be done in 2.2):**
- The escalation history row, which needs the `TicketEscalations` table and its migration.
- Calling the triage from `POST /api/tickets` and the escalate endpoint.
- Measuring the SLA window from the last escalation. For now the at-risk check measures from `CreatedAtUtc`.

### 2.2. Backend

**Fixes:**
- **Triage on create:** `POST /api/tickets` loads the customer's tier, the category's flags and every agent's workload in one query, then applies `TicketTriage.ForNewTicket`. The response is the ticket detail (priority, assigned agent or null, due date, SLA status) plus a `triage` object with the three reasons. When nobody is eligible the ticket is still created (201) and left unassigned, with the reason.
- **Escalate:** `POST /api/tickets/{id}/escalate` with `{ reason, escalatedBy }` returns `{ ticket, escalation }`.
  - 400: a reason shorter than 5 or longer than 500 characters once trimmed, or a missing `escalatedBy`.
  - 404: unknown ticket.
  - 409: the ticket is Critical, Resolved or Closed. The message says which.

  `Ticket.Escalate` creates the history row itself, from the ticket's own values before and after, so the row can't disagree with the change.
- **History:** `GET /api/tickets/{id}/escalations` returns the history newest first. Each row has the before and after priority, agent and due date, plus the reason, who and when. Unknown ticket: 404.
- **SLA filter:** `GET /api/tickets?slaStatus=AtRisk` (or `NotApplicable`, `WithinSla`, `Breached`, `Met`). The rule is translated into a SQL condition and added as one more `if` in `ApplyFilters`, so filtering, sorting, counting and paging all happen in the database.

**Decisions made:**
- **When the SLA window starts:** an escalation recomputes the due date from the moment of escalation (BR-7), so the window starts at the latest escalation, or at creation if there is none. This is read with a `MAX(EscalatedAtUtc)` subquery rather than stored as an extra column on `Tickets`, so the history table is the only place that records it.
- **SLA rule written twice:** the rule exists in C# (`SlaEvaluator`, which produces the status shown) and as a SQL condition (`TicketQueries.HasSlaStatus`, which filters). Both have a comment pointing at the other. SQL Server's `DATEDIFF` works in whole seconds, so a ticket within one second of the at-risk boundary could be filtered differently from the status it displays.
- **Create response shape:** the response stays the ticket detail with an extra `triage` field, rather than a new wrapper object. That keeps the current web form working unchanged. `triage` is left out of every other response.
- **Delete behaviour:** every foreign key on `TicketEscalations` restricts deletes. It is an audit trail, so deleting a ticket or an agent must not silently remove history. Escalation rows are never updated: the entity has no methods and only private setters.
- **No wasted queries on a refused escalation:** the handler checks whether the ticket can be escalated before loading customers, categories or agents, so a 409 costs one query.
- **Validation limits:** the 5–500 reason limits and the 100-character `escalatedBy` limit are constants on `TicketEscalation`. The validator and the EF mapping both use them, so the numbers are never repeated.

**Database:** migration `AddTicketEscalations` adds the `TicketEscalations` table:
- Columns: `Id`, `TicketId`, `FromPriority`, `ToPriority`, `FromAgentId`, `ToAgentId`, `FromDueAtUtc`, `ToDueAtUtc`, `Reason` (500), `EscalatedBy` (100), `EscalatedAtUtc`.
- Check constraints: priority must be 1–4, and `ToPriority = FromPriority + 1`.
- **Index `IX_TicketEscalations_TicketId_EscalatedAtUtc` (TicketId, EscalatedAtUtc DESC):** chosen deliberately, because one index serves three things: the foreign key lookup, the newest-first history, and the "latest escalation" subquery that the ticket list runs for every row.
- EF also adds its default indexes on `FromAgentId` and `ToAgentId`, so checking those foreign keys never scans the table.

**How it was verified:**
- 89 unit tests, including the escalate and create handlers and the reason validator at 4, 5, 500 and 501 characters.
- Manual run against SQL Server:
  - A Security ticket from a Premium customer requested as Low came back Critical, with a 2 h window and a specialist assigned.
  - Escalating Low → Medium → High → Critical worked, and the next escalation returned 409. Escalating a Resolved ticket returned 409 naming the status.
  - The history came back newest first.
  - For each `slaStatus` value, the filtered count matched the number of rows showing that status, and the five counts added up to the total.

**Known limitations:**
- **Concurrent requests:** two tickets created at the same moment can both be given to an agent who only had room for one, because workloads are read and then written without a lock.
- **SLA filter tests:** the filter uses SQL Server's `DATEDIFF`, which the Sqlite test database doesn't support, so it was verified manually against SQL Server rather than with unit tests.

### 2.3. Frontend

**Fixes:**
- **One SLA indicator:** `SlaIndicator` replaces `SlaCell` and is used by both the ticket list and the ticket page. Each of the five states has its own words and symbol as well as its own colour (Within SLA ◷, At risk !, Breached ✕, Met ✓, No SLA –), so the state never depends on colour alone. The labels live in `features/tickets/sla.ts`, and the filter dropdown uses the same ones.
- **SLA filter:** a new "SLA" dropdown in the filter bar. Because of the DEFECT-117 fix, it needed nothing beyond the new `slaStatus` field and one more query parameter: changing it sends a request immediately, and a test checks this.
- **Escalation panel** on the ticket page:
  - Fields for the reason and who is escalating.
  - Client validation in `features/tickets/escalation.ts` mirrors the server: 5–500 characters measured after trimming, a required name of up to 100 characters. Invalid input never calls the API.
  - The button shows a pending state while the request runs, and any server error, including 409, is shown under the form.
  - On success, the ticket updates from the response and the history reloads, without a full page reload.
- **Escalation history:** lists each escalation's priority, agent and due-date change (shown as "before → after"), the reason, who and when. It loads through its own hook, `useTicketEscalations`, and has loading, error and empty states.
- **Triage result after create:** the confirmation shows the priority, due date and assigned agent, each with its reason. When nobody was eligible it shows "Unassigned" with the reason.
- **Blocked escalations:** the escalate form is disabled for Critical, Resolved and Closed tickets, with a sentence saying why. A rejection from the server is still shown if the ticket changed in another tab.

**Decisions made:**
- **"Escalated by":** this is a text field until Task 3, when it will come from the login token instead.
- **After a 409:** the ticket on screen is left as it was and the server's message is shown. The reason the user typed is kept, so they don't lose it.
- **History refresh:** the history is reloaded from the server after an escalation, rather than adding the returned row locally, so the list always matches the database.

**Tests (Vitest):**
- A reason that is too short once trimmed blocks submission, and the API is never called.
- A 409 is shown, and the ticket is left unchanged.
- The SLA indicator renders all five states with different text and a different style.
- The escalate form is disabled, with an explanation, for Critical, Resolved and Closed tickets.
- The SLA filter sends a request immediately.

### 2.4. Tests

**How to run:** `npm test` runs both suites. `dotnet test` runs only the backend (xUnit + Moq); `npm run test --workspace apps/web` runs only the frontend (Vitest + React Testing Library).

**Backend:** all files are under `apps/api/tests/SupportDesk.UnitTests`. Tests that depend on time use `FixedClock`, and tickets are built with `TicketBuilder`.

| Requirement | Where |
|---|---|
| SLA window for every priority and tier, including the 1 h floor (below it, exactly at it, and Standard not floored) | `SlaPolicyTests` |
| Forced-Critical categories override a requested priority, or a missing one | `TicketTriageTests` |
| Agent selection: fewest open tickets, specialists only, limits, inactive agents, deterministic ties, nobody eligible | `AgentSelectorTests` |
| Escalation goes up exactly one level from Low, Medium and High, with the due date recomputed from the moment of escalation (both tiers) | `TicketTriageTests`, `EscalateTicketCommandHandlerTests` |
| Owner kept when still eligible (even at their limit); reassigned when inactive or not a specialist; left unassigned when nobody is eligible | `AgentSelectorTests`, `TicketTriageTests`, `EscalateTicketCommandHandlerTests` |
| History row records before and after for priority, agent and due date, plus the trimmed reason, who and when | `TicketTests`, `EscalateTicketCommandHandlerTests` |
| Rejections: Critical, Resolved and Closed (the message names which); unknown ticket; nothing saved and agents not loaded | `TicketTests`, `TicketTriageTests`, `EscalateTicketCommandHandlerTests` |
| Reason length at 4, 5, 500 and 501 characters, measured after trimming; missing actor | `EscalateTicketRequestValidatorTests` |
| SLA status at exact boundaries: due exactly now, one tick past due, exactly 25% left, just over 25% left, resolved exactly at due or one tick late | `SlaEvaluatorTests` |

**Frontend:** all files are under `apps/web/src/features/tickets`.

| Requirement | Where |
|---|---|
| A short reason (checked after trimming) blocks submission and never calls the API | `EscalationPanel.test.tsx` |
| A 409 is shown and the ticket is unchanged: checked in the panel, and on the whole page (priority unchanged, ticket and history not refetched) | `EscalationPanel.test.tsx`, `TicketDetailPage.test.tsx` |
| The SLA indicator renders every state with its own words, symbol and style | `SlaIndicator.test.tsx` |
| Escalation is disabled, with a reason given, for Critical, Resolved and Closed tickets | `EscalationPanel.test.tsx` |
| History shows priority, agent and due changes, "(unchanged)" when the owner was kept, and an empty state | `EscalationHistory.test.tsx` |
| Triage result shows the reasons, including the "unassigned because…" case | `TriageResult.test.tsx` |
| The SLA filter sends a request immediately | `useTicketList.test.ts` |

**Deliberately not tested:**
- **The SQL `slaStatus` filter:** it uses SQL Server's `DATEDIFF`, which the Sqlite test database can't run. It was checked by hand against SQL Server (see 2.2).
- **EF mapping and the migration:** covered by the existing `PersistenceModelTests`, plus the manual run.

## Authentication

### 3.1. Backend

**Development login:** every seeded agent signs in with their email (for example `alex.turner@northwind-support.example`) and the password **`Test_password`**. This password is a local fixture: it exists only in the development seed and is hashed when the seed runs. Yuki Tanaka is inactive, so she can't sign in. A database seeded before passwords existed gets this password on the next start. If your local database was seeded with an earlier development password, run `docker compose down -v` to re-seed it.

**What was built:**
- `POST /api/auth/login` (open to everyone) returns `{ accessToken, expiresAtUtc, agent: { id, fullName, email } }`. A wrong email, a wrong password and an inactive agent all get the same 401 with the same message, `Invalid email or password.` A malformed request gets 400.
- `GET /api/auth/me` returns the signed-in agent, or 401. It reads the agent from the database rather than trusting the token, so an agent deactivated since signing in is refused.
- Every other endpoint requires a valid token. This is enforced in one place, by an authorization **fallback policy**, so a new controller is protected without any extra code; endpoints opt out with `[AllowAnonymous]`. Login and Swagger stay open, and Swagger has an Authorize button for pasting a token.
- `POST /api/tickets/{id}/escalate` no longer accepts `escalatedBy`. The name is taken from the token's `name` claim, so a client can't record someone else as the escalator.

**Decisions made:**
- **Credentials live in a `PasswordHash` column on `Agents`, not a separate Users table** (migration `AddAgentPasswordHash`). Every user is an agent, one-to-one, with the same email, the same active flag and nothing else to hold. A Users table would add a join and a second copy of the email and the active flag, for no benefit. If customers or administrators ever sign in, that would be the point to split it out.
- **Hashing:** ASP.NET Core Identity's `PasswordHasher` (PBKDF2 with HMAC-SHA512, a random salt for every password, 100,000 iterations). When a hash was made with older settings, it is upgraded on the next successful login.
- **Same response for every failure:** an unknown email still runs one password check against a decoy hash, so it takes as long as a wrong password and the timing doesn't reveal which emails belong to agents.
- **Token:** an HMAC-SHA256 JWT with the claims `sub` (agent id), `name` and `email`, valid for **60 minutes** (`Jwt:AccessTokenMinutes`). An hour covers a working session without a refresh token (out of scope), and limits how long a stolen token is useful. Issuer, audience, signature, algorithm and expiry are all checked, with 30 seconds of allowed clock difference. Tokens are issued and checked from the same `JwtOptions`, so the two can't disagree.
- **Signing key:** `Jwt:SigningKey` appears in no appsettings file and nowhere in the code. In development it lives in user-secrets (stored outside the repository); anywhere else it is the `Jwt__SigningKey` environment variable, listed in `.env.example`. The API refuses to start if the key is missing or shorter than 32 bytes.

**What would change for production:**
- Keep the signing key in a secret store, and rotate it.
- Use short-lived access tokens together with refresh tokens, plus a way to revoke tokens.
- Lock accounts or rate-limit after repeated failed logins.
- Require HTTPS everywhere.

**Known limitations:**
- **No rate limiting or lockout,** so passwords can be guessed online.
- **Tokens can't be revoked before they expire.** `/me` turns away a deactivated agent, but the other endpoints only check that the token is valid, so a deactivated agent's token keeps working for up to an hour.
- **Long names:** `EscalatedBy` is limited to 100 characters, while agent names can be up to 200. An agent with a very long name couldn't escalate.

### 3.2. Frontend

**What was built:**
- **`/login` page (AU-9):** labelled email and password fields with the right `autocomplete` hints. Client validation (an email is required and must look like one; a password is required) stops bad input before any request. The button shows "Signing in..." while the request runs, and rejected credentials show the server's message, `Invalid email or password.`, as an alert.
- **Token attached to every call (AU-10):** `api/client.ts` adds `Authorization: Bearer …` to every request, so no feature code deals with the token.
- **Protected routes (AU-11):** every route except `/login` sits under `RequireAuth`. A signed-out visitor is redirected to `/login`, and after signing in is returned to the page they asked for, query string included; with no such page, they land on the ticket list.
- **Header (AU-12):** shows "Signed in as Priya Nair" and a "Sign out" button that clears the session.
- **Any 401 signs out (AU-13):** a 401 on a request that carried a token clears the session. `RequireAuth` follows the session, so the user is redirected to `/login` straight away, and back to where they were after signing in again. The app also calls `GET /api/auth/me` once per visit, so an expired token, or an agent deactivated since signing in, is caught on arrival rather than at the first action.
- **Escalation:** the "Escalated by" field is gone; the panel shows who the escalation will be recorded under, from the session.

**Where the token is kept, and the trade-off:** in `sessionStorage`, through `auth/session.ts`. React components read it via `useSyncExternalStore`, so every component updates together when someone signs in or out.
- **Chosen because:** the session survives a page reload (memory alone would sign you out on every refresh), it is cleared when the tab is closed, and it isn't shared with other tabs or kept in long-lived storage the way `localStorage` would be.
- **The cost:** any script running on the page can read it, so a cross-site scripting bug could steal the token for up to its 60-minute lifetime. React's escaping and the absence of `dangerouslySetInnerHTML` keep that risk low, but they don't remove it.
- **The stronger alternative:** an `HttpOnly`, `Secure`, `SameSite` cookie set by the API, which scripts can't read. That needs CSRF protection and a cookie-based authentication scheme on the API, which is more than this task needs. It is the first thing to change for production.
- **Stale requests:** a slow request that comes back 401 only clears the session if that session is still the current one, so it can't sign out an agent who has signed in again since.

**Tests (Vitest):**
- The client attaches the token, sends none when signed out, clears the session on a 401, and doesn't clear a newer session.
- An unauthenticated visitor is redirected to login and returned to the page they wanted (path and query) after signing in.
- Clearing the session mid-visit redirects to login.
- The login page blocks invalid input without calling the API, shows the error for rejected credentials, and shows a pending state.

### 3.3. Tests

**How to run:** `npm test` runs everything. `dotnet test` runs the backend: unit tests, the new integration tests and the architecture tests. No Docker or SQL Server is needed for any of them.

**Integration tests over HTTP (`apps/api/tests/SupportDesk.IntegrationTests`):** these test the authentication setup itself, not just the code behind it. `WebApplicationFactory` hosts the real `Program` in memory, with the same middleware, JWT validation and fallback policy as a normal run. Only two things differ: the database is an in-memory SQLite database, and the configuration is test-only, including a new random signing key on every run.

| Behaviour | Test |
|---|---|
| The right password returns a token, its expiry and the agent | `Login_WithTheRightPassword_…` |
| A wrong password, an unknown email and an inactive agent all get the identical 401, `Invalid email or password.`, with no token | `Login_WrongPasswordUnknownEmailAndInactiveAgent_…` |
| `/api/tickets`, `/api/agents`, `/api/customers`, `/api/categories` and `/api/auth/me` are 401 without a token and 200 with one | `ProtectedEndpoint_Is401WithoutAToken_And200WithOne` |
| A token with a tampered signature is rejected | `ProtectedEndpoint_RejectsATamperedToken` |
| The escalation is recorded against the agent in the token, even when the body names someone else | `Escalate_RecordsTheAgentFromTheToken_…` |

**Checking the tests can fail:** I removed the fallback policy temporarily. Six tests failed, including `/api/auth/me`, which has no `[Authorize]` attribute of its own and relies on the central policy. With the policy back, all of them pass.

**Unit and frontend tests from 3.1 and 3.2:**
- Password hashing and verification with the real `PasswordHasher`: the hash is salted, never contains the password, and verification passes and fails correctly (`LoginCommandHandlerTests`).
- `/me` refuses an agent deactivated since signing in (`GetCurrentAgentQueryHandlerTests`).
- On the frontend: the client attaches the token and clears the session on a 401; visitors are redirected to login and back; and the login form validates, shows rejected credentials and shows a pending state (`client.test.ts`, `RequireAuth.test.tsx`, `LoginPage.test.tsx`).

**Deliberately not tested:**
- **Token expiry over HTTP:** it would need either a 60-minute wait or the server's clock replaced. Expiry is enforced by the JWT handler's standard lifetime check, and the frontend's response to the resulting 401 is tested.
- **The SQL Server provider:** the integration tests use SQLite. The SQL Server-specific SLA filter was checked by hand (see 2.2).
