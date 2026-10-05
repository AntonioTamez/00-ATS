# Lens: tests (`"lens": "tests"`)

Judge whether the **changed behaviour is protected**, and whether added tests are trustworthy.
Always use `"category": "testing"`.

Look for:
1. **Untested new behaviour**: a new branch, error path, validation or business rule in
   production code with no test among the changed files exercising it. Name the missing scenario
   precisely ("expired token returns 401"), report on the production line, `severity` medium at most.
2. **Weak or false-passing tests** (in added test code): assertions that cannot fail (`assert True`,
   asserting on the mock itself), no assertion at all, exceptions swallowed in the test, tests that
   pass regardless of the code under test, over-mocking that bypasses the logic being tested.
3. **Flaky patterns**: real clock/sleep, network or file system dependencies, shared mutable state
   or ordering dependencies between tests, random data without a seed.
4. **Changed behaviour with unchanged tests**: production semantics changed (default value, error
   type, return shape) while the existing expectations visible in the diff still encode the old one.
5. **Disabled coverage**: tests newly skipped/ignored/commented out without a reason.

If the diff contains no production code changes and no test changes, return an empty list. Do not
ask for tests for trivial changes (renames, comments, config values with no logic).
