using Pattern.Core.Model;

namespace PatternPro.Core.Persistence.Repositories;

public interface IUserRepository
{
    IReadOnlyList<AppUser> GetAll();
    AppUser? GetById(int id);
    AppUser? GetByUserName(string userName);
    AppUser? GetByEmployeeId(string employeeId);
    AppUser Save(AppUser user);
    void Delete(int id);
    bool AnyUsers();
}
