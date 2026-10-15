# Forgot / change password

## Problem
There is no way for a user to reset a forgotten password or change their password.

## Fix
- Forgot password: emailed single-use code with expiry and attempt limit (reuse the verification-code pattern); same response whether or not the email exists.
- Change password: requires the current password.
- Both revoke all sessions (depends on [session-redesign.md](session-redesign.md)).
- Rate limit the new endpoints like the other auth endpoints.
