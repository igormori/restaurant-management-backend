# Restaurant System — Module Map

Oct 14, 2026 · @Igor Mori

## Overview

The system has 19 modules in 5 domains. Orders and Inventory are the hubs: every sale passes through Orders, and every stock change through Inventory.

&#91;embedded content: module map · 19 modules, 2 hubs\]

Not drawn, to keep it readable: Fiscal also brings supplier invoices into Purchasing, Finance also receives card payments and cash closings, and Customers attach to Orders.

## What was missing

Your list covers the salão well. The biggest gaps are recipes, stock movements and the central kitchen, which are exactly what the AI features depend on.

| Missing piece | Why it matters | Goes in |
| --- | --- | --- |
| Recipes (fichas técnicas) | Link each dish to its ingredients in grams; without them there is no CMV (cost of goods sold) or waste detection | Recipes & Production |
| Stock movements | Every stock change is a record: purchase, sale, waste, transfer, count adjustment | Inventory |
| Units and conversions | Buy a box of 12, store single units, use grams in recipes | Inventory |
| Inventory counts (contagem) | Physical count vs system; the gap is unexplained loss | Inventory |
| Waste log (registro de perdas) | Expired, dropped or burned items, so known loss is separated from unknown loss | Inventory |
| Lots and expiry dates (validade) | Near-expiry alerts and promotion suggestions | Inventory |
| Packaging (embalagens) | Delivery boxes and cups are a real cost; treat them as stock items | Inventory |
| Central kitchen production | Turn raw ingredients into sauces and portions, then transfer them to each store | Recipes & Production |
| Modifiers (adicionais) | "No onion", stuffed crust, doneness: they change the price, the stock used and the kitchen ticket | Catalog & Menu |
| Kitchen stations (praças) | Send each item to the right printer or screen: kitchen, bar, pizza oven | Kitchen |
| Payment details | PIX, card, cash, meal vouchers (vale-refeição), split bill, 10% service charge, discounts | Checkout & Payments |
| Cancellations with approval | A reason and manager approval on every cancelled item; the main anti-fraud control | Orders + Audit |
| Fiscal setup | Tax codes per product (NCM, CFOP, CST), tax regime, digital certificate, NFC-e series per CNPJ | Fiscal |
| Bills to pay and receive | Supplier bills with due dates; card payments that arrive later, minus fees | Finance |
| Audit log | Who cancelled, discounted or opened the cash drawer, and when | Platform |
| Printers and devices | Which printer serves which station and which cash register | Platform |

NCM, CFOP and CST are the tax classification codes every product needs on an invoice.

## Modules by domain

19 modules in 5 domains. Each can become its own `RestaurantManagement.Modules.<Module>` project; small ones can share a project at first.

### Platform

| Module | Status | Main tables | Connects to |
| --- | --- | --- | --- |
| Identity | Exists | Users, Roles, Permissions, RefreshTokens | Every module (who did what) |
| Organization | Exists | Organizations (tenants), Locations (store or central kitchen), Settings, FiscalProfiles | Every module: rows carry OrganizationId, most also LocationId |
| Audit | New | AuditLog (who, what, when, before and after) | Orders, Checkout, Cash and Inventory write to it |
| Devices | New | Printers, Terminals, StationPrinters | Kitchen, Checkout, Cash |

### Front of house (salão)

| Module | Status | Main tables | Connects to |
| --- | --- | --- | --- |
| Catalog & Menu | Partly exists (Menu, MenuCategories) | Products, ModifierGroups, Modifiers, Menus, MenuCategories, Prices (per location and channel) | Orders, Recipes, Inventory (resale items), Fiscal (tax codes) |
| Floor (Salão) | New | Areas (Floor 1, Inside, Outside), Tables | Orders |
| Orders | New | Orders (table, comanda, takeout, delivery), OrderItems, ItemModifiers, Cancellations, TableTransfers | Floor, Catalog, Kitchen, Checkout, Recipes, Customers |
| Kitchen | New | ProductionStations (kitchen, bar, oven), StationRouting, KitchenTickets | Orders, Devices |
| Customers | New | Customers (name, phone, CPF), Consents (LGPD), loyalty later | Orders, Fiscal (CPF on the invoice) |

### Money

| Module | Status | Main tables | Connects to |
| --- | --- | --- | --- |
| Checkout & Payments | New | Bills, BillSplits, Discounts, ServiceCharges, Payments (PIX, credit, debit, cash, meal voucher), Tips | Orders, Cash, Fiscal, Finance |
| Cash (Caixa) | New | CashRegisters, CashSessions (opening balance, expected vs counted), CashMovements (sales, sangria, suprimento) | Checkout, Finance |
| Fiscal | New | NfceDocuments, FiscalSeries, ProductTaxData, InboundNfe (supplier XMLs) | Checkout, Purchasing, Organization (CNPJ, certificate) |
| Finance | New | AccountsPayable, AccountsReceivable, Expenses, CardFees, IncomeStatement (DRE) | Purchasing, Checkout, Cash |

Sangria is cash taken out of the drawer, suprimento is cash added, and DRE is the income statement.

### Back of house

