# Walkthrough running order (5–8 minutes)

A shot list for the recording. Keep two browsers open (one normal, one private) so you can be signed in as
two roles at the same time.

**0:00 — What it is (30s)**
One ASP.NET Core 8 project holding the REST API and the Razor UI, SQL Server through EF Core, three roles.

**0:30 — Sign in as admin (45s)**
`admin@tms.com`. Walk the dashboard: workload strip, status counts, past-deadline count, next deadlines.

**1:15 — Teams and people (1m)**
Teams page → open a team → add a member. People page → change someone's role → show that the last admin
cannot be demoted.

**2:15 — Manager creates and assigns a task (1m15s)**
Sign in as `manager@tms.com` in the second browser. Create a task, set priority and deadline, assign it to
a team member. Point out that the manager's list is scoped to their team.

**3:30 — The user side (1m15s)**
Sign in as `user1@tms.com`. Show the notification about the new assignment. Open the task, move it to
In Progress, add a comment.

**4:45 — Back to the manager (45s)**
Refresh: the status change and the comment both produced notifications. Show `App_Data/notifications.log`
for the mock emails.

**5:30 — Filters (30s)**
Tasks page: filter by status, priority and deadline range; toggle past-deadline only.

**6:00 — The API (1m15s)**
`/swagger`: POST `/api/auth/login` → copy the token → Authorize → GET `/api/tasks` as a manager, then as a
user, to show the same role scoping. Try a POST `/api/tasks` with the user token to show the 403.

**7:15 — Code and tests (45s)**
Services folder (rules live here, not in controllers), then `dotnet test` passing.

Close with the repository link.
