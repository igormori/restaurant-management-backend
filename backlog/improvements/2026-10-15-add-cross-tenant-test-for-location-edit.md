# Add a cross-tenant test for editing a location of another organization

**Found by:** implementer, while verifying the Overall DoD of organization-and-location-fields.

`PUT api/locations/{id}` has role-rejection tests (Manager, Cashier, Waiter, Kitchen) and a
cross-organization code-reuse test, but no test that an Owner of organization A who sends the id of
organization B's location gets 404 and B's location stays unchanged. Only the read path
(`GetLocationAsync_LocationOfAnotherOrganization_ReturnsNull`) is covered.

Add one service or Web test for the edit endpoint.
