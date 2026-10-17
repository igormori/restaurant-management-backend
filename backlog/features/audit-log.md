# Audit log for auth events

## Problem
There is no record of logins, failures, lockouts or role changes to investigate an incident.

## Fix
- Record: login success/failure, lockout, refresh, logout, password change, role change.
- Also record refused access (403s, choosing an organization the user is not a member of) and
  verification or reset code lockouts (gap report summary #9 and #13, ASVS V16.3.1–V16.3.3).
- Store user ID, event, timestamp, IP, user agent; never passwords, tokens or codes.
