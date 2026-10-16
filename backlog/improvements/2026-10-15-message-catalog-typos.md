# Typos and wording issues in the message catalog

**Found during:** docs/plans/shared/2026-10-15-localize-client-messages/plan.md
**Date:** 2026-10-15

## Description
Existing messages in `src/RestaurantManagement.Shared/Localization/` have wording issues:
- `UserNotFound`: en "User not found!." and pt "Usuário não encontrado.." (extra punctuation)
- `InvalidToken`: pt "Token Invalido." (missing accent, odd capital: "Token inválido.")
- `TooManyRequests`: pt "Demasiados pedidos." is European Portuguese; Brazilian users expect "Muitas solicitações."

## Why it matters
These messages reach end users; sloppy text looks unprofessional, mostly to the Brazilian launch market.
