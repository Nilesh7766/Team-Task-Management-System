# Team Task Management System

A role-based task management application built as a single ASP.NET Core 8 project: the REST API and the
server-rendered web UI ship together, backed by SQL Server through Entity Framework Core.

Admins run the organisation, managers run their teams, and everyone else works through the tasks assigned
to them. Assignments and status changes notify the people involved.

\---

## 1\. Technology stack

|Layer|Choice|
|-|-|
|Backend|ASP.NET Core 8 (MVC + Web API in one project)|
|Database|SQL Server (LocalDB / SQL Server Express / full SQL Server)|
|ORM|Entity Framework Core 8 (code first, `EnsureCreated` + seeding on startup)|
|Auth (API)|JWT bearer tokens, HMAC-SHA256 signed, 20 minute expiry|
|Auth (UI)|Cookie authentication, sliding 120 minute expiry|
|Passwords|PBKDF2 / HMAC-SHA256, 100,000 iterations, per-user random salt|
|Frontend|Razor views + one hand-written responsive stylesheet (no framework, no npm)|
|API docs|Swagger / OpenAPI at `/swagger`, plus a Postman collection|
|Tests|xUnit with the EF Core in-memory provider|
|CI|GitHub Actions: restore, build, test, publish|

No Docker and no React are used — the UI lives inside the same .NET project, as required.

\---

## 2\. Running it

### Requirements

* .NET SDK 8.0
* SQL Server: LocalDB, Express, or a full instance

### Steps

```bash
git clone <your-repo-url>
cd TaskManagementSystem

# point the app at your SQL Server instance (see below), then:
dotnet restore
dotnet run --project src/TaskManagement.Web
```

Open https://localhost:44395/ (or whichever port the console prints). The database is created and seeded
automatically on first run.

From Visual Studio: open `TaskManagement.sln`, set **TaskManagement.Web** as the startup project, press F5.

### Connection string

`src/TaskManagement.Web/appsettings.json` → `ConnectionStrings:DefaultConnection`

```jsonc
// SQL Server Express (default in this repo)
"Server=localhost\\\\SQLEXPRESS;Database=TaskManagementDb;Trusted\_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"

// LocalDB
"Server=(localdb)\\\\MSSQLLocalDB;Database=TaskManagementDb;Trusted\_Connection=True;TrustServerCertificate=True"

// SQL authentication
"Server=localhost,1433;Database=TaskManagementDb;User Id=sa;Password=Your\_password123;TrustServerCertificate=True"
```

### Optional: EF Core migrations

The app calls `EnsureCreated()` so it works out of the box. To use migrations instead:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/TaskManagement.Web
dotnet ef database update --project src/TaskManagement.Web
```

Then swap `EnsureCreatedAsync()` for `MigrateAsync()` in `Data/DbInitializer.cs`.

\---

## 3\. Sample credentials

|Role|Email|Password|What they can do|
|-|-|-|-|
|Admin|`admin@gamil.com`|`Admin@123`|Everything: teams, people, roles, all tasks|
|Manager|`manager@gamil.com`|`Manager@123`|Platform Engineering: create and assign tasks, manage members|

\---

## 4\. What the roles can do

|Capability|Admin|Manager|User|
|-|:-:|:-:|:-:|
|See every task|yes|own teams only|assigned only|
|Create / edit / delete tasks|yes|own teams only|no|
|Change task status|yes|own teams only|own tasks only|
|Comment on a visible task|yes|yes|yes|
|Create / delete teams|yes|no|no|
|Add or remove team members|yes|own teams only|no|
|Change someone's role|yes|no|no|
|Deactivate an account|yes|no|no|

Rules are enforced in the service layer (`Services/TaskService.cs`, `TeamService.cs`, `UserService.cs`), so the
UI and the API cannot drift apart. Controllers add a second gate with `\[Authorize(Roles = ...)]` and the
`AdminOnly` / `ManagerOrAdmin` policies.

\---

## 5\. Features

**Authentication and authorization**

* Registration and login for both the UI (cookies) and the API (JWT).
* Self sign-up always produces a `User`; only an admin token can mint a Manager or Admin.
* Token and cookie expiry are both configurable under `Jwt:ExpiryMinutes`.
* Passwords are never stored or logged in plain text.

**Tasks**

* Title, description, status (To Do / In Progress / Done), priority, deadline, assignee, team.
* Status shortcuts on the task page; full edit form for managers and admins.
* Comment thread per task.

**Teams**

* Admins create teams and appoint a lead; leads add and remove members.
* Team page shows members and the team's tasks.

**Notifications**

* Triggered on task assignment, status update, new comment, and team changes.
* Every notification is stored in the database and shown in the app, and an email is produced.
* `Notifications:Mode` is `Mock` by default: emails are written to the log and to
`App\_Data/notifications.log` instead of being delivered. Set it to `Smtp` and fill in
`Notifications:Smtp` to send real mail.

**Dashboard**

* Status counts for the tasks in the caller's scope, plus a past-deadline count.
* A proportional workload strip, next deadlines, recent activity, teams, and latest notifications.
* The task list filters by status, priority, assignee, deadline range, free text, and overdue only.

\---

## 6\. API

Interactive docs: **`/swagger`**. Postman collection: `docs/TaskManagement.postman\_collection.json`.

All `/api` endpoints except register and login need `Authorization: Bearer <token>`.

|Method|Route|Who|
|-|-|-|
|POST|`/api/auth/register`|anyone|
|POST|`/api/auth/login`|anyone|
|GET|`/api/auth/me`|any signed-in user|
|GET|`/api/tasks`|any (scoped by role; filters: `status`, `priority`, `assignedToUserId`, `teamId`, `dueFrom`, `dueTo`, `search`, `onlyOverdue`)|
|GET|`/api/tasks/summary`|any|
|GET|`/api/tasks/{id}`|any with access|
|POST|`/api/tasks`|Admin, Manager|
|PUT|`/api/tasks/{id}`|Admin, Manager (User may send status only)|
|PATCH|`/api/tasks/{id}/status`|assignee, owner, Admin|
|DELETE|`/api/tasks/{id}`|Admin, Manager|
|GET/POST|`/api/tasks/{id}/comments`|any with access|
|GET|`/api/teams`, `/api/teams/{id}`|any (scoped)|
|POST|`/api/teams`|Admin|
|PUT|`/api/teams/{id}`|Admin, owning Manager|
|DELETE|`/api/teams/{id}`|Admin|
|POST|`/api/teams/{id}/members`|Admin, owning Manager|
|DELETE|`/api/teams/{id}/members/{userId}`|Admin, owning Manager|
|GET|`/api/users`|Admin|
|GET|`/api/users/assignable`|any|
|PATCH|`/api/users/{id}/role`|Admin|
|PATCH|`/api/users/{id}/team`|Admin, owning Manager|
|PATCH|`/api/users/{id}/active`|Admin|
|GET|`/api/notifications`, `/api/notifications/unread-count`|any|
|PATCH|`/api/notifications/{id}/read`, `/api/notifications/read-all`|any|

Errors come back in one shape:

```json
{ "status": 403, "message": "You can only assign tasks to members of your own teams.", "traceId": "..." }
```

`400` validation or business rule, `401` missing or expired token, `403` wrong role, `404` not found,
`500` unexpected — handled in `Middleware/ApiExceptionMiddleware.cs`.

### Quick check with curl

```bash
TOKEN=$(curl -s -X POST https://localhost:7175/api/auth/login -k \\
  -H "Content-Type: application/json" \\
  -d '{"email":"manager@tms.com","password":"Manager@123"}' | jq -r .token)

