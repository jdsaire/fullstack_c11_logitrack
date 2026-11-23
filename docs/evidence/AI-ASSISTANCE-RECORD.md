# AI Assistance Record · LogiTrack

**System**: LogiTrack order management API
**Author**: Juan Diego S. (`jdsaire`)
**Scope**: Microsoft Full-Stack Developer certificate, Course 11 capstone, Parts 1 to 5
**Feeds**: Submission entry 3 (challenges, and how the AI assistant helped)

## Summary

17 course prompts, one entry each: 9 changed, 5 kept in part, 1 kept as suggested, 1 rejected, and 1 unnecessary
(the code already did what it asked). The rejected or corrected suggestions that mattered most: a role field on
registration that would let anyone become Manager (AI-08), the signing key kept in `appsettings.json` (AI-07), and a
cache that never cleared after a write (AI-10).

## How this record works

Each course part ends with suggested prompts for an AI assistant. Every prompt was run as written, and each one gets
one entry: the prompt verbatim, what the assistant suggested, what was kept, changed or rejected, and why. Choices
where the course page and a clean build disagreed are logged at the end of each part.

## Part 1 · Business logic and database

### Entry AI-01 · Part 1
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Generate EF Core code for one-to-many between Order and InventoryItem.
- **Suggested**: A nullable `OrderId` foreign key on `InventoryItem`, an `Items` collection on `Order`, a back-reference
  `Order` property on each item, and a fluent `HasMany`/`WithOne`/`HasForeignKey` configuration in `OnModelCreating`.
- **Verdict**: Changed.
- **Why**: Kept the nullable `OrderId`, the `Items` collection and the fluent configuration, with `OnDelete(SetNull)`
  so deleting an order releases its items instead of deleting stock. Dropped the back-reference: nothing reads it, and
  it would loop when the API returns an order with its items as JSON.
- **Result**: `Models/InventoryItem.cs`, `Models/Order.cs`, `Data/LogiTrackContext.cs`, migration `InitialCreate`.

### Entry AI-02 · Part 1
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Suggest a way to print Order summaries efficiently.
- **Suggested**: Build the line with one interpolated string instead of concatenation; for many orders, write them
  through a single `StringBuilder`; count items in the database query instead of loading every item just to count.
- **Verdict**: Changed.
- **Why**: Kept the single interpolated string, with the date formatted explicitly (`M/d/yyyy`, invariant culture) so
  the summary reads `4/5/2025` on any machine; the Mac this was built on uses a Peruvian locale. Rejected the
  `StringBuilder`: the method returns one line. Counting in the query belongs to the API's order list and is
  revisited in Part 4.
- **Result**: `Order.GetOrderSummary()`; culture test in `Part1ModelTests`.

### Entry AI-03 · Part 1
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Identify any performance improvements in my AddItem method.
- **Suggested**: Guard against a null item; skip an item already in the order; keep a `HashSet<int>` of item ids for
  constant-time duplicate checks.
- **Verdict**: Changed.
- **Why**: Kept the null guard and the duplicate skip. Rejected the `HashSet`: orders hold a handful of items, and a
  second collection would have to stay in step with the list EF Core tracks. The suggestion also missed a real case:
  unsaved items all have id 0, so matching by id alone would treat two new items as one. Unsaved items are matched by
  reference instead, and a test covers it.
- **Result**: `Order.AddItem()`; `AddItem_KeepsDistinctUnsavedItems`.

### Course page vs clean build · Part 1
- The page's command `dotnet new webapi -n LogiTrack cd LogiTrack` is two commands on one line; the project was
  created with controllers (`--use-controllers`) because Part 2 asks for controllers.
- The page's `OnConfiguring` connection string was replaced by a connection string in `appsettings.json`, passed in
  through dependency injection, which Part 2 requires.
- The page's seed block creates the context with `new LogiTrackContext()`; it runs at startup in Development instead,
  through the context the app already registers. It prints the page's line, then builds an order in memory, adds three
  items, removes one, and prints its summary.
- The page's "List (list of items in the order)" property is named `Items`.

## Part 2 · REST endpoints

### Entry AI-04 · Part 2
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Generate a GET endpoint to return an order by ID including item details.
- **Suggested**: `[HttpGet("{id}")]` loading the order with `.Include(o => o.Items)` and `FirstOrDefaultAsync`, returning
  `NotFound()` when missing and the order entity itself when found.
- **Verdict**: Changed.
- **Why**: Kept the eager load and the 404. The route takes an integer id only (`{id:int}`), and the endpoint returns
  a response object with the items and the order's summary line, rather than the database entity, so the API's
  shape doesn't change when the table does.
- **Result**: `OrderController.GetById`, `Dtos/OrderDtos.cs`.

### Entry AI-05 · Part 2
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Suggest how to handle error responses if an ID is not found.
- **Suggested**: Return `NotFound()` so the framework sends a standard 404 problem body; optionally add a custom message
  naming the missing id; add an exception-handling middleware for unexpected errors.
