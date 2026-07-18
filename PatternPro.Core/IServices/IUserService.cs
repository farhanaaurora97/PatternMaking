using Pattern.Core.Model;

namespace PatternPro.Core.IServices;

public interface IUserService
{
    IReadOnlyList<AppUser> GetAll();
    AppUser? GetById(int id);
    AppUser? ValidateLogin(string userName, string password);
    AppUser CreateUser(string employeeId, string userName, string displayName, string role, string password);
    AppUser RegisterUser(string employeeId, string displayName, string userName, string password);
    AppUser? UpdateUser(int id, string employeeId, string displayName, string role, bool isActive);
    bool ChangePassword(int userId, string currentPassword, string newPassword);
    bool ResetPassword(int id, string newPassword);
    bool DeleteUser(int id);
    void RecordLogin(int id);
    void EnsureSeedAdmin(string userName, string password);
    bool IsRegistrationEnabled();
    bool RequiresAdminApproval();
}