curl -k -H "Authorization: Bearer $TOKEN" https://localhost:7175/api/tasks
```

\---

## 7\. Database design

Five tables. See `docs/DATABASE.md` for the diagram and the column list.

* **Users** — `Id`, `FullName`, `Email` (unique), `PasswordHash`, `Role`, `IsActive`, `TeamId?`
* **Teams** — `Id`, `Name` (unique), `Description`, `ManagerId?`
* **Tasks** — `Id`, `Title`, `Description`, `Status`, `Priority`, `DueDate?`, `AssignedToUserId?`, `CreatedByUserId`, `TeamId?`, timestamps
* **Comments** — `Id`, `TaskItemId`, `UserId`, `Text`, `CreatedAt`
* **Notifications** — `Id`, `UserId`, `Title`, `Message`, `Type`, `TaskItemId?`, `IsRead`, `CreatedAt`

Relations: a team has many members and many tasks; a user has assigned tasks, created tasks, comments and
notifications; a task has many comments. Deleting a task cascades to its comments; deleting a team detaches
its members and tasks instead of deleting them.

\---

## 8\. Tests

```bash
dotnet test
```

Around 25 xUnit tests over the business rules that matter: password hashing and verification, role scoping
of the task list, who may create, assign, move and comment on tasks, team name uniqueness and lead
validation, protection of the last admin account, and manager boundaries when moving people between teams.

\---

## 9\. Project layout

```
TaskManagement.sln
src/TaskManagement.Web/
  Controllers/            MVC controllers for the UI
  Controllers/Api/        REST API controllers
  Data/                   AppDbContext, seeding
  Helpers/                Claims extensions, domain exceptions
  Middleware/             JSON error handling for /api
  Models/Entities/        EF Core entities
  Models/Dtos/            API request and response shapes
  Models/ViewModels/      Form and page models
  Services/               Business rules: auth, tasks, teams, users, notifications
  Views/                  Razor pages and layouts
  wwwroot/css/site.css    The whole stylesheet
tests/TaskManagement.Tests/
docs/                     Database notes, API notes, Postman collection, demo script
.github/workflows/        CI pipeline
```

\---

## 10\. Deliverables checklist

* \[x] GitHub repository with the backend and the frontend organised in one solution
* \[x] README with setup steps, sample credentials, and the stack
* \[x] Swagger docs and a Postman collection
* \[x] Unit tests
* \[x] GitHub Actions CI
* \[ ] Deployed demo link — add yours here
* \[ ] 5–8 minute walkthrough video — add the link here (`docs/DEMO\_SCRIPT.md` has a running order)

