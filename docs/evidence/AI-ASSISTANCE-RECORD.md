# AI Assistance Record · LogiTrack

**System**: LogiTrack order management API
**Author**: Juan Diego S. (`jdsaire`)
**Scope**: Microsoft Full-Stack Developer certificate, Course 11 capstone, Parts 1–5
**Feeds**: Submission entry 3 (challenges, and how the AI assistant helped)

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
