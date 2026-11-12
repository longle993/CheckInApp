using System.Text.Json;
using CheckInApp.Application;
using CheckInApp.Domain;
using CheckInApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CheckInApp.Tests;

public class SearchTests
{
    [Theory]
    [InlineData(" nguyen   bao ", 2)]
    [InlineData("bao BAO", 1)]
    [InlineData("  ", 0)]
    public void TermsTrimSplitAndDeduplicate(string text, int count) => Assert.Equal(count, ReadService.SearchTerms(text).Length);
    [Fact] public void TermsBoundWork() => Assert.Throws<AppException>(() => ReadService.SearchTerms(new string('a', 121)));
    [Fact]
    public async Task StudentSearchMatchesUnaccentedWordsInAnyOrderWithinScope()
    {
        await using var f = new Fixture();
        await f.Init();
        var read = new ReadService(f.Db, f.Access, f.Clock);
        var result = JsonSerializer.SerializeToElement(await read.List(f.User, "students", "bao nguyen", null, null, 1, 25, default));
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.False(result.GetProperty("items")[0].GetProperty("hasAvatar").GetBoolean());
        var empty = JsonSerializer.SerializeToElement(await read.List(f.User, "students", "bao khongtontai", null, null, 1, 25, default));
        Assert.Equal(0, empty.GetProperty("total").GetInt32());
    }
    [Fact]
    public async Task TeacherAssignmentsAreLimitedToPageAndAvatarRemovalIsVisible()
    {
        await using var f = new Fixture();
        await f.Init();
        var teacher = await f.Db.Catechists.SingleAsync();
        f.Db.Audit.Add(new()
        {
            EntityId = teacher.Id,
            EntityType = "catechists",
            Action = "Avatar",
            Detail = "test.png",
            CreatedAtUtc = new(2026, 1, 1)
        });
        f.Db.Audit.Add(new()
        {
            EntityId = teacher.Id,
            EntityType = "catechists",
            Action = "Avatar",
            Detail = "",
            CreatedAtUtc = new(2026, 2, 1)
        });
        await f.Db.SaveChangesAsync();
        var read = new ReadService(f.Db, f.Access, f.Clock);
        var result = JsonSerializer.SerializeToElement(await read.List(f.User, "catechists", "absent", null, null, 1, 25, default));
        Assert.Empty(result.GetProperty("assignments").EnumerateArray());
        result = JsonSerializer.SerializeToElement(await read.List(f.User, "catechists", null, null, null, 1, 25, default));
        Assert.False(result.GetProperty("items")[0].GetProperty("hasAvatar").GetBoolean());
    }
}
