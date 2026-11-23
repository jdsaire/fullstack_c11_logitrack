# Performance report · caching and query changes

Every figure below is **[M]**, measured: run on the machine named here, with the method and date beside it. Nothing in
this file is estimated, so it has no **[SIM]** rows.

| | |
|---|---|
| Machine | Apple M2 (4 performance + 4 efficiency cores), 16 GB, macOS 26.6.2 |
| Runtime | .NET 10.0.5 (SDK 10.0.201), Release build |
| Method | Stopwatch around each request, in process (test host, no network), signed in as a plain user |
| Dataset | 2,000 inventory items; 200 orders × 5 items, in a SQLite file |
| Requests | 20 warm-up, then 200 timed per route (50 for forced cache misses); 3 runs each |
| Before | commit `e7b71cc` (Parts 1 to 3, no cache, tracked queries) |
| After | commit `0008d37` (30 s inventory cache, untracked and projected reads) |
| Date | 23 Nov 2026 |
| Raw rows | `timing-before.csv`, `timing-after.csv` |
| How to repeat | `dotnet test -c Release --filter TestCategory=Timing` |

## Median per run, in milliseconds

| Route | Before (runs 1 · 2 · 3) | After (runs 1 · 2 · 3) |
|---|---|---|
| GET /api/inventory, repeat requests | 9.827 · 13.021 · 9.292 [M] | 0.935 · 0.921 · 0.911 [M] (served from memory) |
| GET /api/inventory, cache cleared before each request | not cached | 3.942 · 4.026 · 4.129 [M] |
| GET /api/orders/{id} | 0.517 · 0.476 · 0.502 [M] | 0.453 · 0.448 · 0.445 [M] |

## Reading the table

- **Inventory list, repeat requests:** the middle run goes from 9.827 ms to 0.921 ms once the list is served from
  memory. Before run 2 sat about 33% above the other two, so all three runs are shown.
- **Inventory list, cache cleared:** 4.026 ms (middle run) against 9.827 ms before. This part of the change comes from
  reading without change tracking and selecting only the columns the response needs.
- **Order by id:** 0.502 ms to 0.448 ms (middle runs). The difference is about 0.05 ms, close to run-to-run variation.
  An earlier single run (22 Nov, 0.490 ms before, 0.498 ms after) showed no change.
- p95 values are in the raw rows.
