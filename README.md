# LogiTrack

LogiTrack is an order management API for a logistics platform. It tracks inventory items across warehouses and the
customer orders that use them. I built it as the capstone of Course 11 (Deployment and DevOps) of the Microsoft
Full-Stack Developer certificate, in five parts: data model, endpoints, sign-in and roles, caching, and state that
survives a restart.

## Key features

- **Inventory and orders.** List, add and remove stock items. Create an order from items in stock, read it back with
  its items, and delete it to return the items to stock.
- **Sign-in and roles.** Accounts register and sign in to get a token, and every inventory and order route needs one.
  Only the Manager can add stock or delete anything. Every request body is checked, and bad input gets a clear 400.
- **Caching.** The inventory list stays in memory for 30 seconds and refreshes after any change.
- **State.** Data lives in SQLite, so orders and stock are still there after the API restarts. Each order records
  who created it.

## How to run locally

You need the .NET 10 SDK. From the repository root:

```bash
cd src/LogiTrack
dotnet user-secrets set "Jwt:Key" "<a random value of at least 32 characters>"
dotnet user-secrets set "Manager:Email" "<the email that should become Manager>"
dotnet run
```

Open http://localhost:5281/swagger. Register the Manager email to get the Manager role; every other account is a
regular user. Sign in, paste the token into **Authorize**, and try the routes. Run the tests with `dotnet test` from
the repository root.

## Results

Repeat requests for the inventory list went from 9.8 ms to 0.9 ms once served from memory, measured on my laptop. The
method, every run and the raw numbers are in [docs/evidence/PERFORMANCE-REPORT.md](docs/evidence/PERFORMANCE-REPORT.md);
the restart check is in [docs/evidence/RESTART-LOG.md](docs/evidence/RESTART-LOG.md).

## Known limitations

- Each stock item belongs to at most one order, so an order claims whole items instead of drawing a quantity from
  them. A real system would add order lines with quantities.
- List routes return every row; there's no paging.
- The Manager role goes to whoever registers the configured email; there's no email confirmation.
- Built and tested on a laptop only, with no deployment, HTTPS or monitoring.

## AI Assistance Disclosure

I built LogiTrack with Claude Code as my AI coding assistant. I ran each of the course's suggested prompts through it
and recorded every suggestion, including the ones I changed or rejected, in
[docs/evidence/AI-ASSISTANCE-RECORD.md](docs/evidence/AI-ASSISTANCE-RECORD.md). I reviewed each part before moving
to the next.