- **Verdict**: Changed.
- **Why**: Kept `NotFound()` on every route that takes an id. Skipped the custom message: the standard body is enough
  and stays consistent across routes. The exception handler moved to Part 3, where error bodies are reviewed for
  internal detail.
- **Result**: 404 paths in both controllers; `Part2RouteTests`.

### Entry AI-06 · Part 2
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Refactor this controller to use async methods.
- **Suggested**: `ToListAsync`, `FindAsync`, `FirstOrDefaultAsync` and `SaveChangesAsync` with `Task<ActionResult<T>>`
  return types; pass a `CancellationToken` into every query.
- **Verdict**: Unnecessary in part; one suggestion rejected.
- **Why**: Both controllers were written async from the start, so the refactor changed nothing. The cancellation token
  was rejected for this build: every query is a short local SQLite call.
- **Result**: no change.

### Course page vs clean build · Part 2
- An order is created from items already in stock: the body carries a customer name and the ids of items that aren't
  on another order. Unknown, repeated or already-ordered ids return 400. This keeps adding inventory a Manager task
  (Part 3), as ruled for this build. Deleting an order puts its items back in stock.
- The order list returns each order's item count; the order-by-id route returns the items themselves.
- The routes are set explicitly (`api/inventory`, `api/orders`) because the order controller's class name is singular.
- .NET 10's Web API template no longer includes a Swagger page, so Swagger UI comes from the Swashbuckle package, in
  Development only. The template's own OpenAPI package was removed: it pulled in a library version with a published
  high-severity advisory.

## Part 3 · Identity, tokens and roles

### Entry AI-07 · Part 3
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Add JWT authentication to an ASP.NET Core API.
- **Suggested**: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with issuer, audience,
  lifetime and signing-key validation; the signing key under `Jwt:Key` in `appsettings.json`; a token built from the
  user's id, email and roles; `UseAuthentication()` before `UseAuthorization()`.
- **Verdict**: Changed.
- **Why**: Kept the four validations and the claims. Moved the key out of `appsettings.json` into .NET user-secrets,
  with a startup check that stops the API if the key is missing or shorter than 32 bytes. The suggestion also missed
  an interaction: `AddIdentity` makes cookies the default scheme, so a call without a token got a redirect instead of
  a 401. Tokens are now the default for sign-in checks, challenges and refusals, and a test asserts 401 with no
  redirect on every route.
- **Result**: `Program.cs`, `Services/TokenService.cs`, `Services/JwtSettings.cs`; `Part3AuthTests`.

### Entry AI-08 · Part 3
- **Date**: 2026-11-22
- **Prompt (verbatim)**: How do I restrict a route to users with a specific role?
- **Suggested**: `[Authorize(Roles = "Manager")]` on the route, the role created with `RoleManager`, and a `role` field on
  the registration request so an admin account can be created through the API.
- **Verdict**: Changed.
- **Why**: Kept the attribute on adding inventory and on both deletes, and the role creation. Rejected the role field:
  anyone could register as a Manager. A startup seeder gives the role only to the account whose email is set in
  user-secrets, once that account registers. A test posts a role at registration and confirms it's ignored.
- **Result**: `Data/ManagerSeeder.cs`, both controllers; `Register_IgnoresAPostedRole`.

### Entry AI-09 · Part 3
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Suggest improvements to secure user registration.
- **Suggested**: Unique emails; strong password rules; lockout after repeated failed sign-ins; the same message for a
  wrong password and an unknown email; email confirmation; rate limiting on sign-in; HTTPS only.
- **Verdict**: Kept in part.
- **Why**: Kept unique emails, Identity's default password rules, lockout on failed sign-ins and generic messages (a
  taken email also gets a generic message, so registration doesn't reveal who has an account). Field rules on every
  request body were added at the same time, with 400s that carry no internal detail. Rejected email confirmation and
  rate limiting for this build: there is no mail service, and both sit outside the capstone's parts. HTTPS belongs to
  a deployment, which is out of scope; local runs use http on localhost.
- **Result**: `Controllers/AuthController.cs`, request models in `Dtos/`; `Part3ValidationTests`.

