# Lens: maintainability (`"lens": "maintainability"`)

Find design and performance problems that will cost the team later, introduced by the added lines.
Be selective: report only issues with a clear, concrete consequence.

Look for:
1. **Design flaws**: responsibility placed in the wrong layer, new circular or inverted
   dependency, leaky abstraction, duplicated business rule that must now be changed in two places
   (point at the duplicate), hidden global/static state, new hard-coded environment assumptions.
2. **Complexity hazards**: a function that now mixes many concerns, deep nesting that hides a bug
   path, boolean-flag parameters that fork behaviour, magic values whose meaning is not obvious.
3. **Performance with a trigger**: N+1 queries or I/O in a loop, unbounded collection or query
   (no paging/limit), repeated expensive work that could be hoisted, blocking calls on a hot or
   async path, accidental quadratic behaviour on realistic input sizes.
4. **Operability**: missing logging/metrics on a new failure path, no timeout/retry/backoff on a
   new network call, configuration that cannot be changed without a redeploy when it should.
5. **Compatibility**: public API/schema/migration changes that break existing consumers or running
   versions during a rolling deployment.

Never report formatting, naming taste, or "add comments". Never report something a linter rule would
flag unless it has a non-obvious consequence here.
