using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Clinic.Tests;

/// <summary>Everything wired by the Host: the adapters between capabilities do their job end to end.</summary>
public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private HttpClient Fresh() => factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
    {
        s.RemoveAll<TimeProvider>();
        s.AddSingleton<TimeProvider>(new FakeTime(Now));
    })).CreateClient();

    private static async Task<Guid> RegisterAsync(HttpClient client, string email = "ana@x.com")
    {
        var response = await client.PostAsJsonAsync("/patients", new { name = "Ana", email });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> BookAsync(HttpClient client, Guid patient, DateTimeOffset start) =>
        client.PostAsJsonAsync("/appointments", new { patientId = patient, doctor = "Dr. Who", start });

    [Fact]
    public async Task ARegisteredPatient_CanBeFetched_AndAnUnknownOneIs404()
    {
        var client = Fresh();
        var id = await RegisterAsync(client);

        Assert.Equal("Ana", (await client.GetFromJsonAsync<JsonElement>($"/patients/{id}")).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/patients/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task ABrokenEntityRule_Returns422() =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Fresh().PostAsJsonAsync("/patients", new { name = "", email = "x" })).StatusCode);

    [Fact]
    public async Task Booking_WorksAcrossCapabilities_PatientsAnswersTheQuestionAppointmentsAsks()
    {
        var client = Fresh();
        var patient = await RegisterAsync(client);

        var response = await BookAsync(client, patient, Now.AddDays(1).AddHours(2));         // Tuesday 10:00

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>($"/patients/{patient}/appointments")).GetArrayLength());
    }

    [Fact]
    public async Task BookingForAnUnknownPatient_Returns404_ThroughThePatientsAdapter() =>
        Assert.Equal(HttpStatusCode.NotFound, (await BookAsync(Fresh(), Guid.NewGuid(), Now.AddDays(1).AddHours(2))).StatusCode);

    [Fact]
    public async Task ADoubleBooking_Returns422()
    {
        var client = Fresh();
        var patient = await RegisterAsync(client);
        await BookAsync(client, patient, Now.AddDays(1).AddHours(2));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await BookAsync(client, patient, Now.AddDays(1).AddHours(2))).StatusCode);
    }

    [Fact]
    public async Task ALateCancellation_ReachesBilling_ThroughTheFeeChargerAdapter()
    {
        var client = Fresh();
        var patient = await RegisterAsync(client);
        var booked = await (await BookAsync(client, patient, Now.AddHours(7))).Content.ReadFromJsonAsync<JsonElement>();     // today 15:00

        var cancel = await client.PostAsync($"/appointments/{booked.GetProperty("id").GetGuid()}/cancel", null);
        var invoices = await client.GetFromJsonAsync<JsonElement>($"/patients/{patient}/invoices");

        Assert.True((await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("lateCancellation").GetBoolean());
        Assert.Equal(25m, invoices[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task AnEarlyCancellation_ProducesNoInvoice()
    {
        var client = Fresh();
        var patient = await RegisterAsync(client);
        var booked = await (await BookAsync(client, patient, Now.AddDays(2).AddHours(2))).Content.ReadFromJsonAsync<JsonElement>();

        await client.PostAsync($"/appointments/{booked.GetProperty("id").GetGuid()}/cancel", null);

        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"/patients/{patient}/invoices")).GetArrayLength());
    }

    [Fact]
    public async Task CancellingTwice_Returns422_AndAnUnknownAppointment404()
    {
        var client = Fresh();
        var patient = await RegisterAsync(client);
        var booked = await (await BookAsync(client, patient, Now.AddDays(2).AddHours(2))).Content.ReadFromJsonAsync<JsonElement>();
        var id = booked.GetProperty("id").GetGuid();
        await client.PostAsync($"/appointments/{id}/cancel", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/appointments/{id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/appointments/{Guid.NewGuid()}/cancel", null)).StatusCode);
    }
}
