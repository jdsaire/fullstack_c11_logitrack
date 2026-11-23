# Reflection · LogiTrack

Peer review submission for the Course 11 capstone, as submitted.

## 1 · Project Title

LogiTrack: Order Management API with Role-Based Access and a 30-Second Inventory Cache

---

## 2 · Describe your API project and its key features.

LogiTrack is an ASP.NET Core Web API for a logistics platform. It tracks inventory items across warehouses and the
customer orders that use them, and was tested route by route in Swagger.

Key features:
1. **Inventory and orders.** List, add and remove stock; create an order from items in stock and read it back with its
   items.
2. **Sign-in with tokens and a Manager role.** Every inventory and order route needs a token, and only the Manager can
   change stock or delete records.
3. **Input checks on every write**, with clear 400 answers that name the field.
4. **A 30-second memory cache** for the inventory list, refreshed after any change.
5. **Data that survives a restart**, with each order recording who created it.

Built with C#, Entity Framework Core on SQLite, JWT tokens, NUnit (69 automated tests) and an AI assistant.

---

## 3 · What were the major challenges you faced, and how did you overcome them?

- **Calls without a token got a redirect instead of a refusal.** Adding Identity made cookies the default, so the API
  sent callers to a login page that doesn't exist. I made tokens the default for every check, and tests now confirm a
  401 on all seven inventory and order routes.
- **The first cache never refreshed.** A new item stayed invisible for up to 30 seconds. Every write that changes the
  list now clears it, including creating or deleting an order, since that changes which order an item belongs to.
- **Modeling stock.** The course's one-to-many design means an item sits on one order at a time. I kept it, made orders
  take existing items only (so a regular user can't create stock through an order), and named it as a limitation.

**How the AI assistant helped.** I ran all 17 of the course's prompts through it and logged each answer. It sped up the
database relationship, token setup and cache code. It was wrong or unnecessary in places: it proposed a role field on
registration that would let anyone become Manager, kept the signing key in a config file, suggested a cache with no
refresh, and offered an async refactor for code that was already async. I rejected or corrected each one.

---

## 4 · How did you implement key components like business logic, data persistence, and state management?

- **Business logic.** `InventoryItem` (name, quantity, location) prints its details with `DisplayInfo()`. `Order`
  (customer, date, items) has `AddItem`, `RemoveItem` and `GetOrderSummary()`, which returns lines like
  `Order #1001 for Samir | Items: 2 | Placed: 4/5/2025`. One order holds many items; deleting an order returns them to
  stock.
- **Routes.** `/api/inventory` (list, add, delete), `/api/orders` (list, get one with its items, create, delete) and
  `/api/auth` (register, log in). All async, with a 404 for a missing id.
- **Persistence.** Two database migrations: the inventory and order tables, then the account tables.
- **State.** I chose the database strategy: the database is the source of truth, so nothing lives only in memory. I proved it
  twice: a test that stops and restarts the API on the same file, and a logged manual run where an order and its items
  came back unchanged after a restart. Each order stores the id of the user who created it. (Caching is in entry 6.)

---

## 5 · What security measures did you implement?

**Authentication**
- Accounts use ASP.NET Core Identity, which stores only password hashes and applies its default password rules.
- Login returns a signed token valid for 60 minutes. The API checks its issuer, audience, expiry and signature.
- Repeated failed logins lock the account for a while. A wrong password and an unknown email get the same message, and
  so does registering an email that's taken, so the API doesn't reveal who has an account.

**Role-based access**
- Every inventory and order route needs a token; without one the answer is 401.
- Manager only: adding stock, deleting items and deleting orders. Any signed-in user can read everything and create
  orders, and the API refuses the rest with 403.
- Registration has no role field. The Manager role goes only to the account whose email is set in the app's private
  settings, once it registers.

```csharp
[HttpPost]
[Authorize(Roles = ManagerSeeder.ManagerRole)]
public async Task<ActionResult<InventoryItemResponse>> Create(CreateInventoryItemRequest request)
```

**Input validation**
- Every request body has rules: required fields, 100-character limits, quantity from 0 to 1,000,000, 1 to 50 items per
  order, a valid email.
- Bad input returns 400 with the field names and nothing internal: no error traces, no database or parser messages.

**Secrets**
- The token signing key and the Manager email live in .NET user-secrets on my machine, never in the repo.
- The API won't start without a signing key of at least 32 bytes, and tests generate their own key on every run.
- GitHub secret scanning and push protection are on, and a scan of the full history found no key.

**Evidence**
- 69 automated tests, including a 401 on each of the 7 routes with no token and with a forged Manager token, a 403 for a
  regular user on every Manager route, a role posted at registration being ignored, and 14 invalid-input cases.
- A final check called 10 routes with no token, as a regular user and as the Manager: all 30 answers matched.

---

## 6 · How did you manage caching and optimize performance?

- **Caching.** The inventory list, the most-read route, stays in memory for 30 seconds. Any change to stock or orders
  clears it, so the next read is fresh. Tests cover a repeat read served from memory, the refresh after each kind of
  change, and expiry after 30 seconds.
- **Queries.** Reads skip change tracking, the lists select only the fields they return, the order list counts items in
  the database instead of loading them, and an order's items load with it in one query.
- **Measured with a Stopwatch** on my laptop (Apple M2, .NET 10, Release build), 200 requests per route, middle of 3
  runs, in milliseconds:

| Route | Before | After |
|---|---|---|
| Inventory list, repeat requests | 9.827 | 0.921 (from memory) |
| Inventory list, cache cleared | not cached | 4.026 |
| One order with its items | 0.502 | 0.448 |

The order lookup changed by about 0.05 ms, close to run-to-run variation. Full method and raw rows are in the repo's
`docs/evidence/` folder.
