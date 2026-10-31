# Guía: MVC y MVVM paso a paso

Para un dev junior que quiere entender los dos patrones de presentación más usados en .NET y construir un ejemplo de cada uno sobre **el mismo modelo**. El sample (un carrito de la compra) tiene una web MVC con vistas Razor, un ViewModel de MVVM y una vista de consola que se enlaza a él; sus binarios están analizados con REA.

> Estado de las pruebas (leer antes de fiarse): en la primera ejecución pasaron **62 de 66** tests. Los 4 fallos los atribuí, **por inspección del código**, a errores **míos en los tests** (el ayudante de cookies de los tests de controlador, y el euro que Razor escribe codificado como `&#x20AC;`). Apliqué esas correcciones, pero **no pude volver a ejecutar la suite**: Windows (Control de aplicaciones) bloqueó la carga de `Shop.Tests.dll` recompilado (error 0x800711C7). No hice pruebas de mutación en este kit por el mismo motivo. Ver sección 10.

> Fuentes: [Martin Fowler – GUI Architectures](https://martinfowler.com/eaaDev/uiArchs.html), [Microsoft – ASP.NET Core MVC overview](https://learn.microsoft.com/en-us/aspnet/core/mvc/overview) y [Microsoft – MVVM (.NET MAUI)](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm). Las citas están copiadas de esas páginas. Los nombres de clases, la estructura del sample y las reglas de los tests son **decisiones de este kit**.

---

## 1. Qué son (y qué no son)

MVC y MVVM **no son arquitecturas de aplicación** como las del resto del catálogo. Son **patrones de presentación**: organizan el código de la parte que habla con el usuario. Viven *dentro* de la capa de presentación (o del adaptador de entrada) de una arquitectura mayor.

La idea común, según Fowler: *"the separation of presentation from domain logic and synchronizing presentation state through events"*. Y la regla de oro: *"Model objects are completely ignorant of the UI."*

| | MVC | MVVM |
|---|---|---|
| Piezas | Model, View, Controller | Model, View, ViewModel |
| Quién recibe la acción del usuario | El **controlador** | La **vista** la reenvía a un **comando** del ViewModel |
| Cómo se actualiza la pantalla | El controlador elige una vista y le da datos | La vista **observa** al ViewModel (binding) y se redibuja sola |
| Dónde encaja | Web servidor (petición → respuesta) | Interfaces con estado: escritorio, móvil (XAML) |
| En el sample | `Shop.Mvc` | `Shop.ViewModels` + `Shop.ConsoleView` |

## 2. MVC en una frase por pieza

Microsoft: *"the Model... represents the state of the application and any business logic"*; *"Views are responsible for presenting content"* con *"minimal logic within views"*; y *"Controllers are the components that handle user interaction, work with the model, and ultimately select a view to render"*. Fowler: *"The controller's job is to take the user's input and figure out what to do with it."*

La dependencia (Microsoft): *"Both the view and the controller depend on the model. However, the model doesn't depend on the view or the controller."* Y una advertencia sobre controladores: *"push business logic out of the controller and into the domain model"*.

```
Navegador ──petición──► Controlador ──usa──► Modelo (reglas)
                            │
                            └─ elige y llena ──► Vista (HTML) ──respuesta──► Navegador
```

## 3. MVVM en una frase por pieza

Microsoft: la vista *"is responsible for defining the structure, layout, and appearance of what the user sees"*; el ViewModel *"implements properties and commands to which the view can data bind to, and notifies the view of any state changes through change notification events"*; el modelo *"encapsulates the app's business logic and data"*.

La dependencia (Microsoft): *"the view 'knows about' the view model, and the view model 'knows about' the model, but the model is unaware of the view model, and the view model is unaware of the view."* Y: *"don't reference view types, such as Button and ListView, from view models."*

```
Vista ──observa y envía comandos──► ViewModel ──usa──► Modelo (reglas)
  ▲                                      │
  └──── PropertyChanged / CollectionChanged ┘     (el ViewModel no sabe quién escucha)
```

## 4. La confusión más común: dos significados de "ViewModel"

| | ViewModel en **MVC** | ViewModel en **MVVM** |
|---|---|---|
| Naturaleza | **Pasivo**: una bolsa de datos para una vista | **Activo**: estado + comandos + notificaciones |
| Vida | Una petición | Mientras la pantalla existe |
| Notifica cambios | No | Sí (`INotifyPropertyChanged`) |
| Tiene comandos | No | Sí (`ICommand`) |
| Lo crea | El controlador, en cada petición | La composición de la app |
| En el sample | `ProductsIndexViewModel`, `CartIndexViewModel` (records inmutables) | `ShopViewModel` |

Un test de arquitectura codifica esta diferencia: los view models de MVC no implementan `INotifyPropertyChanged`, no tienen comandos y sus propiedades son `init`; el `ShopViewModel` sí notifica y expone `ICommand`.

## 5. Estructura del sample

```
Shop.Model          ← el dominio: Cart, Catalog, Product, reglas (máx. 10 por línea, 10 % de descuento desde 100 €)
Shop.Mvc            ← MVC web: controladores, view models pasivos, vistas Razor (.cshtml)
Shop.ViewModels     ← MVVM: ObservableObject, RelayCommand, ShopViewModel
Shop.ConsoleView    ← una VISTA para MVVM dibujada en consola (y su Program, el composition root)
Shop.Tests
```

Las dos presentaciones comparten el **mismo** `Shop.Model`: las reglas se escriben una vez y las hacen cumplir ambas.

## 6. Reglas y cómo se verifican

| Regla | Cómo se verifica |
|---|---|
| El Model no conoce ninguna UI ni otro proyecto | Test + REA (`ev_f9e5d24a3…4c4b`) |
| MVC depende solo del Model | Test + REA (`ev_79fbf92a6…8a8f`) |
| El ViewModel depende solo del Model y de ningún framework de UI | Test + REA (`ev_90f08b0a8…8f40`) |
| La vista de consola no usa el Model: solo el ViewModel | Test de firmas + **REA a nivel de IL**: 0 llamadas de la vista al Model |
| Los controladores devuelven `IActionResult` | Test |
| Los view models de MVC son pasivos e inmutables | Test |
| Las vistas `.cshtml` no contienen código del Model ni `@inject` | Test que escanea los archivos |
| Los comandos del ViewModel se exponen como `ICommand`, no como la clase concreta | Test |
| Solo `SelectedProduct` y `Quantity` son escribibles desde la vista | Test |
| Todo POST de la web lleva token anti-falsificación | Filtro global + test (POST sin token → 400) |

## 7. Paso a paso

Requisito: .NET 10 SDK.

### 7.1 El Model primero (sirve a los dos patrones)
```bash
dotnet new classlib -n Shop.Model -o src/Shop.Model
```
`Cart` con sus reglas, sin nada de UI. Pruébalo solo: `ModelTests`.

### 7.2 MVC
```bash
dotnet new web -n Shop.Mvc -o src/Shop.Mvc
dotnet add src/Shop.Mvc reference src/Shop.Model
```
1. `AddControllersWithViews` y un filtro global `AutoValidateAntiforgeryTokenAttribute` (protege cada POST contra CSRF).
2. **Controladores finos**: reciben la petición, usan el Model, eligen la vista. Los POST hacen *Post/Redirect/Get*: redirigen tras añadir para que refrescar la página no repita la acción.
3. **View models pasivos** (records) con los datos ya formateados.
4. **Vistas Razor** fuertemente tipadas (`@model ...`). Los formularios usan `asp-controller`/`asp-action` para que Razor inserte el token anti-falsificación.
5. Pruébalo en tres niveles: controlador sin servidor, HTTP real y reglas de arquitectura.

### 7.3 MVVM
```bash
dotnet new classlib -n Shop.ViewModels -o src/Shop.ViewModels
dotnet add src/Shop.ViewModels reference src/Shop.Model
```
1. `ObservableObject`: `INotifyPropertyChanged` con un `Set` que **solo notifica si el valor cambió**.
2. `RelayCommand`: `ICommand` con `CanExecute` y `RaiseCanExecuteChanged`.
3. `ShopViewModel`: colecciones observables, propiedades editables (selección, cantidad), textos ya formateados y tres comandos.
4. Pruébalo **sin vista**: es la gran ventaja del patrón (Microsoft: *"Developers can create unit tests for the view model and the model, without using the view"*).

### 7.4 Una vista para el ViewModel
```bash
dotnet new console -n Shop.ConsoleView -o src/Shop.ConsoleView
dotnet add src/Shop.ConsoleView reference src/Shop.ViewModels
```
La vista se suscribe a `PropertyChanged`/`CollectionChanged` y se redibuja; reenvía la entrada del usuario a los comandos. **No contiene reglas.** En XAML esto sería `{Binding ...}` y `Command="{Binding ...}"`; aquí lo hacemos a mano para poder probarlo sin un framework de UI.

## 8. Las piezas clave

### MVC: el controlador no decide reglas, elige qué mostrar
```csharp
[HttpPost("/cart/add")]
public IActionResult Add(string sku, int quantity)
{
    var product = catalog.Find(sku);
    if (product is null) return NotFound();
    var cart = carts.GetOrCreate(HttpContext);
    try { cart.Add(product, quantity); }                   // la REGLA vive en el Model
    catch (CartRuleException ex)
    {
        Response.StatusCode = 422;
        return View("Index", BuildModel(cart, ex.Message)); // el controlador solo elige la vista
    }
    return RedirectToAction(nameof(Index));                 // Post/Redirect/Get
}
```

### MVVM: un comando con "¿puedo?", y un fallo convertido en estado
```csharp
_add = new RelayCommand(_ => AddSelected(), _ => SelectedProduct is not null && Quantity > 0);

private void AddSelected()
{
    try { _cart.Add(SelectedProduct!.Product, Quantity); StatusMessage = $"Added {Quantity} x {SelectedProduct.Name}."; }
    catch (CartRuleException ex) { StatusMessage = ex.Message; }   // no es una excepción para la vista: es estado
    Refresh();
}
```

### La notificación (lo que hace "reactiva" a la vista)
```csharp
protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
{
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;   // no notifica si no cambió
    field = value; Raise(name); return true;
}
```
Microsoft lo exige: *"Never raising a PropertyChanged event if the property does not change"* y *"Always raising a PropertyChanged event for any calculated properties"* (por eso los totales se notifican tras cada cambio).

### La vista de MVVM solo observa y reenvía
```csharp
_vm.PropertyChanged += OnViewModelChanged;            // observar
_vm.Lines.CollectionChanged += OnLinesChanged;
...
Execute(_vm.AddSelectedCommand, null, "add");         // reenviar la acción del usuario al comando
```

## 9. Tests

| Qué | Dónde | Idea |
|---|---|---|
| Reglas del dominio | `ModelTests` | Sin UI: fusionar líneas, máximo 10, descuento exacto en 100 €, quitar, vaciar |
| Controladores MVC | `MvcControllerTests` | Sin servidor: un controlador es una clase que devuelve un resultado. Un `Visitor` simula las cookies entre peticiones |
| MVC por HTTP | `MvcHttpTests` | Enrutado, HTML renderizado, token anti-falsificación, 422, 404, carritos separados por navegador |
| ViewModel | `ViewModelTests` | Comandos habilitados/deshabilitados, notificaciones (cada propiedad una vez), reglas rechazadas como estado |
| Vista | `ConsoleViewTests` | Se le da entrada y se lee lo que dibuja |
| Reglas de diseño | `ArchitectureTests` | Ver sección 6 |

Dato práctico: Razor escribe los caracteres no ASCII codificados (el euro sale como `&#x20AC;`). Un navegador lo decodifica; en los tests hay que decodificar el HTML antes de comparar.

## 10. Estado de verificación (honesto)

- **Primera ejecución: 62/66.** Fallaron 4 tests. Mi diagnóstico, por lectura del código y de los mensajes de fallo (no confirmado ejecutando de nuevo): (1) el ayudante de cookies reutilizaba un controlador entre peticiones, lo que crea carritos distintos (el fallo mostraba una lista vacía donde se esperaba `MUG-1`); (2)-(4) tres tests de HTTP comparaban el euro sin decodificar el HTML (el fallo era "subcadena no encontrada").
- **Corregido, sin volver a ejecutar.** Al recompilar `Shop.Tests.dll`, Windows lo bloqueó (*"Una directiva de Control de aplicaciones bloqueó este archivo"*, 0x800711C7). No esquivé la política, así que **esos 4 tests corregidos no están verificados**; hay que ejecutar `dotnet test` en una máquina donde cargue.
- **Pruebas de mutación: no realizadas** en este kit.
- **REA sí se ejecutó** sobre los binarios de esa primera compilación (los `.dll` de `src/`, que no cambiaron después).

## 11. Verificación del binario con REA

| Hecho | Evidencia |
|---|---|
| `Shop.Model.dll` referencia solo ensamblados del framework: **ningún proyecto nuestro, ninguna UI** | `ev_f9e5d24a3…4c4b` |
| `Shop.ViewModels.dll` referencia, de lo nuestro, **solo `Shop.Model`**, y ningún ensamblado de UI | `ev_90f08b0a8…8f40` |
| `Shop.Mvc.dll` referencia, de lo nuestro, **solo `Shop.Model`** | `ev_79fbf92a6…8a8f` |
| En `Shop.ConsoleView.dll`: **0 llamadas desde `ConsoleShopView` (y sus cierres) hacia `Shop.Model`**; solo `Program` (el composition root) construye `Catalog` y `Cart` | `inspect_managed_members`, `call_edges`, `ev_3a3552cf9…5a10` |

El último punto es la comprobación que un test de firmas no puede hacer: mira **dentro de los cuerpos de los métodos**. REA **no** prueba el comportamiento en ejecución.

## 12. Receta: añadir un cupón de descuento

1. **Model**: `Cart.ApplyCoupon(code)` con su regla y su test. (Una sola vez.)
2. **MVC**: acción `POST /cart/coupon` que llama al Model; el view model del carrito gana `CouponText`; la vista Razor muestra el campo y el texto.
3. **MVVM**: propiedad editable `CouponCode`, comando `ApplyCouponCommand` (habilitado si hay texto), y que `Refresh()` actualice los totales.
4. Los dos reutilizan la **misma** regla. Si la regla se hubiera escrito en el controlador o en el ViewModel, habría que duplicarla.

## 13. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| Reglas de negocio en el controlador o en el code-behind | No se reutilizan ni se prueban sin UI | Al Model |
| Controlador "gordo" | Imposible de mantener | Controladores finos; reglas en el Model |
| Lógica en la vista Razor (`@if` con reglas, cálculos) | Mezcla presentación con negocio | Calcular en el view model; la vista solo pinta |
| ViewModel que referencia controles (`Button`, `ListView`) | No se puede probar sin la UI | El ViewModel expone estado y comandos |
| Habilitar/deshabilitar botones desde el code-behind | La lógica de estado se esconde en la vista | `CanExecute` en el comando (Microsoft lo recomienda) |
| No notificar un cambio (o notificar sin cambio) | La vista se queda desfasada o parpadea | `Set` con comparación; notificar propiedades calculadas |
| Pasar entidades del Model directamente a la vista | La vista queda acoplada al dominio | View models con datos ya formateados |
| POST sin protección CSRF | Cualquier web ajena podría enviar formularios por el usuario | Token anti-falsificación en todo POST |
| Confundir "ViewModel" de MVC con el de MVVM | Se diseña el equivocado | Sección 4 |
| Usar MVC/MVVM como "la arquitectura" de todo el sistema | Son patrones de la capa de presentación | Combinar con Layered/Clean/Hexagonal |

## 14. Límites de este sample (honestidad)

- **La vista de MVVM es de consola**, no XAML/MAUI/WPF: demuestra el patrón (observar + comandos) y se puede probar, pero no un framework de UI real con *data binding* automático.
- **El carrito web vive en memoria** (`CartStore`, una cookie `HttpOnly`): es una demo, no una sesión de producción. No hay autenticación, persistencia ni concurrencia entre peticiones del mismo usuario.
- **No hay MVP, Razor Pages ni Blazor**; se mencionan solo en la comparativa.
- **Todo es síncrono**; en MVVM real las operaciones de E/S son asíncronas (Microsoft lo recomienda).
- **Las tres afirmaciones de verificación** de la sección 10 (62/66, tests corregidos sin re-ejecutar, sin mutaciones) son el estado real.

## 15. Checklist

- [ ] ¿Está el Model libre de cualquier referencia a la UI?
- [ ] Mis controladores (o ViewModels) solo coordinan; las reglas viven en el Model
- [ ] Las vistas solo pintan
- [ ] Sé qué es un "ViewModel" en mi patrón (pasivo o activo)
- [ ] En MVVM, los comandos exponen `CanExecute` y la vista no habilita botones a mano
- [ ] Notifico cambios solo cuando cambian, incluidas las propiedades calculadas
- [ ] En web, todo POST lleva token anti-falsificación y uso Post/Redirect/Get
- [ ] Probé el Model y el ViewModel/controladores sin la UI
- [ ] Hay tests de arquitectura que fijan quién conoce a quién
