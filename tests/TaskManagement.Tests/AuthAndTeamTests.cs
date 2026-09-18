using TaskManagement.Web.Helpers;
using TaskManagement.Web.Models.Dtos;
using TaskManagement.Web.Models.Entities;
using TaskManagement.Web.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TaskManagement.Tests;

public class AuthServiceTests
{
    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "TestSigningKeyTestSigningKeyTestSigningKey123!",
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:ExpiryMinutes"] = "60"
        }).Build();

    [Fact]
    public async Task Register_stores_a_hashed_password_and_logs_the_user_in()
    {
        var db = TestDb.Create();
        var hasher = new Pbkdf2PasswordHasher();
        var service = new AuthService(db, hasher, new JwtTokenService(Config()), new FakeNotificationService());

        var user = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "New Joiner",
            Email = "New.Joiner@Test.com",
            Password = "Passw0rd!"
        });

        Assert.Equal("new.joiner@test.com", user.Email);
        Assert.NotEqual("Passw0rd!", user.PasswordHash);

        var result = await service.LoginAsync(new LoginRequest { Email = "new.joiner@test.com", Password = "Passw0rd!" });
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("User", result.User.Role);
    }

    [Fact]
    public async Task Self_signup_cannot_grant_itself_the_admin_role()
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        var user = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Sneaky",
            Email = "sneaky@test.com",
            Password = "Passw0rd!",
            Role = UserRole.Admin
        });

        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public async Task An_admin_can_create_a_manager_account()
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        var user = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "New Manager",
            Email = "newmanager@test.com",
            Password = "Passw0rd!",
            Role = UserRole.Manager
        }, callerRole: UserRole.Admin);

        Assert.Equal(UserRole.Manager, user.Role);
    }

    [Fact]
    public async Task Duplicate_email_addresses_are_rejected()
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(new RegisterRequest
        {
            FullName = "Copy",
            Email = "admin@test.com",
            Password = "Passw0rd!"
        }));
    }

    [Theory]
    [InlineData("short1!")]      // too short
    [InlineData("alllowercase1!")] // no uppercase
    [InlineData("ALLUPPERCASE1!")] // no lowercase
    [InlineData("NoDigitsHere!")]  // no digit
    [InlineData("NoSpecialChar1")] // no special character
    public async Task Weak_passwords_are_rejected_on_registration(string weakPassword)
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        await Assert.ThrowsAsync<DomainException>(() => service.RegisterAsync(new RegisterRequest
        {
            FullName = "Weak Password",
            Email = "weak@test.com",
            Password = weakPassword
        }));
    }

    [Fact]
    public async Task Registering_sends_a_welcome_notification()
    {
        var db = TestDb.Create();
        var notifications = new FakeNotificationService();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), notifications);

        var user = await service.RegisterAsync(new RegisterRequest
        {
            FullName = "Welcome Me",
            Email = "welcome@test.com",
            Password = "Passw0rd!"
        });

        Assert.Contains(notifications.Sent, s => s == $"account-created:{user.Id}");
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        var db = TestDb.Create();
        var hasher = new Pbkdf2PasswordHasher();
        var service = new AuthService(db, hasher, new JwtTokenService(Config()), new FakeNotificationService());

        await service.RegisterAsync(new RegisterRequest { FullName = "A", Email = "a@test.com", Password = "Correct@1" });

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ValidateCredentialsAsync("a@test.com", "Wrong@1"));
    }

    [Fact]
    public async Task Forgot_password_issues_a_token_for_a_known_email_and_lets_it_reset_the_password()
    {
        var db = TestDb.Create();
        var hasher = new Pbkdf2PasswordHasher();
        var notifications = new FakeNotificationService();
        var service = new AuthService(db, hasher, new JwtTokenService(Config()), notifications);

        var (user, token) = await service.GeneratePasswordResetTokenAsync("admin@test.com");
        Assert.NotNull(user);
        Assert.False(string.IsNullOrWhiteSpace(token));

        await service.ResetPasswordAsync("admin@test.com", token!, "NewPass1!");

        // Old password no longer works, new one does.
        await Assert.ThrowsAsync<DomainException>(() => service.ValidateCredentialsAsync("admin@test.com", "x"));
        var loggedIn = await service.ValidateCredentialsAsync("admin@test.com", "NewPass1!");
        Assert.Equal(user!.Id, loggedIn.Id);

        Assert.Contains(notifications.Sent, s => s.StartsWith("password-reset-requested:"));
        Assert.Contains(notifications.Sent, s => s == $"password-changed:{user.Id}");
    }

    [Fact]
    public async Task Forgot_password_returns_no_token_for_an_unknown_email()
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        var (user, token) = await service.GeneratePasswordResetTokenAsync("nobody@test.com");

        Assert.Null(user);
        Assert.Null(token);
    }

    [Fact]
    public async Task An_incorrect_reset_token_is_rejected()
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        await service.GeneratePasswordResetTokenAsync("admin@test.com");

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ResetPasswordAsync("admin@test.com", "totally-wrong-token", "NewPass1!"));
    }

    [Fact]
    public async Task A_reset_token_cannot_be_reused_after_a_successful_reset()
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        var (_, token) = await service.GeneratePasswordResetTokenAsync("admin@test.com");
        await service.ResetPasswordAsync("admin@test.com", token!, "NewPass1!");

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ResetPasswordAsync("admin@test.com", token!, "AnotherPass1!"));
    }

    [Fact]
    public async Task A_weak_new_password_is_rejected_during_reset()
    {
        var db = TestDb.Create();
        var service = new AuthService(db, new Pbkdf2PasswordHasher(), new JwtTokenService(Config()), new FakeNotificationService());

        var (_, token) = await service.GeneratePasswordResetTokenAsync("admin@test.com");

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ResetPasswordAsync("admin@test.com", token!, "weak"));
    }
}

