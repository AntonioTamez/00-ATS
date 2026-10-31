using Helpdesk.Api.Common;
using Open = Helpdesk.Api.Features.OpenTicket;
using Assign = Helpdesk.Api.Features.AssignTicket;
using Close = Helpdesk.Api.Features.CloseTicket;
using Get = Helpdesk.Api.Features.GetTicket;
using List = Helpdesk.Api.Features.ListTickets;

namespace Helpdesk.Tests;

/// <summary>
/// Each slice is tested through its own handler against a real (in-memory) DbContext.
/// No mocks of repositories: there are none.
/// </summary>
public class SliceTests
{
    private readonly AppDbContext _db = TestDb.New();
    private readonly FixedTimeProvider _clock = new(TestDb.T0);

    private async Task<Guid> OpenOne(string title = "Printer broken")
    {
        var created = await new Open.Handler(_db, _clock).Handle(new Open.Request(title, "It jams"), default);
        return created.Id;
    }

    // --- OpenTicket

    [Fact]
    public async Task OpenTicket_PersistsAnOpenTicket()
    {
        var response = await new Open.Handler(_db, _clock).Handle(new Open.Request("  VPN down ", null), default);

        Assert.Equal("VPN down", response.Title);
        Assert.Equal("Open", response.Status);
        Assert.Single(_db.Tickets);
    }

    [Fact]
    public async Task OpenTicket_EmptyTitle_Throws_AndPersistsNothing()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            new Open.Handler(_db, _clock).Handle(new Open.Request("", null), default));

        Assert.Empty(_db.Tickets);
    }

    // --- AssignTicket

    [Fact]
    public async Task AssignTicket_SetsAssignee()
    {
        var id = await OpenOne();

        var response = await new Assign.Handler(_db).Handle(id, new Assign.Request("Ana"), default);

        Assert.Equal("Assigned", response.Status);
        Assert.Equal("Ana", response.Assignee);
    }

    [Fact]
    public async Task AssignTicket_Unknown_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new Assign.Handler(_db).Handle(Guid.NewGuid(), new Assign.Request("Ana"), default));

    // --- CloseTicket

    [Fact]
    public async Task CloseTicket_SetsClosedAtFromClock()
    {
        var id = await OpenOne();
        _clock.Now = TestDb.T0.AddHours(3);

        var response = await new Close.Handler(_db, _clock).Handle(id, new Close.Request("Replaced toner"), default);

        Assert.Equal("Closed", response.Status);
        Assert.Equal(TestDb.T0.AddHours(3), response.ClosedAt);
    }

    [Fact]
    public async Task CloseTicket_WithoutResolution_Throws()
    {
        var id = await OpenOne();

        await Assert.ThrowsAsync<DomainException>(() =>
            new Close.Handler(_db, _clock).Handle(id, new Close.Request(" "), default));
    }

    // --- GetTicket (query: projection, no entity)

    [Fact]
    public async Task GetTicket_ReturnsProjection()
    {
        var id = await OpenOne("VPN down");
        await new Assign.Handler(_db).Handle(id, new Assign.Request("Ana"), default);

        var response = await new Get.Handler(_db).Handle(id, default);

        Assert.Equal("VPN down", response.Title);
        Assert.Equal("Assigned", response.Status);
        Assert.Equal("Ana", response.Assignee);
    }

    [Fact]
    public async Task GetTicket_Unknown_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new Get.Handler(_db).Handle(Guid.NewGuid(), default));

    // --- ListTickets (query: paging + filter + validation)

    [Fact]
    public async Task ListTickets_PagesOldestFirst()
    {
        for (var i = 1; i <= 5; i++)
        {
            _clock.Now = TestDb.T0.AddMinutes(i);
            await OpenOne($"T{i}");
        }

        var page2 = await new List.Handler(_db).Handle(new List.Query(null, Page: 2, PageSize: 2), default);

        Assert.Equal(5, page2.Total);
        Assert.Equal(["T3", "T4"], page2.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task ListTickets_FiltersByStatus_CaseInsensitive()
    {
        var first = await OpenOne("A");
        await OpenOne("B");
        await new Assign.Handler(_db).Handle(first, new Assign.Request("Ana"), default);

        var assigned = await new List.Handler(_db).Handle(new List.Query("assigned"), default);

        Assert.Equal(["A"], assigned.Items.Select(i => i.Title));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, List.Handler.MaxPageSize + 1)]
    public async Task ListTickets_InvalidPaging_Throws(int page, int pageSize) =>
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            new List.Handler(_db).Handle(new List.Query(null, page, pageSize), default));

    [Fact]
    public async Task ListTickets_UnknownStatus_Throws() =>
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            new List.Handler(_db).Handle(new List.Query("nonsense"), default));
}
