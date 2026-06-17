using Microsoft.Extensions.Configuration;
using Pattern.Core.Model;
using PatternPro.Business.Services;
using PatternPro.Core.Persistence.Repositories;

namespace PatternPro.Tests;

public class UserServiceTests
{
    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<AppUser> _users = [];
        private int _nextId = 1;

        public IReadOnlyList<AppUser> GetAll() => _users;
        public AppUser? GetById(int id) => _users.FirstOrDefault(u => u.Id == id);
        public AppUser? GetByUserName(string userName) =>
            _users.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));
        public AppUser? GetByEmployeeId(string employeeId) =>
            _users.FirstOrDefault(u => string.Equals(u.EmployeeId, employeeId, StringComparison.OrdinalIgnoreCase));
        public bool AnyUsers() => _users.Count > 0;

        public AppUser Save(AppUser user)
        {
            if (user.Id <= 0)
            {
                user.Id = _nextId++;
                _users.Add(user);
            }
            else
            {
                var idx = _users.FindIndex(u => u.Id == user.Id);
                _users[idx] = user;
            }
            return user;
        }

        public void Delete(int id) => _users.RemoveAll(u => u.Id == id);
    }

    private static UserService CreateSut(InMemoryUserRepository repo, bool requireApproval = false) =>
        new(repo, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RegistrationEnabled"] = "true",
                ["Auth:RequireAdminApproval"] = requireApproval ? "true" : "false",
            })
            .Build());

    [Fact]
    public void ValidateLogin_rejects_disabled_user()
    {
        var repo = new InMemoryUserRepository();
        var sut = CreateSut(repo);
        sut.EnsureSeedAdmin("admin", "secret12");
        var user = repo.GetByUserName("admin")!;
        user.IsActive = false;
        repo.Save(user);

        Assert.Null(sut.ValidateLogin("admin", "secret12"));
    }

    [Fact]
    public void ValidateLogin_accepts_active_user()
    {
        var repo = new InMemoryUserRepository();
        var sut = CreateSut(repo);
        sut.EnsureSeedAdmin("admin", "secret12");

        Assert.NotNull(sut.ValidateLogin("admin", "secret12"));
    }

    [Fact]
    public void ValidateLogin_accepts_employee_id()
    {
        var repo = new InMemoryUserRepository();
        var sut = CreateSut(repo);
        sut.EnsureSeedAdmin("admin", "secret12");

        Assert.NotNull(sut.ValidateLogin("ADMIN", "secret12"));
    }

    [Fact]
    public void RegisterUser_creates_pending_user_when_approval_required()
    {
        var repo = new InMemoryUserRepository();
        var sut = CreateSut(repo, requireApproval: true);

        var user = sut.RegisterUser("EMP-100", "QA Tester", "qa100", "secret12");

        Assert.Equal("EMP-100", user.EmployeeId);
        Assert.False(user.IsActive);
        Assert.Equal(AppRoles.Viewer, user.Role);
    }

    [Fact]
    public void RegisterUser_throws_when_registration_disabled()
    {
        var repo = new InMemoryUserRepository();
        var sut = new UserService(repo, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RegistrationEnabled"] = "false",
            })
            .Build());

        Assert.Throws<InvalidOperationException>(() =>
            sut.RegisterUser("EMP-200", "Test", "test", "secret12"));
    }

    [Fact]
    public void UpdateUser_cannot_disable_last_active_admin()
    {
        var repo = new InMemoryUserRepository();
        var sut = CreateSut(repo);
        sut.EnsureSeedAdmin("admin", "secret12");

        var admin = repo.GetByUserName("admin")!;
        Assert.Throws<InvalidOperationException>(() =>
            sut.UpdateUser(admin.Id, admin.EmployeeId, admin.DisplayName, AppRoles.Admin, isActive: false));
    }
}
