# Resend email failure log drops the exception

**Found during:** docs/plans/identity/2026-10-15-hash-verification-codes/plan.md
**Date:** 2026-10-15

## Description
`VerificationService.ResendVerificationCodeAsync` catches email-send failures and calls `_logger.LogError("Failed to send verification email to {Email}", ...)` without passing the caught exception. `RegistrationService` passes `ex` for the same failure.

## Why it matters
The error is logged and sent to Sentry with no stack trace or inner cause. That makes it hard to diagnose SMTP (Simple Mail Transfer Protocol) failures behind the 500 response on resend.
