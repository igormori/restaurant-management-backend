---
name: domain
description: Business knowledge of the RestaurantManagement product in plain language — what the product is, its glossary, modules, roles and standing business rules. Use when writing a spec, deciding scope, or checking whether a business question is already answered.
---

# Business domain

Plain business language only. No code, tables or class names.
Items marked **[confirm]** are not settled yet; treat them as open, not as rules.

## 1. The product
- A restaurant management system with built-in AI (artificial intelligence). It does not just
  record operations: it predicts, alerts and recommends actions from the restaurant's own data.
- **Core value:** money the owner can see. Detect ingredient waste (purchases vs. sales),
  forecast demand, recommend promotions for ingredients about to spoil, and read purchase
  invoices from a photo or file to fill the stock automatically.
- **Customers:** small and medium restaurants in Brazil, Canada and the USA (United States of
  America) from launch.
- **Pilot:** a family-owned Italian restaurant group in Curitiba with two locations.

## 2. Glossary
| Term | Meaning |
|---|---|
| Organization | The restaurant business (company or group). Owns all its data |
| Location | One physical restaurant (unit) of an organization |
| Ingredient | Something the restaurant buys and stocks (e.g. tomato, flour), with a unit of measure |
| Dish | An item on sale, made from ingredients in set quantities (its recipe) |
| Menu | A set of dishes offered, e.g. lunch menu, dinner menu, per location |
| Purchase invoice | The supplier's invoice for received goods (in Brazil, the "nota fiscal") |
| Stock movement | Anything that changes stock: purchase, sale, waste, adjustment |
| Waste | Ingredients lost (spoiled, dropped, over-portioned) |
| Order | What a table or customer asked for ("comanda") |
| Central kitchen | A planned kitchen that prepares items (sauces, portioned meat) for all locations |

## 3. Modules (business view)
| Module | Covers | Status |
|---|---|---|
| Identity | User accounts, access, roles | Built |
| Organization | Restaurant businesses, locations, settings | Built |
| Menu | Dishes, recipes (dish ↔ ingredients), menus | In progress |
| Inventory | Ingredients, purchase invoices, stock, low-stock alerts, waste | Planned (next) |
| Orders | Tables, orders, bill closing | Planned |
| Analytics | Sales per dish, margin per dish, demand forecast, waste detection | Planned |
| AI Intelligence | Recommendations ("buy more chicken", "chicken will be left over — promote it"), weekly insights | Planned |
| Staff | Employees, shifts | Planned |

Build order matters: no dish without ingredients, no menu without dishes.
Later: own point of sale with payments by PIX (Brazilian instant payment), card or cash; iFood
(delivery platform) orders going straight into the system. Brazilian electronic fiscal receipts
(NFC-e) are a known risk area.

## 4. Roles and permissions
- **Owner:** the only system role. Can do everything at every location. Cannot be edited or
  deleted. Every organization always keeps at least one Owner. Only an Owner can make someone
  else an Owner or remove that role.
- **Admin and Staff:** default roles created for every new organization. The Owner can edit or
  delete them. Admin: everything except organization settings. Staff: read-only on orders and menu.
- **Custom roles:** the Owner creates any role (Chef, Waiter, Cashier) with any mix of permissions.
  Visible only inside that organization.
- A user can have several roles at once; their permissions add up.
- A role applies to one location or to all locations of the organization. A role for location A
  gives nothing at location B.
- A user always has at least one role.
- The list of available permissions is fixed by the product; organizations cannot invent new ones.

## 5. Standing business rules
- An organization never sees or changes another organization's data.
- Nothing in use is deleted: locations are closed and users are deactivated, so history is kept.
- Each organization sells in one currency, chosen when it is created: BRL (Brazilian real),
  USD (US dollar) or CAD (Canadian dollar). No country default.
- Each location has its own time zone (Canada and the USA span several), used for its business
  day. No country default.
- Not launched yet: there is no real customer data.
- Units of measure for ingredients (grams, millilitres, units): **[confirm]** the full list.
