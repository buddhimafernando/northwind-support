# Approach

## How I worked

I did the tasks in order and kept each one small enough to commit on its own: the defect fix and the filtering as separate commits, then the rules, backend, frontend and tests for Task 2, and the same for Task 3 and Task 4. I read the brief for each task first, wrote down anything ambiguous with the decision I made, and only then started on code.

I used AI tools throughout (details in the README). Antigravity helped me get around the codebase at the start. Claude Code did a lot of the writing and reviewing. For every change I read the diff, ran the tests and checked it by hand in Swagger, with curl or in the app before committing. When something looked more complicated than it needed to be, I asked for a simpler version or rewrote it.

## Task 1: Defect and filtering

- **How I found the cause:** I reproduced it with the Network tab open. Changing the search box sent a request, but changing Status or Priority sent nothing. That pointed at the hook that fetches the list, `useTicketList`. Its effect only listed `search`, `page` and `pageSize` as dependencies, so React never re-ran it when a dropdown changed. The filter state did update; it just never reached the API until the search text changed too.
- **Why I fixed it this way:** the effect now depends on the whole filter object, so any filter, including ones added later, triggers a fetch. Only the search text is debounced, because that's the one people type into; a dropdown should respond at once. Responses from older requests are ignored, so a slow early response can't overwrite a newer one. The regression tests fail on the old code.
- **Filtering:** the API accepted the filters but never used them. I added `ApplyFilters`, with one `if` and one `.Where` per filter on the `IQueryable`, before `CountAsync` and paging. That way the database does the work, the total count matches the filtered list, and adding a filter is one more `if`. `slaStatus` in Task 2 proved that.

## Task 2: Escalation and assignment

- **How I read the rules:** where the brief didn't say, I picked the reading that seemed fairest and wrote it down:
  - At exactly the due time a ticket is At risk, not Breached.
  - The 1-hour floor applies only to Premium, because that's how BR-3 is worded.
  - On escalation, the current owner can keep the ticket even at their limit, since it already counts towards their load. A new ticket needs an agent strictly below their limit.
  - Ties go to the lowest agent id, so the result never depends on load order.
- **Why the rules are domain classes:** create and escalate apply mostly the same rules (priority, SLA window, who gets the ticket), so they had to live in one place. The first AI-generated version spread them across interfaces with one implementation each, had the SLA hours in three places, and depended on an API DTO. It also had two bugs: an agent over their limit could keep a ticket, and At risk was never calculated. I replaced it with a few small classes (`SlaPolicy`, `SlaEvaluator`, `AgentSelector`, `TicketTriage`) that do no I/O and take "now" as a parameter, so they're easy to test at exact boundaries.
- **Hardest part:** the SLA window after an escalation. An escalation restarts the window, so "how much time is left" has to be measured from the latest escalation, not from creation. I read that with a `MAX(EscalatedAtUtc)` subquery instead of storing another column, so the history table stays the only source. The SLA filter was also tricky: the rule has to exist in SQL as well as in C#, and the first version used `DATEDIFF` in seconds, which could disagree with the displayed status right at the boundary. I rewrote it to compare "now" directly against the moment the ticket turns at risk, and checked the exact boundary in SQL Server.

## Task 3: Authentication

- **Column on Agents vs a Users table:** every user is an agent, one-to-one, with the same email and the same active flag. A Users table would only copy those and add a join. A `PasswordHash` column on `Agents` was simpler. If customers or admins ever need to sign in, that's the point to split it out.
- **Token lifetime and storage:** 60 minutes covers a working session without refresh tokens, which were out of scope, and limits how long a stolen token is useful. The frontend keeps the token in `sessionStorage`: it survives a refresh, is gone when the tab closes, and isn't shared across tabs. The downside is that any script on the page can read it, so an XSS bug could leak it. For production I'd move to an HttpOnly cookie with CSRF protection.
- **How I made sure every endpoint is protected:** I didn't put `[Authorize]` on each controller. I set a fallback authorization policy that requires a signed-in user everywhere, so a new endpoint is protected by default and only login and `/health` opt out. To check that the integration tests really test this, I removed the policy for a moment: six tests failed, including `/api/auth/me`, which has no attribute of its own. Failed logins all return the same 401, and unknown emails still run a password check against a dummy hash, so neither the message nor the timing reveals which emails exist.

## Task 4: Docker

- I copied the project files and ran `dotnet restore` before copying the rest of the source, so changing a `.cs` file reuses the cached restore layer. I checked that the build log shows `CACHED`.
- The runtime image runs as the built-in non-root `app` user over plain HTTP on port 8080. The final image is 485 MB, against 1.36 GB for the SDK image.
- `appsettings.Development.json` holds the local connection string, so it stays out of the image. That meant moving the SLA and JWT settings, which aren't secrets, into `appsettings.json`. The connection string and signing key come in as environment variables.
- The surprise was `/health`. With the database stopped, it hung for over a minute because the connection retry policy kept retrying. I gave the check its own 5-second timeout, so it now reports 503 quickly.
- The container migrates on startup only because it runs as Development. In production I'd run a migration bundle as a separate release step.

## What I'd do with more time

- The 2.5 SLA summary report.
- A SQL Server test container for the SLA filter, instead of checking it by hand.
- Full-text search: `LIKE '%…%'` won't scale to millions of tickets.
- Refresh tokens and a way to revoke them, an HttpOnly cookie for the token, and rate limiting on login.
- A lock or retry around assignment, so two tickets created at the same moment can't overload one agent.
