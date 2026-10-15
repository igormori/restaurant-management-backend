# Session redesign: logout, revocation, multiple devices

## Problem
- No logout: a stolen refresh token stays valid for 30 days.
- Refresh token reuse is not detected: if an attacker refreshes first, nothing is revoked.
- One refresh token per user: logging in on a second device logs out the first.
- Access tokens can't be revoked: password or role changes take effect only when the token expires.
- Refresh tokens are hashed with BCrypt, so they can't be looked up directly (the client must also send the email).

## Fix (one spec, one migration)
- New `RefreshTokens` table: token hash (SHA-256), user ID, device/user agent, created, expires, revoked, replaced-by.
- `POST /api/auth/logout` revokes the current refresh token.
- Reuse of a revoked token revokes all of that user's tokens.
- `TokenVersion` on `User`, added to the JWT; increase it on password change, deactivation or role change; reject JWTs with an old version.
