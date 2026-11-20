# Fix a security problem the AI agent got wrong

You are a developer on a team that fixes security problems in an old health-benefits website.
An AI agent already tried to fix security problem **MYT-3108** (SQL injection in the claims search)
in `src/Claims/ClaimsRepository.cs`. Its tests pass. Your job: check its work, fix what is wrong,
and prove it. You may use your AI tool; we will watch how you use it.

## Files

- `FINDING.md`: the problem, and how the search must behave.
- `AGENT_NOTES.md`: what the agent says it did, and the code before its change.
- `src/Claims/ClaimsRepository.cs`: the code after the agent's change. You change this file.
- `tests/Claims.Tests/`: the agent's tests and `MyTests.cs` for yours.

## Start

```
dotnet test tests/Claims.Tests
```

## Tasks: 15 minutes in total (any order)

1. **Sorting (about 5 minutes).** Make the sort option safe: only the options in `FINDING.md` work,
   anything else is rejected with `ArgumentException`.
2. **Search (about 4 minutes).** Make the search behave as `FINDING.md` describes, without
   reopening the injection.
3. **Your tests (about 4 minutes).** In `MyTests.cs`, write at least two tests that pass on your
   code and would fail on the agent's version.
4. **Notes (about 2 minutes).** Fill in `NOTES.md`: what the agent proposed, what you changed and
   why, and your evidence.

If you are still on one task two minutes after its time, move to the next one.
At 15 minutes we stop. Then run:

```
dotnet run --project check
```

Do not edit the `check` folder.
