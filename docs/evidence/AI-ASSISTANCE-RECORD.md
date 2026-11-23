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
