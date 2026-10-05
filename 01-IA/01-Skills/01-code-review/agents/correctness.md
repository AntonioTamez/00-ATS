# Lens: correctness (`"lens": "correctness"`)

Find logic and reliability defects introduced by the added lines.

Look for, in this order:
1. **Wrong results**: off-by-one, inverted conditions, wrong operator/precedence, wrong variable
   used (copy-paste), integer overflow/truncation, unit or timezone mix-ups, incorrect default.
2. **Null/empty/boundary handling**: dereference of a value that can be null/None/undefined,
   empty collection, zero, negative, very large input.
3. **Error handling**: exceptions swallowed, errors converted to success, partial failure leaving
   inconsistent state, missing rollback/cleanup, missing `await`/unobserved promise/task.
4. **Resource handling**: files, connections, locks or subscriptions not released on every path.
5. **Concurrency**: shared mutable state without synchronisation, check-then-act races,
   non-atomic read-modify-write, async misuse (blocking in async, fire-and-forget).
6. **Contract changes**: a changed signature, return shape, status code or serialized field that
   existing callers visible in the diff still rely on.
7. **Removed code**: lines marked `-` that removed a validation, guard, retry or cleanup without a
   replacement among the `+` lines (report on the nearest added line, only if verifiable).

Do not report security issues (the security lens does) or test gaps (the tests lens does), unless
the defect is also a plain correctness bug.
