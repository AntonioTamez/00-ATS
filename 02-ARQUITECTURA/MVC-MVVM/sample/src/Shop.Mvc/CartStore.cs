using Shop.Model;

namespace Shop.Mvc;

/// <summary>One cart per visitor, found through a cookie. It lives in memory: a demo, not a production session store.</summary>
public sealed class CartStore
{
    public const string CookieName = "cart-id";

    private readonly object _gate = new();
    private readonly Dictionary<Guid, Cart> _carts = [];

    public Cart GetOrCreate(HttpContext http)
    {
        lock (_gate)
        {
            if (Guid.TryParse(http.Request.Cookies[CookieName], out var id) && _carts.TryGetValue(id, out var existing))
                return existing;

            var cart = new Cart();
            var newId = Guid.NewGuid();
            _carts[newId] = cart;
            http.Response.Cookies.Append(CookieName, newId.ToString(), new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
            return cart;
        }
    }
}
