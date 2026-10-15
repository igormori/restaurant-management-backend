# Organization module

Inferred from code; no spec exists yet for this module.

New organizations are always created on a hardcoded TRIAL plan (30 days, 1 location),
never driven by the request body.
A user who already owns any organization on a TRIAL plan cannot create another one.
DeleteLocationAsync is a soft delete: it sets Status to Closed and never removes the row.
The Organization entity is in Entities/Organization.cs and shares its name with the
module's own namespace segment, so it must stay referenced as Entities.Organization
in the DbContext and services to avoid ambiguity.
Accepted gaps are tracked in docs/follow-ups.md; don't fix them as drive-by changes.
