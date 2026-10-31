using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shop.Tests;

/// <summary>MVC through a real HTTP pipeline: routing, anti-forgery, Razor views rendered to HTML.</summary>
public partial class MvcHttpTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="([^"]+)""")]
    private static partial Regex TokenPattern();

    private HttpClient NewBrowser() => factory.WithWebHostBuilder(_ => { })
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    // Razor HTML-encodes non-ASCII characters (the euro sign becomes &#x20AC;). Browsers decode it; so do we.
    private static async Task<string> GetHtmlAsync(HttpClient browser, string url) =>
        WebUtility.HtmlDecode(await browser.GetStringAsync(url));

    private static async Task<string> TokenAsync(HttpClient browser)
    {
        var html = await browser.GetStringAsync("/");
        return TokenPattern().Match(html).Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient browser, string url, string token, params (string Key, string Value)[] fields)
    {
        var form = fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)).ToList();
        if (token.Length > 0) form.Add(new("__RequestVerificationToken", token));
        return await browser.PostAsync(url, new FormUrlEncodedContent(form));
    }

    [Fact]
    public async Task TheHomePage_RendersEveryProduct_AsHtml()
    {
        var response = await NewBrowser().GetAsync("/");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Clean Architecture (book)", html);
        Assert.Contains("35.00 €", html);
        Assert.Contains("<strong id=\"cart-count\">0</strong>", html);
    }

    [Fact]
    public async Task EveryFormOnThePage_CarriesAnAntiForgeryToken()
    {
        var html = await NewBrowser().GetStringAsync("/");

        Assert.Equal(Regex.Matches(html, "<form ").Count, Regex.Matches(html, "__RequestVerificationToken").Count);
    }

    [Fact]
    public async Task AddingAProduct_RedirectsToTheCart_WhichShowsIt()
    {
        var browser = NewBrowser();
        var token = await TokenAsync(browser);

        var add = await PostFormAsync(browser, "/cart/add", token, ("sku", "MUG-1"), ("quantity", "2"));
        var cart = await GetHtmlAsync(browser, "/cart");

        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        Assert.Equal("/cart", add.Headers.Location!.OriginalString);
        Assert.Contains("Architecture mug", cart);
        Assert.Contains("<span id=\"total\">19.00 €</span>", cart);
    }

    [Fact]
    public async Task TheHomePage_ShowsTheCartCount_AfterAdding()
    {
        var browser = NewBrowser();
        var token = await TokenAsync(browser);
        await PostFormAsync(browser, "/cart/add", token, ("sku", "PEN-1"), ("quantity", "3"));

        var home = await GetHtmlAsync(browser, "/");

        Assert.Contains("<strong id=\"cart-count\">3</strong>", home);
    }

    [Fact]
    public async Task TheDiscount_AppearsInTheRenderedCart()
    {
        var browser = NewBrowser();
        var token = await TokenAsync(browser);
        await PostFormAsync(browser, "/cart/add", token, ("sku", "BOOK-1"), ("quantity", "3"));

        var cart = await GetHtmlAsync(browser, "/cart");

        Assert.Contains("<span id=\"discount\">10.50 €</span>", cart);
        Assert.Contains("<span id=\"total\">94.50 €</span>", cart);
    }

    [Fact]
    public async Task ARefusedRule_ReturnsTheCartPage_With422_AndTheMessage()
    {
        var browser = NewBrowser();
        var token = await TokenAsync(browser);

        var response = await PostFormAsync(browser, "/cart/add", token, ("sku", "PEN-1"), ("quantity", "11"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("at most 10 units of Pen", html);
    }

    [Fact]
    public async Task APost_WithoutTheAntiForgeryToken_IsRejected()
    {
        var browser = NewBrowser();
        await browser.GetStringAsync("/");

        var response = await PostFormAsync(browser, "/cart/add", token: "", ("sku", "PEN-1"), ("quantity", "1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownProduct_Returns404()
    {
        var browser = NewBrowser();
        var token = await TokenAsync(browser);

        Assert.Equal(HttpStatusCode.NotFound,
            (await PostFormAsync(browser, "/cart/add", token, ("sku", "NOPE"), ("quantity", "1"))).StatusCode);
    }

    [Fact]
    public async Task RemoveAndClear_UpdateTheRenderedCart()
    {
        var browser = NewBrowser();
        var token = await TokenAsync(browser);
        await PostFormAsync(browser, "/cart/add", token, ("sku", "PEN-1"), ("quantity", "1"));
        await PostFormAsync(browser, "/cart/add", token, ("sku", "MUG-1"), ("quantity", "1"));

        await PostFormAsync(browser, "/cart/remove", token, ("sku", "PEN-1"));
        var afterRemove = await GetHtmlAsync(browser, "/cart");
        Assert.DoesNotContain("<td>Pen</td>", afterRemove);
        Assert.Contains("Architecture mug", afterRemove);

        await PostFormAsync(browser, "/cart/clear", token);
        Assert.Contains("id=\"empty\"", await GetHtmlAsync(browser, "/cart"));
    }

    [Fact]
    public async Task TwoBrowsers_HaveSeparateCarts()
    {
        var ana = NewBrowser();
        var bob = NewBrowser();
        var token = await TokenAsync(ana);
        await PostFormAsync(ana, "/cart/add", token, ("sku", "PEN-1"), ("quantity", "1"));

        Assert.Contains("id=\"empty\"", await GetHtmlAsync(bob, "/cart"));
        Assert.DoesNotContain("id=\"empty\"", await GetHtmlAsync(ana, "/cart"));
    }
}
