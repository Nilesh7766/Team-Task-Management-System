# Database design

SQL Server, created code-first by Entity Framework Core 8 from the entities in
`src/TaskManagement.Web/Models/Entities`.

## Diagram

```
        ┌────────────────────┐
        │       Teams        │
        │ Id (PK)            │
        │ Name (unique)      │
        │ Description        │
        │ ManagerId (FK) ────┼──┐  a team is led by one user
        │ CreatedAt          │  │
        └─────────┬──────────┘  │
                  │ 1           │
                  │             │
                  │ *           │
        ┌─────────┴──────────┐  │
        │       Users        │◄─┘
        │ Id (PK)            │
        │ FullName           │
        │ Email (unique)     │
        │ PasswordHash       │
        │ Role               │
        │ IsActive           │
        │ TeamId (FK, null)  │
        └──┬────┬────────┬───┘
     1     │    │ 1      │ 1
           │    │        │
     *     │    │ *      │ *
┌──────────┴─┐  │  ┌─────┴──────────┐
│ Tasks      │  │  │ Notifications  │
│ Id (PK)    │  │  │ Id (PK)        │
│ Title      │  │  │ UserId (FK)    │
│ Description│  │  │ Title, Message │
│ Status     │  │  │ Type           │
│ Priority   │  │  │ TaskItemId     │
│ DueDate    │  │  │ IsRead         │
│ AssignedToUserId (FK, null)        │
│ CreatedByUserId  (FK)              │
│ TeamId (FK, null) ──► Teams        │
│ CreatedAt / UpdatedAt / CompletedAt│
└──────┬─────────────────────────────┘
       │ 1
       │ *
┌──────┴───────┐
│  Comments    │
│ Id (PK)      │
│ TaskItemId FK│
│ UserId FK    │
│ Text         │
│ CreatedAt    │
└──────────────┘
```

## Relationships

| From | To | Kind | On delete |
|---|---|---|---|
| Team | Users (members) | one-to-many | set member `TeamId` to null |
| Team | Manager (User) | many-to-one | no action |
| Task | AssignedToUser | many-to-one, optional | no action |
| Task | CreatedByUser | many-to-one, required | no action |
| Task | Team | many-to-one, optional | set null |
| Task | Comments | one-to-many | cascade |
| Comment | User | many-to-one | no action |
| Notification | User | one-to-many | cascade |

`NoAction` is used on the user-facing foreign keys so SQL Server does not reject the schema with multiple
cascade paths through `Users`. Deletions that need cleanup are handled in the service layer instead
(`TeamService.DeleteAsync` detaches members and tasks before removing a team).

## Indexes

- `Users.Email` — unique; it is the login key.
- `Teams.Name` — unique.
- `Tasks.Status` and `Tasks.DueDate` — the dashboard and the filters sort and count on both.

## Enums

Stored as integers.

| Enum | Values |
|---|---|
| `UserRole` | Admin = 1, Manager = 2, User = 3 |
| `TaskState` | ToDo = 0, InProgress = 1, Done = 2 |
| `TaskPriority` | Low = 0, Medium = 1, High = 2 |
| `NotificationType` | TaskAssigned = 0, TaskStatusUpdated = 1, TaskCommented = 2, TeamMembershipChanged = 3 |

## Seed data

On first run the app creates one admin, two managers, two users, two teams and four tasks. See the README
for the credentials.
