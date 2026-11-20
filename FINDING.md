# Security problem MYT-3108: SQL injection in the claims search

**Source:** the client's security scanning tool, confirmed by the client's security team ·
**Type:** SQL injection (CWE-89) · **Severity:** High

**What happens:** `ClaimsRepository.Search` built its SQL by joining the search text and the sort
option into the query. Searching for `' OR 1=1 --` returned the claims of **other members**.

**Who uses it:** the claims page of the member website, and the support agents' tool.

**How the search must behave (product rules, unchanged):**

- It returns only the claims of the given member.
- It finds claims whose description **or** provider **contains** the text, in any case.
  For example, `dental` finds "Dental cleaning" and "Dental X-ray".
- Names with apostrophes must work: `O'Brien` finds "O'Brien Eye Clinic".
- `sortBy` is one of `date`, `amount` or `status`. Anything else is rejected with `ArgumentException`.
- `sortDir` is `asc` or `desc`. 20 results per page.

**To close it:** the fix merged through the pipeline, tests that prove it, and a new scan
that no longer reports the problem.
