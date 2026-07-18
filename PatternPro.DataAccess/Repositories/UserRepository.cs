using System.Text.Json;
using Pattern.Core.Model;
using PatternPro.Core.Persistence.Repositories;
using PatternPro.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PatternPro.DataAccess.Repositories;

internal sealed class JsonUserRepository : IUserRepository
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonUserRepository(string appDataDirectory)
    {
        Directory.CreateDirectory(appDataDirectory);
        _path = Path.Combine(appDataDirectory, "users.json");
    }

    public IReadOnlyList<AppUser> GetAll() =>
        Load().Users.OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase).ToList();

    public AppUser? GetById(int id) =>
        Load().Users.FirstOrDefault(u => u.Id == id);

    public AppUser? GetByUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return null;
        var key = userName.Trim();
        return Load().Users.FirstOrDefault(u =>
            string.Equals(u.UserName, key, StringComparison.OrdinalIgnoreCase));
    }

    public AppUser? GetByEmployeeId(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId)) return null;
        var key = employeeId.Trim().ToUpperInvariant();
        return Load().Users.FirstOrDefault(u =>
            string.Equals(u.EmployeeId, key, StringComparison.OrdinalIgnoreCase));
    }

    public AppUser Save(AppUser user)
    {
        lock (_lock)
        {
            var store = Load();
            if (user.Id <= 0)
            {
                user.Id = store.NextId++;
                user.CreatedAt = DateTime.UtcNow;
                store.Users.Add(user);
            }
            else
            {
                var idx = store.Users.FindIndex(u => u.Id == user.Id);
                if (idx < 0)
                    throw new InvalidOperationException($"User {user.Id} not found.");
                store.Users[idx] = user;
            }

            Write(store);
            return user;
        }
    }

    public void Delete(int id)
    {
        lock (_lock)
        {
            var store = Load();
            store.Users.RemoveAll(u => u.Id == id);
            Write(store);
        }
    }

    public bool AnyUsers() => Load().Users.Count > 0;

    private UsersStore Load()
    {
        if (!File.Exists(_path))
            return new UsersStore();

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<UsersStore>(json, PersistenceJson.Options) ?? new UsersStore();
        }
        catch
        {
            return new UsersStore();
        }
    }

    private void Write(UsersStore store)
    {
        var json = JsonSerializer.Serialize(store, PersistenceJson.Options);
        File.WriteAllText(_path, json);
    }
}

internal sealed class PostgresUserRepository : IUserRepository
{
    private readonly IDbContextFactory<PatternProDbContext> _factory;

    public PostgresUserRepository(IDbContextFactory<PatternProDbContext> factory) =>
        _factory = factory;

    public IReadOnlyList<AppUser> GetAll()
    {
        using var db = _factory.CreateDbContext();
        return db.AppUsers.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(ToModel)
            .ToList();
    }

    public AppUser? GetById(int id)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.AppUsers.AsNoTracking().FirstOrDefault(u => u.Id == id);
        return entity is null ? null : ToModel(entity);
    }

    public AppUser? GetByUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return null;
        var key = userName.Trim().ToLowerInvariant();
        using var db = _factory.CreateDbContext();
        var entity = db.AppUsers.AsNoTracking()
            .FirstOrDefault(u => u.UserName.ToLower() == key);
        return entity is null ? null : ToModel(entity);
    }

    public AppUser? GetByEmployeeId(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId)) return null;
        var key = employeeId.Trim().ToUpperInvariant();
        using var db = _factory.CreateDbContext();
        var entity = db.AppUsers.AsNoTracking()
            .FirstOrDefault(u => u.EmployeeId.ToUpper() == key);
        return entity is null ? null : ToModel(entity);
    }

    public AppUser Save(AppUser user)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.AppUsers.FirstOrDefault(u => u.Id == user.Id);
        if (entity is null)
        {
            if (user.Id <= 0)
            {
                var maxId = db.AppUsers.Select(u => (int?)u.Id).Max() ?? 0;
                user.Id = maxId + 1;
            }

            user.CreatedAt = DateTime.UtcNow;
            entity = ToEntity(user);
            db.AppUsers.Add(entity);
        }
        else
        {
            entity.EmployeeId = user.EmployeeId;
            entity.DisplayName = user.DisplayName;
            entity.Role = user.Role;
            entity.IsActive = user.IsActive;
            entity.PasswordHash = user.PasswordHash;
            entity.LastLoginAt = user.LastLoginAt;
        }

        db.SaveChanges();
        return ToModel(entity);
    }

    public void Delete(int id)
    {
        using var db = _factory.CreateDbContext();
        var entity = db.AppUsers.FirstOrDefault(u => u.Id == id);
        if (entity is null) return;
        db.AppUsers.Remove(entity);
        db.SaveChanges();
    }

    public bool AnyUsers()
    {
        using var db = _factory.CreateDbContext();
        return db.AppUsers.Any();
    }

    private static AppUser ToModel(AppUserEntity e) => new()
    {
        Id = e.Id,
        EmployeeId = e.EmployeeId,
        UserName = e.UserName,
        DisplayName = e.DisplayName,
        Role = e.Role,
        IsActive = e.IsActive,
        PasswordHash = e.PasswordHash,
        CreatedAt = e.CreatedAt,
        LastLoginAt = e.LastLoginAt,
    };

    private static AppUserEntity ToEntity(AppUser u) => new()
    {
        Id = u.Id,
        EmployeeId = u.EmployeeId,
        UserName = u.UserName,
        DisplayName = u.DisplayName,
        Role = u.Role,
        IsActive = u.IsActive,
        PasswordHash = u.PasswordHash,
        CreatedAt = u.CreatedAt,
        LastLoginAt = u.LastLoginAt,
    };
}
