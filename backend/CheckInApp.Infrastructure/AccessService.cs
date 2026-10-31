using CheckInApp.Application;
using CheckInApp.Domain;
using Microsoft.EntityFrameworkCore;
namespace CheckInApp.Infrastructure;

public class AccessService(AppDbContext db, TimeProvider clock)
{
    public async Task<(long Mask, bool Global)> Rights(Guid user, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(x => x.Id == user && x.Status == AccountStatus.Active, ct))
            throw new AppException(403, "ACCOUNT_INACTIVE", "Tài khoản chưa hoạt động.");
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == user select r).ToListAsync(ct);
        return (roles.Aggregate(0L, (v, r) => v | r.Permissions), roles.Any(x => x.GlobalScope));
    }
    public async Task Require(Guid user, Permission p, CancellationToken ct = default)
    {
        var r = await Rights(user, ct);
        if (p == Permission.None || (r.Mask & (long)p) != (long)p)
            throw new AppException(403, "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.");
    }
    public async Task<List<Guid>> ClassIds(Guid user, CancellationToken ct = default)
    {
        var rights = await Rights(user, ct);
        if (rights.Global)
            return await db.Classes.Select(x => x.Id).ToListAsync(ct);
        var today = AttendanceRules.Today(clock);
        return await (from a in db.Assignments join c in db.Catechists on a.CatechistId equals c.Id where c.UserId == user && a.From <= today && (a.Until == null || today < a.Until) select a.ClassRoomId).Distinct().ToListAsync(ct);
    }
    public async Task Class(Guid user, Guid id, CancellationToken ct = default)
    {
        if (!(await ClassIds(user, ct)).Contains(id))
            throw new AppException(403, "CLASS_SCOPE", "Bạn không phụ trách lớp này.");
    }
}
