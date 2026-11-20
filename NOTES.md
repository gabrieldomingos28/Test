# MYT-3108 notes

## What the agent proposed

The agent made the search text a query parameter, which fixed the injection reported by the scanner
(`' OR 1=1 --`). The rest of its change was wrong or did not help:

- **Sort option still injectable.** `sortBy` was still written into the SQL after `ORDER BY`. The agent
  only removed `'` and `;`, which blocks nothing: values such as `amount, (SELECT name FROM members)` or
  a `CASE WHEN ... END` expression run as SQL and can read other members' data one character at a time.
  On SQL Server (production) statements do not even need `;` to be chained. It also accepted any column,
  such as `submitted_at`, while FINDING.md allows only `date`, `amount` and `status`.
- **Search broken.** It switched to exact matching ("so nobody can list claims by guessing letters"),
  so `dental` found nothing. FINDING.md says the search must find claims that *contain* the text.
  This is not a security control: the query is already limited to the member's own claims.
- **Apostrophes stripped.** Removing `'` from the search text as "defense in depth" breaks `O'Brien`
  and adds no protection once the value is a parameter.
- **Tests confirmed the wrong behaviour.** `Search_ExactMatchOnly_BlocksWildcardEnumeration` expects
  `dental` to return nothing. No test covered injection through the sort option.

## What I changed and why

- **New `ClaimsSearchQueryBuilder`.** The SQL is built in one place from fixed pieces only.
  `ClaimsRepository.Search` keeps its signature and calls the builder.
- **Sorting uses allowlists.** A column name cannot be a parameter, so `sortBy` is looked up in a fixed
  map (`date` -> `submitted_at`, `amount`, `status`) and `sortDir` in (`asc`, `desc`). Anything else
  throws `ArgumentException`. User input never reaches the SQL text. `id` is added as a tie-breaker so
  paging is stable.
- **Every value is a parameter:** member id, search text, page size (`@limit`) and offset.
- **Search matches "contains", in any case,** with `LIKE '%' || text || '%'` built in the parameter
  value. `%`, `_` and `\` in the user's text are escaped (`ESCAPE '\'`), so `100%` finds only the claim
  that really contains "100%".
- **Removed the apostrophe stripping,** so `O'Brien` works.
- **Paging:** a negative page is treated as 0, and the offset is computed as `long` so a huge page
  number cannot overflow.
- **Tests:** `MyTests.cs` (30 test cases, public API only) and `ClaimsSearchQueryBuilderTests.cs`
  (21 test cases for the builder). Added `coverlet.msbuild` to measure coverage.
- **Left as is:** `AgentTests.cs`. Its exact-match test fails on the fixed code because it expects
  behaviour that contradicts FINDING.md; it should be deleted or rewritten to expect claims 101 and 103.
- **Still to check outside this repo:** that callers pass the logged-in member's id, not one from the
  request, and that the SQL Server version of this query gets the same allowlist + parameters fix.

## Evidence

- `dotnet run --project check`: Sorting 4/4, Search 4/4, extra "100%" check passes.
  My tests: 30/30 pass on my code, and 20 of 30 fail on the agent's version.
- `dotnet test tests/Claims.Tests`: 51 of my tests pass. The only failure is the agent's
  `Search_ExactMatchOnly_BlocksWildcardEnumeration` (wrong expectation, see above).
- Coverage (`/p:CollectCoverage=true /p:Threshold=90 "/p:Include=[Claims]*"`): 100% line,
  100% branch, 100% method for the `Claims` project.
- Tests that fail on the agent's version and pass on mine include: `dental` finds 101 and 103;
  `O'Brien` finds 102; `100%` finds only 104; `sortBy` values `submitted_at`, `id`,
  `amount, (SELECT name FROM members)`, `amount;DROP TABLE claims` and a `CASE WHEN` expression all
  throw `ArgumentException`.
