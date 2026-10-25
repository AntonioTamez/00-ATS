using System.Net;
using System.Net.Http.Json;
using Hotel.Application;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hotel.Tests;

public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<RoomDto> NewRoom(HttpClient client, decimal rate = 100m) =>
        (await (await client.PostAsJsonAsync("/api/rooms", new { number = "101", nightlyRate = rate })).Content.ReadFromJsonAsync<RoomDto>())!;

    private static Task<HttpResponseMessage> Book(HttpClient client, Guid roomId, int fromDays, int toDays, string email = "ana@mail.com") =>
        client.PostAsJsonAsync("/api/reservations", new
        {
            roomId,
            guestName = "Ana",
            guestEmail = email,
            checkIn = Today.AddDays(fromDays),
            checkOut = Today.AddDays(toDays)
        });

    [Fact]
    public async Task FullStay_Book_CheckIn_Charge_CheckOut()
    {
        var client = factory.CreateClient();
        var room = await NewRoom(client);

        var booked = await Book(client, room.Id, 0, 2);
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var reservation = (await booked.Content.ReadFromJsonAsync<ReservationDto>())!;
        Assert.Equal(200m, reservation.Total);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/reservations/{reservation.Id}/check-in", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/reservations/{reservation.Id}/charges", new { description = "Minibar", amount = 15m })).StatusCode);
        var checkedOut = await client.PostAsync($"/api/reservations/{reservation.Id}/check-out", null);

        var done = (await checkedOut.Content.ReadFromJsonAsync<ReservationDto>())!;
        Assert.Equal("CheckedOut", done.Status);
        Assert.Equal(215m, done.Total);
        Assert.Single(done.Charges);
    }

    [Fact]
    public async Task Overlapping_Booking_Returns422()
    {
        var client = factory.CreateClient();
        var room = await NewRoom(client);
        await Book(client, room.Id, 10, 13);

        var clash = await Book(client, room.Id, 12, 15);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, clash.StatusCode);
    }

    [Fact]
    public async Task Cancel_FreesTheDates()
    {
        var client = factory.CreateClient();
        var room = await NewRoom(client);
        var reservation = (await (await Book(client, room.Id, 20, 22)).Content.ReadFromJsonAsync<ReservationDto>())!;

        var cancel = await client.PostAsync($"/api/reservations/{reservation.Id}/cancel", null);
        var again = await Book(client, room.Id, 20, 22);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task InvalidEmail_Returns422() =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Book(factory.CreateClient(), (await NewRoom(factory.CreateClient())).Id, 30, 32, "nope")).StatusCode);

    [Fact]
    public async Task UnknownReservation_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/reservations/{Guid.NewGuid()}")).StatusCode);

    [Fact]
    public async Task UnknownRoom_Returns404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Book(factory.CreateClient(), Guid.NewGuid(), 5, 7)).StatusCode);
}
