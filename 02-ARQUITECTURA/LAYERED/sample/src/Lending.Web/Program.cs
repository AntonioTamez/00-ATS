using Lending.Business;
using Lending.Web;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLendingBusiness();
builder.Services.AddExceptionHandler<LendingExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

app.MapGet("/books", (CatalogService catalog) => Results.Ok(catalog.ListBooks()));
app.MapGet("/books/{id:int}", (int id, CatalogService catalog) => Results.Ok(catalog.GetBook(id)));
app.MapPost("/books", (NewBookRequest request, CatalogService catalog) =>
{
    var book = catalog.AddBook(request.Title ?? "", request.Author ?? "");
    return Results.Created($"/books/{book.Id}", book);
});

app.MapPost("/members", (NewMemberRequest request, MembershipService membership) =>
{
    var member = membership.Register(request.Name ?? "", request.Email ?? "");
    return Results.Created($"/members/{member.Id}", member);
});
app.MapGet("/members/{id:int}", (int id, MembershipService membership) => Results.Ok(membership.Get(id)));
app.MapGet("/members/{id:int}/loans", (int id, LoanService loans) => Results.Ok(loans.ListForMember(id)));

app.MapPost("/loans", (NewLoanRequest request, LoanService loans) =>
{
    var loan = loans.Borrow(request.MemberId, request.BookId);
    return Results.Created($"/loans/{loan.Id}", loan);
});
app.MapPost("/loans/{id:int}/return", (int id, LoanService loans) => Results.Ok(loans.Return(id)));

app.Run();

namespace Lending.Web
{
    public sealed record NewBookRequest(string? Title, string? Author);
    public sealed record NewMemberRequest(string? Name, string? Email);
    public sealed record NewLoanRequest(int MemberId, int BookId);

    public sealed class LendingExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title) = exception switch
            {
                BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "A business rule was violated"),
                NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                _ => (0, ""),
            };
            if (status == 0) return false;

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { title, detail = exception.Message, status }, cancellationToken);
            return true;
        }
    }
}

public partial class Program;
