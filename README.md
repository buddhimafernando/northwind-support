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

**What was built:**
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

**What was built:**
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
