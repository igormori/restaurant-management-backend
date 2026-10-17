# Mask secrets in CI test reports once the job gets secrets

**Status:** proposed
**Severity:** medium
**Origin:** docs/reviews/identity-security-matrix.md · Watch out 6
**Date:** 2026-10-16

## Problem
The CI test report prints the first 600 characters of every failure message into GitHub
annotations and the run Summary, without masking. Today that exposes nothing new: the build job
gets no secrets, test data is fictional, and the same messages are already in the job log. It
becomes a leak as soon as the job is given a secret (a connection string, a JWT key, a Sentry
DSN), because some failure messages include whole exception texts, and annotations are visible
to everyone who can see the pull request.

## Evidence
- `.claude/scripts/report-test-results.py` — copies failure messages into annotations and the Summary unmasked
- `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs` — startup tests assert on `ToString()` of startup exceptions, which can contain configuration values
- `.github/workflows/dotnet.yml` — no `secrets.*` used yet

## Acceptance criteria
- [ ] Before any `secrets.*` value reaches the build job, each one is masked with `::add-mask::` before the test step
- [ ] A failure message that contains a masked value shows `***` in the annotation and the Summary
- [ ] The workflow's own comments say why the mask step must come first

## Related
None
