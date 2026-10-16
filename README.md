# RaceDay REST API — Part 2

A C# ASP.NET Core Web API starter implementation based on the Part 1 RaceDay ERD, SQL script, and endpoint plan. It uses Entity Framework Core, SQL Server, Swagger UI, session-cookie authentication, role checks, password hashing, and a basic xUnit test project.

## Requirements
- Visual Studio 2022 with the ASP.NET and web development workload, or .NET 8 SDK
- SQL Server LocalDB or a SQL Server instance
- Git and a GitHub account

## Run locally
1. Open `RaceDay.Api/RaceDay.Api.csproj` in Visual Studio.
2. Check `RaceDay.Api/appsettings.json` and change the `RaceDay` connection string if your SQL Server instance differs.
3. In the terminal, run:
   ```bash
   dotnet restore RaceDay.Api/RaceDay.Api.csproj
   dotnet run --project RaceDay.Api/RaceDay.Api.csproj
   ```
4. Open the Swagger URL printed in the terminal (normally `https://localhost:<port>/swagger`).
5. The EF Core context is configured for SQL Server. Create the database/schema using EF Core migrations (`dotnet ef migrations add InitialCreate` and `dotnet ef database update`) or adapt the supplied Part 1 SQL script to your setup. Do not run the Part 1 script and migrations against the same existing schema without checking first because the SQL script drops/recreates tables.

## Roles
- **Organiser:** create/update/cancel owned events, manage owned categories, view enrolments for owned events, and record/correct results for owned events.
- **Participant:** view events, manage their own profile, enrol in categories, cancel their own enrolments, and view their own results.
- Public routes include registration, login, event listing/details, categories and event results.

## Authentication
`POST /api/auth/register` registers a user and hashes the password with ASP.NET Core's `PasswordHasher`. For a real deployment, do not let the public choose the Organiser role without an approval/admin process; this coursework starter accepts both roles to demonstrate the brief. `POST /api/auth/login` establishes a server-side session cookie. `POST /api/auth/logout` clears the session. When testing in Swagger, keep the same browser session/cookies between login and protected requests. For a public deployment, use HTTPS and review cookie security, CSRF protection, session storage, rate limiting, and account-recovery requirements.

## API groups
- `/api/auth` — register, login, logout
- `/api/users` — own profile, profile lookup, own enrolments/results
- `/api/events` — list/filter, event details, event CRUD, categories, enrolments, results, weather and route
- `/api/categories` — update/delete category
- `/api/enrolments` — update/cancel enrolment
- `/api/results` — update/delete results

## Tests and CI
Run `dotnet test RaceDay.Tests/RaceDay.Tests.csproj`. The GitHub Actions workflow in `.github/workflows/dotnet.yml` restores, builds and runs tests on pushes and pull requests. The included tests cover password hashing and role-name validation; expand these with endpoint integration tests for registration/login, unauthenticated requests, role enforcement, event ownership, and enrolment recording before submission.

## Before submitting
- Build and run the project locally and fix any compiler/runtime issues.
- Confirm all endpoints and response descriptions in Swagger.
- Connect a real weather provider and route/map source, or clearly explain the implemented fallback; those two endpoints currently return placeholder information.
- Add comprehensive endpoint integration tests; current included tests are foundational only.
- Push at least 20 meaningful commits from your own GitHub account; do not manufacture commits in bulk.
- After a successful GitHub Actions run, add a real screenshot of the green workflow run here.
- Record an unlisted YouTube video showing the running API, code structure, roles, authentication and tests; add its URL here.
- Submit your own GitHub repository link on ARC.

## Part 1 documents
The `Part1-docs` folder contains the ERD, SQL Server script and API endpoint plan supplied with the assignment.
