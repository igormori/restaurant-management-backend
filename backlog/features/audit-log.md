# Audit log for auth events

## Problem
There is no record of logins, failures, lockouts or role changes to investigate an incident.

## Fix
- Record: login success/failure, lockout, refresh, logout, password change, role change.
- Store user ID, event, timestamp, IP, user agent; never passwords, tokens or codes.
