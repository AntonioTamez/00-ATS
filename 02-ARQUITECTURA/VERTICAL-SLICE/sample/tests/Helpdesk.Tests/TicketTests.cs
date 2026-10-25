using Helpdesk.Api.Common;

namespace Helpdesk.Tests;

/// <summary>The shared model keeps the business rules; each slice relies on them.</summary>
public class TicketTests
{
    private static readonly DateTimeOffset Now = TestDb.T0;

    [Fact]
    public void Open_TrimsTitle_AndStartsOpen()
    {
        var ticket = Ticket.Open("  Printer broken ", null, Now);

        Assert.Equal("Printer broken", ticket.Title);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(string.Empty, ticket.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_RejectsEmptyTitle(string title) =>
        Assert.Throws<DomainException>(() => Ticket.Open(title, null, Now));

    [Fact]
    public void Open_RejectsTitleOverMaxLength() =>
        Assert.Throws<DomainException>(() => Ticket.Open(new string('a', Ticket.MaxTitleLength + 1), null, Now));

    [Fact]
    public void Assign_SetsAssigneeAndStatus()
    {
        var ticket = Ticket.Open("T", null, Now);

        ticket.Assign("Ana");

        Assert.Equal("Ana", ticket.Assignee);
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
    }

    [Fact]
    public void Assign_RequiresAssignee() =>
        Assert.Throws<DomainException>(() => Ticket.Open("T", null, Now).Assign(" "));

    [Fact]
    public void Close_RequiresResolution() =>
        Assert.Throws<DomainException>(() => Ticket.Open("T", null, Now).Close("", Now));

    [Fact]
    public void Close_Twice_Throws()
    {
        var ticket = Ticket.Open("T", null, Now);
        ticket.Close("Fixed", Now);

        Assert.Throws<DomainException>(() => ticket.Close("Again", Now));
    }

    [Fact]
    public void Assign_ClosedTicket_Throws()
    {
        var ticket = Ticket.Open("T", null, Now);
        ticket.Close("Fixed", Now);

        Assert.Throws<DomainException>(() => ticket.Assign("Ana"));
    }
}