### Course page vs clean build · Part 3
- The page installs the SQLite package again; it was already in place from Part 1.
- The page names no JWT package; tokens use Microsoft.AspNetCore.Authentication.JwtBearer.
- Registration returns 201 without a token; login issues the token.
- Each order records the id of the signed-in user who created it (Part 5's guidance), from the token.

## Part 4 · Caching and query performance

### Entry AI-10 · Part 4
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Add in-memory caching to a GET request in ASP.NET Core.
- **Suggested**: `AddMemoryCache()`, `IMemoryCache` injected into the controller, `TryGetValue` then `Set` with a
  30-second absolute expiration around the inventory query, caching the entity list.
- **Verdict**: Changed.
- **Why**: Kept the 30-second absolute expiration. Used `GetOrCreateAsync` for one read path, and cached the response
  list rather than database entities. The suggestion never cleared the cache, so a new item would stay invisible for
  up to 30 seconds. Every write that changes the list now clears it: adding or deleting an item, and creating or
  deleting an order (both change an item's order id). Tests cover each write and the expiry.
- **Result**: `InventoryController.GetAll`, `Services/InventoryCache.cs`; `Part4CacheTests`.

### Entry AI-11 · Part 4
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Optimize Entity Framework queries in ASP.NET Core.
- **Suggested**: `.AsNoTracking()` on read-only queries; `.Select()` projections instead of loading whole entities;
  `.Include()` where related data is needed, to avoid one query per order; an index on the foreign key; compiled
  queries; pagination on list routes.
- **Verdict**: Kept in part.
- **Why**: Kept no-tracking reads everywhere, a projection for the inventory list, and a projection for the order list
  that counts items in SQL instead of loading every item. Order by id keeps `.Include()` because it returns the items.
  The foreign-key index already existed from the first migration. Rejected compiled queries (no measurable need at
  this size) and pagination (no course part asks for it; named as a limitation).
- **Result**: both controllers' read routes.

### Entry AI-12 · Part 4
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Suggest performance improvements in this controller.
- **Suggested**: For the inventory controller: sort in the database rather than in memory; add `[ResponseCache]` so
  browsers and proxies keep the list.
- **Verdict**: Kept in part.
- **Why**: Sorting already ran in SQL. Rejected HTTP response caching: every route needs a token, and a client-side copy
  would keep showing stale stock after a write, which the server cache avoids by clearing itself.
- **Result**: no change beyond AI-10 and AI-11.

### Entry AI-13 · Part 4
- **Date**: 2026-11-22
- **Prompt (verbatim)**: How can I avoid repeated DB calls for the same result?
- **Suggested**: Cache the result in memory; keep the cache key in one place so every controller clears the same entry;
  load several rows with one query instead of one lookup per id.
- **Verdict**: Kept.
- **Why**: The key and lifetime live in one class used by both controllers. Order creation already loads all requested
  items in one query.
- **Result**: `Services/InventoryCache.cs`, `OrderController.Create`.

### Entry AI-14 · Part 4
- **Date**: 2026-11-22
- **Prompt (verbatim)**: Improve the speed of this API endpoint.
- **Suggested**: For order by id: read without tracking, and split the order and its items into two queries
  (`AsSplitQuery`).
- **Verdict**: Changed.
- **Why**: Kept no-tracking. Rejected the split query: one order with a few items is cheaper as one joined query. A
  first single run showed no change (0.490 ms before, 0.498 ms after); the three-run sweep in Part 5 shows 0.502 ms
  before and 0.448 ms after, a difference close to run-to-run variation.
- **Result**: `OrderController.GetById`; `docs/evidence/PERFORMANCE-REPORT.md` (Part 5).

### Course page vs clean build · Part 4
- Timings are taken with a Stopwatch around in-process requests (no network) by an opt-in timing test, so the same
  measurement runs before and after the changes. The inventory GET also logs, with a Stopwatch, whether each response
  came from memory or the database.

## Part 5 · State and final review

### Entry AI-15 · Part 5
- **Date**: 2026-11-23
- **Prompt (verbatim)**: Review this codebase for redundant logic.
- **Suggested**: Merge the inventory list's inline projection with `InventoryItemResponse.From` into one mapping; drop the
  `bool` that `Order.RemoveItem` returns, since no route reads it; remove the order route's manual "no items" check now
  that a validation rule covers it.
- **Verdict**: Kept in part.
- **Why**: The manual "no items" check was already removed when validation rules arrived in Part 3. Kept the inline
  projection: the database needs an expression it can turn into SQL, and the shared method runs only in memory, so
  merging them would bring back loading whole rows. Kept `RemoveItem`'s result: Part 1's tests use it to show whether
  an item was in the order.
- **Result**: no further change.

### Entry AI-16 · Part 5
- **Date**: 2026-11-23
- **Prompt (verbatim)**: Suggest final performance improvements.
- **Suggested**: Response compression; caching order-by-id results too; a longer cache lifetime for the inventory list.
- **Verdict**: Rejected.
- **Why**: Payloads are a few kilobytes on localhost, so compression has nothing to save. Order by id measured 0.5 ms
  without a cache, and caching it would add another set of entries to clear on every write. The 30-second lifetime is
  what the course asks for.
- **Result**: no change.

### Entry AI-17 · Part 5
- **Date**: 2026-11-23
- **Prompt (verbatim)**: What else should I include in a production-ready API?
- **Suggested**: Health checks, rate limiting, structured logging, API versioning, HTTPS with HSTS, a CORS policy,
  documented error responses, a container image and a CI pipeline.
- **Verdict**: Kept in part.
- **Why**: Kept documented responses: Swagger now lists the 401 and 403 answers on protected routes and the 200 answer
  on both lists, which it had marked "undocumented" during the Part 5 walkthrough. The rest belongs to deploying and
  running a service, which this capstone doesn't do; each item is named as out of scope in the README.
- **Result**: `ProducesResponseType` attributes on both controllers.

### Course page vs clean build · Part 5
- State persistence uses the database strategy: SQLite is the source of truth, and each order stores the id of the user
  who created it (the page's "user tokens on the order" guidance). `RESTART-LOG.md` records a manual restart; a test
  repeats it on every run.
