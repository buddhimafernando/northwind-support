# Approach

## How I worked
[Order of work, how you used the 2 days, how you used AI, commit-as-you-go]

## Task 1: Defect and filtering
- How I found the cause: [what you looked at first, how you narrowed it to the dependency array]
- Why I fixed it this way: [debounce only search, ignore stale responses]
- Filtering: [why one Where per filter before Count/paging]

## Task 2: Escalation and assignment
- How I read the rules: [anything ambiguous and what you decided]
- Why the rules are domain classes: [shared by create and escalate, easy to test]
- Hardest part: [e.g. the SLA window after escalation / the SQL filter]

## Task 3: Authentication
- Column on Agents vs Users table: [your reasoning]
- Token lifetime and storage: [why 60 min, why sessionStorage, what you'd change]
- How I made sure every endpoint is protected: [fallback policy + the mutation check]

## Task 4: Docker
- [restore caching, non-root, why settings moved, the /health timeout surprise]

## What I'd do with more time
[e.g. 2.5 SLA report, full-text search, refresh tokens, HttpOnly cookie]