public class TeamServiceTests
{
    [Fact]
    public async Task Only_an_admin_can_create_a_team()
    {
        var service = new TeamService(TestDb.Create());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateAsync(new CreateTeamRequest { Name = "Beta" }, UserRole.Manager));
    }

    [Fact]
    public async Task Team_names_have_to_be_unique()
    {
        var service = new TeamService(TestDb.Create());

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(new CreateTeamRequest { Name = "Alpha" }, UserRole.Admin));
    }

    [Fact]
    public async Task A_plain_user_cannot_be_made_team_lead()
    {
        var service = new TeamService(TestDb.Create());

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(new CreateTeamRequest { Name = "Gamma", ManagerId = 3 }, UserRole.Admin));
    }

    [Fact]
    public async Task A_manager_only_sees_the_teams_they_are_part_of()
    {
        var db = TestDb.Create();
        var service = new TeamService(db);
        await service.CreateAsync(new CreateTeamRequest { Name = "Delta" }, UserRole.Admin);

        var managerView = await service.GetTeamsAsync(2, UserRole.Manager);
        var adminView = await service.GetTeamsAsync(1, UserRole.Admin);

        Assert.Single(managerView);
        Assert.Equal("Alpha", managerView[0].Name);
        Assert.Equal(2, adminView.Count);
    }
}

public class UserServiceTests
{
    [Fact]
    public async Task The_last_admin_cannot_be_demoted()
    {
        var db = TestDb.Create();
        var service = new UserService(db, new FakeNotificationService());

        await Assert.ThrowsAsync<DomainException>(() => service.ChangeRoleAsync(1, UserRole.User));
    }

    [Fact]
    public async Task A_manager_cannot_move_people_into_a_team_they_do_not_own()
    {
        var db = TestDb.Create();
        db.Teams.Add(new Team { Id = 2, Name = "Beta", ManagerId = 1 });
        await db.SaveChangesAsync();

        var service = new UserService(db, new FakeNotificationService());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AssignTeamAsync(4, 2, actorId: 2, actorRole: UserRole.Manager));
    }

    [Fact]
    public async Task An_admin_can_move_anyone_and_the_person_is_notified()
    {
        var db = TestDb.Create();
        var notes = new FakeNotificationService();
        var service = new UserService(db, notes);

        await service.AssignTeamAsync(4, 1, actorId: 1, actorRole: UserRole.Admin);

        var moved = await service.GetAsync(4);
        Assert.Equal(1, moved!.TeamId);
        Assert.Contains(notes.Sent, s => s.StartsWith("team:4"));
    }
}
