# Restart log · state survives an API restart

**What this shows**: orders and inventory live in the SQLite file, not in memory, so stopping and starting the API
loses nothing (Part 5, database strategy). An automated test proves the same thing on every run
(`tests/LogiTrack.Tests/Part5RestartTests.cs`).

**Run**: 23 Nov 2026 (local time -05), Apple M2, macOS 26.6.2, .NET 10.0.5, Development build, fresh database, every
call made through Swagger UI at `http://localhost:5281/swagger`. Test accounts were created for this run with
generated passwords that weren't kept.

## Before the restart

| Time | Call | Result |
|---|---|---|
| 22 Nov 23:59:21 | Register and sign in the Manager account | 201, 200 |
| 23:59:24 | Manager adds Hand Truck, then Stretch Wrap | 201 (item 2), 201 (item 3) |
| 23:59:26 | Register and sign in a plain user | 201, 200 |
| 23:59:28 | Plain user creates an order for Samir with items 2 and 3 | 201, order 1, created by this user |
| 23:59:29 | GET /api/orders | 200, order 1 with 2 items |

## Restart

| Time | Step |
|---|---|
| 23:59:38 | API process stopped; port 5281 closed; database file `logitrack.db` (118,784 bytes) left in place |
| 23:59:50 | API started again on the same file |

## After the restart

| Time | Call | Result |
|---|---|---|
| 23:59:51 | Plain user signs in again | 200, same user id as before |
| 23:59:52 | GET /api/orders | 200, order 1 for Samir, 2 items, same date |
| 23:59:53 | GET /api/inventory | 200: Pallet Jack (no order), Hand Truck (order 1), Stretch Wrap (order 1) |
| 23:59:53 | GET /api/orders/1 | items Hand Truck and Stretch Wrap; creator id unchanged |
