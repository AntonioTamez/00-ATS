using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shop.Model;
using Shop.Mvc;
using Shop.Mvc.Controllers;
using Shop.Mvc.Models;

namespace Shop.Tests;

/// <summary>MVC controllers tested WITHOUT a web server: a controller is a plain class that returns a result.</summary>
public class MvcControllerTests
{
    /// <summary>One visitor: remembers the cookie the way a browser would, between controller calls.</summary>
    private sealed class Visitor
    {
        private readonly Catalog _catalog = new();
        private readonly CartStore _carts;
        private string? _cookie;

        public Visitor(CartStore? shared = null) => _carts = shared ?? new CartStore();

        public ProductsController Products() => With(new ProductsController(_catalog, _carts));

        public CartController Cart() => With(new CartController(_catalog, _carts));

        /// <summary>One complete request: a fresh controller, then the cookie it set is kept for the next request.</summary>
        public IActionResult Do(Func<CartController, IActionResult> action)
        {
            var controller = Cart();
            var result = action(controller);
            Remember(controller);
            return result;
        }

        public void Remember(ControllerBase controller)
        {
            var setCookie = controller.Response.Headers.SetCookie.ToString();
            if (setCookie.StartsWith($"{CartStore.CookieName}=")) _cookie = setCookie.Split(';')[0];
        }

        private T With<T>(T controller) where T : ControllerBase
        {
            var http = new DefaultHttpContext();
            if (_cookie is not null) http.Request.Headers.Cookie = _cookie;
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            return controller;
        }
    }

    [Fact]
    public void ProductsIndex_ShowsEveryProduct_WithFormattedPrices_AndAnEmptyCart()
    {
        var model = Assert.IsType<ProductsIndexViewModel>(Assert.IsType<ViewResult>(new Visitor().Products().Index()).Model);

        Assert.Equal(3, model.Products.Count);
        Assert.Contains(new ProductCard("BOOK-1", "Clean Architecture (book)", "35.00 €"), model.Products);
        Assert.Equal(0, model.CartItemCount);
    }

    [Fact]
    public void AddingAProduct_RedirectsToTheCart_Post_Redirect_Get()
    {
        var visitor = new Visitor();
        var cart = visitor.Cart();

        var result = cart.Add("PEN-1", 2);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public void TheCartPage_ShowsWhatWasAdded_AndTheTotals()
    {
        var visitor = new Visitor();
        var add = visitor.Cart();
        add.Add("BOOK-1", 3);
        visitor.Remember(add);

        var model = Assert.IsType<CartIndexViewModel>(Assert.IsType<ViewResult>(visitor.Cart().Index()).Model);

        var line = Assert.Single(model.Lines);
        Assert.Equal(("BOOK-1", 3, "105.00 €"), (line.Sku, line.Quantity, line.LineTotalText));
        Assert.Equal(("105.00 €", "10.50 €", "94.50 €", false), (model.SubtotalText, model.DiscountText, model.TotalText, model.IsEmpty));
    }

    [Fact]
    public void ARuleRefusedByTheModel_ReShowsTheCart_WithTheMessage_And422()
    {
        var visitor = new Visitor();
        var cart = visitor.Cart();

        var result = cart.Add("PEN-1", Cart.MaxPerLine + 1);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, cart.Response.StatusCode);
        Assert.Contains("at most", Assert.IsType<CartIndexViewModel>(view.Model).Error);
    }

    [Fact]
    public void AnUnknownProduct_IsNotFound() =>
        Assert.IsType<NotFoundResult>(new Visitor().Cart().Add("NOPE", 1));

    [Fact]
    public void RemoveAndClear_RedirectBackToTheCart_AndChangeTheModel()
    {
        var visitor = new Visitor();
        // Each call is a new request (a new controller), like in real life; the cookie is what links them.
        visitor.Do(c => c.Add("PEN-1", 1));
        visitor.Do(c => c.Add("MUG-1", 1));

        Assert.IsType<RedirectToActionResult>(visitor.Do(c => c.Remove("PEN-1")));
        var afterRemove = Assert.IsType<CartIndexViewModel>(Assert.IsType<ViewResult>(visitor.Do(c => c.Index())).Model);
        Assert.Equal(["MUG-1"], afterRemove.Lines.Select(l => l.Sku));

        Assert.IsType<RedirectToActionResult>(visitor.Do(c => c.Clear()));
        Assert.True(Assert.IsType<CartIndexViewModel>(Assert.IsType<ViewResult>(visitor.Do(c => c.Index())).Model).IsEmpty);
    }

    [Fact]
    public void TwoVisitors_HaveSeparateCarts()
    {
        var shared = new CartStore();
        var ana = new Visitor(shared);
        var bob = new Visitor(shared);
        var anaAdd = ana.Cart();
        anaAdd.Add("PEN-1", 1);
        ana.Remember(anaAdd);
        var bobIndex = bob.Cart();
        bobIndex.Index();
        bob.Remember(bobIndex);

        var anaModel = Assert.IsType<CartIndexViewModel>(Assert.IsType<ViewResult>(ana.Cart().Index()).Model);
        var bobModel = Assert.IsType<CartIndexViewModel>(Assert.IsType<ViewResult>(bob.Cart().Index()).Model);

        Assert.False(anaModel.IsEmpty);
        Assert.True(bobModel.IsEmpty);
    }
}