| Module | Status | Main tables | Connects to |
| --- | --- | --- | --- |
| Inventory | New | StockItems (ingredients, resale items, packaging), Units, UnitConversions, StockLevels, StockMovements, InventoryCounts, WasteLog, Lots | Purchasing, Recipes, Orders, Analytics |
| Recipes & Production | New | Recipes, RecipeItems, ProductionOrders, Transfers (central kitchen to stores) | Catalog, Inventory, Organization (the central kitchen is a Location) |
| Purchasing | New | Suppliers, SupplierItems (their code to your stock item), PurchaseOrders, PurchaseReceipts, PriceHistory | Inventory, Finance, Fiscal |

### Intelligence and integrations

| Module | Status | Main tables | Connects to |
| --- | --- | --- | --- |
| Analytics & AI | New | Forecasts, Alerts, Recommendations, WeeklyReports | Reads Orders, Inventory, Purchasing, Recipes |
| Notifications | New | NotificationRules, Notifications | Analytics, Inventory, Cash |
| Integrations | New | IntegrationAccounts, ExternalOrders (iFood), ImportJobs (Spice) | Orders, Catalog |

Later, not in the first build: Staff and shifts, Reservations, and Subscription billing for when you sell to other restaurants.

## Key business flows

Four flows cross most modules; if these work end to end, the system works.

**1. Order to cash (salão)**

1. The waiter opens an order on a table or comanda (Floor, Orders).
2. Each item goes to its station: kitchen, bar or oven (Kitchen).
3. The customer asks for the bill; split, service charge and discounts are applied (Checkout & Payments).
4. Payments are recorded by method in the open cash session (Cash).
5. The cashier issues the NFC-e when needed (Fiscal).
6. Stock is deducted using each dish's recipe (Recipes & Production, Inventory).
7. The sale feeds reports and forecasts (Analytics & AI).

**2. Purchase to stock**

1. A supplier invoice arrives: the NF-e XML is fetched automatically, or Claude reads a photo of a paper note (Fiscal, Purchasing).
2. Supplier item codes are matched to your stock items, and prices are updated (Purchasing).
3. Stock goes up at the receiving location (Inventory).
4. A bill to pay is created with its due date (Finance).
5. Price increases are flagged (Analytics & AI).

**3. Central kitchen to stores**

1. A store falls below its target stock, or the forecast predicts higher demand (Inventory, Analytics & AI).
2. A replenishment request goes to the central kitchen (Recipes & Production).
3. The central kitchen produces: ingredients out, prepared items in, each batch with a lot and expiry date (Recipes & Production, Inventory).
4. The transfer is sent and received, and stock moves between locations (Inventory).
5. Items close to expiry are flagged for a promotion (Analytics & AI).

**4. Cash close (fechamento)**

1. The cashier opens a session with the opening balance (Cash).
2. Sales, withdrawals (sangria) and additions (suprimento) are recorded during the shift.
3. At close, the cashier counts each payment method; the system compares expected vs counted and records the difference.
4. Several sessions per day are allowed, one per shift or cashier; the day's totals feed Finance.

## Key design decisions

Decided and locked; changing them after real data exists is expensive.

**Dishes and beverages are both Products.** A Product is what you sell; a Stock item is what you buy and store. They connect through a recipe or a one-to-one link:

| Example | Product type | How it uses stock |
| --- | --- | --- |
| Margherita pizza | Prepared | Recipe: flour, mozzarella, tomato sauce |
| Caipirinha | Prepared (bar) | Recipe: cachaça, lime, sugar |
| Coca-Cola can 350 ml | Resale | 1 can of the matching stock item |
| Lunch combo | Combo | The products inside it |

**Catalog and Menu are separate.** The catalog defines each product once; menus decide where it appears (salão, iFood, lunch), and prices can differ per location and channel. Your current MenuCategories belong on the menu side.

**Every table is tenant-scoped.** Rows carry OrganizationId, and most carry LocationId; EF Core Global Query Filters apply the filter to every query.

**The central kitchen is a Location.** Its type is "production", so stock, transfers and costs work exactly as they do in the stores.

**Fiscal settings live on the Location.** Mercado Municipal and Batel each have their own CNPJ, so each needs its own digital certificate and NFC-e series.

**Stock is a ledger of movements.** Never edit a stock level directly: every change is a movement, and the level is their sum. That is what makes waste detection and audits possible.

**Sensitive actions leave a trail.** Money uses `decimal`; cancellations and discounts need a reason and a permission, and all of them go to the Audit log.

## Build phases

Spice stays live until phases 1 and 2 are both done; the AI in phase 3 then builds on the stock data from phase 2.

| Phase | Goal | Modules |
| --- | --- | --- |
| 0. Foundations (now) | A secure base every feature inherits | Identity, Organization, Audit, tenant filters |
| 1. Run the salão | Build the salão operation, with Spice still live | Catalog & Menu, Floor, Orders, Kitchen, Checkout & Payments, Cash, Fiscal, Devices |
| 2. Control stock | Know your stock and dish costs, then switch off Spice | Inventory, Recipes & Production, Purchasing |
| 3. Intelligence | Forecasts, waste alerts and central kitchen replenishment | Analytics & AI, Notifications, Integrations (Spice history import) |
| 4. Grow | Ready to sell to other restaurants | Finance, Customers and loyalty, iFood, Staff, Reservations, Subscription billing |

We keep Spice live until phase 2 is complete, then switch over one unit at a time.
