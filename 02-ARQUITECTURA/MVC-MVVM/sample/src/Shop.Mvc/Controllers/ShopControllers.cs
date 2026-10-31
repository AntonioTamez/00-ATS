using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Shop.Model;
using Shop.Mvc.Models;

namespace Shop.Mvc.Controllers;

internal static class Money
{
    public static string Format(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture) + " €";
}

/// <summary>Takes the request, works with the Model, picks a View. It holds NO business rule: those live in Shop.Model.</summary>
public sealed class ProductsController(Catalog catalog, CartStore carts) : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        var cart = carts.GetOrCreate(HttpContext);
        var model = new ProductsIndexViewModel(
            catalog.All.Select(p => new ProductCard(p.Sku, p.Name, Money.Format(p.Price))).ToList(),
            cart.ItemCount);
        return View(model);
    }
}

public sealed class CartController(Catalog catalog, CartStore carts) : Controller
{
    [HttpGet("/cart")]
    public IActionResult Index() => View(BuildModel(carts.GetOrCreate(HttpContext), error: null));

    [HttpPost("/cart/add")]
    public IActionResult Add(string sku, int quantity)
    {
        var product = catalog.Find(sku);
        if (product is null) return NotFound();

        var cart = carts.GetOrCreate(HttpContext);
        try
        {
            cart.Add(product, quantity);
        }
        catch (CartRuleException ex)
        {
            // The rule was refused by the MODEL; the controller only chooses what to show.
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return View("Index", BuildModel(cart, ex.Message));
        }

        return RedirectToAction(nameof(Index));          // Post/Redirect/Get: refreshing the page does not add again
    }

    [HttpPost("/cart/remove")]
    public IActionResult Remove(string sku)
    {
        carts.GetOrCreate(HttpContext).Remove(sku);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/cart/clear")]
    public IActionResult Clear()
    {
        carts.GetOrCreate(HttpContext).Clear();
        return RedirectToAction(nameof(Index));
    }

    private static CartIndexViewModel BuildModel(Cart cart, string? error) => new(
        cart.Lines.Select(l => new CartLineView(l.Product.Sku, l.Product.Name, l.Quantity, Money.Format(l.Product.Price), Money.Format(l.Total))).ToList(),
        Money.Format(cart.Subtotal),
        Money.Format(cart.Discount),
        Money.Format(cart.Total),
        cart.Lines.Count == 0,
        error);
}
