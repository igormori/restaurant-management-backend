# Reject breached passwords

## Problem
Password rules check complexity only; a common leaked password like `Summer2026!` is accepted.

## Fix
- On register and password change, check the password against Have I Been Pwned using the k-anonymity range API (only a hash prefix leaves the server).
- Decide behaviour if the service is unavailable (allow, and log).
