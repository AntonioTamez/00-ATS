# Guía: Modular Monolith (monolito modular) paso a paso

Para un dev junior que quiere construir un proyecto básico con módulos de fronteras estrictas dentro de **un solo despliegue**. Cada afirmación del sample está verificada: 47 tests, incluidas reglas de frontera ejecutables, y análisis del binario con REA.

> Fuentes: [Kamil Grzybek – Modular Monolith with DDD](https://github.com/kgrzybek/modular-monolith-with-ddd) (las citas están copiadas de su README) y [Microsoft – Architecture styles](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/). El nombre "Contracts/Core", la separación en dos proyectos por módulo y las reglas exactas de este sample son **decisiones de este kit**, no un estándar: cada equipo las adapta (ver sección 6 y 14).

---

## 1. Qué es y qué problema resuelve

Un monolito normal es un solo despliegue. Su riesgo es que, sin fronteras, todo termina llamando a todo: el *big ball of mud* (Microsoft lo define como la situación en que *"dependencies aren't well managed and the structure breaks down"*).

La salida "de moda" es partirlo en microservicios, pero eso trae red, consistencia eventual, despliegues múltiples y observabilidad distribuida. Microsoft lo advierte: *"it introduces significant complexity in areas such as service discovery, data consistency, and distributed system management"*.

El **monolito modular** es el camino intermedio: **un solo proceso y un solo despliegue, dividido en módulos que no pueden tocarse por dentro**. Obtienes las fronteras de los microservicios sin pagar el costo de red.

| | Monolito sin fronteras | **Monolito modular** | Microservicios |
|---|---|---|---|
| Despliegue | 1 | **1** | N |
| Fronteras entre partes | Por convención (se rompen) | **Por compilador + tests** | Por red |
| Llamada entre partes | Cualquiera | **Contrato público / evento** | HTTP / mensajería |
| Datos | Compartidos | **Propios por módulo** | Propios por servicio |
| Costo operativo | Bajo | **Bajo** | Alto |

## 2. La idea en cinco reglas

1. **Un módulo = una capacidad de negocio** (Catálogo, Pedidos, Notificaciones), no una capa técnica.
2. **Todo lo de dentro es `internal`.** Lo único público del módulo es su *entry point* de registro y su carpeta de contratos.
3. **Los módulos solo se conocen por sus contratos** (interfaz pública + DTOs + eventos), nunca por su implementación.
4. **Cada módulo es dueño de sus datos.** Nadie lee tablas (o estructuras) de otro módulo.
5. **Las dependencias entre módulos forman un grafo sin ciclos.**

Así lo expresa Grzybek: *"Each Module is highly encapsulated - only required types and members are public, the rest are internal or private"* y *"Each Module has it's own data in a separate schema - shared data is not allowed"*.

## 3. Piezas del sample

```
Shop.Host                       ← composition root: conoce todos los módulos y los cablea
Shop.SharedKernel               ← lo mínimo compartido: bus de eventos + reloj

Catalog.Contracts   (público)   ← ICatalogApi, ProductInfo, ReservationResult
Catalog.Core        (interno)   ← Product, ProductStore, CatalogApi + CatalogModule (único público)

Ordering.Contracts  (público)   ← IOrderingApi, DTOs, eventos OrderPlaced / OrderCancelled
Ordering.Core       (interno)   ← Order, OrderStore, OrderingApi + OrderingModule

Notifications.Contracts         ← INotificationsApi, NotificationDto
Notifications.Core              ← handlers de eventos, NotificationLog + NotificationsModule
```

Dependencias del sample:

```
Notifications ──(evento OrderPlaced)──► Ordering ──(ICatalogApi)──► Catalog
```

## 4. Quién puede referenciar a quién

| Proyecto | Puede referenciar | NO puede referenciar |
|---|---|---|
| `X.Core` | `X.Contracts`, `Y.Contracts` (módulos de los que depende), `SharedKernel` | `Y.Core` (jamás) |
| `X.Contracts` | `SharedKernel` (solo para `IIntegrationEvent`) | Cualquier `*.Core` |
| `SharedKernel` | Nada propio | Ningún módulo |
| `Shop.Host` | Todos los `*.Core` (solo para llamar `AddXModule` / `MapXEndpoints`) | — |

Por qué dos proyectos por módulo: separar **lo que otros pueden ver** (`Contracts`) de **lo que no** (`Core`) hace que la frontera la imponga el **compilador** (no puedes escribir `using Catalog.Core` desde Ordering porque ni siquiera tienes la referencia).

## 5. Cómo se comunican los módulos (dos formas)

| Forma | Cuándo | En el sample |
|---|---|---|
| **Llamada a la API pública** (`ICatalogApi`) | Necesitas una **respuesta ahora** (¿hay stock? ¿a qué precio?) | `Ordering → Catalog.ReserveAsync` |
| **Evento de integración** (`OrderPlaced`) | Avisar "algo pasó" a quien le interese, sin esperar respuesta | `Ordering → Notifications` |

Importante: **Grzybek es más estricto**: *"Modules communicate each other only asynchronously using Events Bus - direct method calls are not allowed"*. Este sample permite además la llamada síncrona vía contrato porque es la opción pragmática más común y mantiene la frontera (solo se llama a una interfaz pública), pero el precio es acoplamiento en tiempo de ejecución: si Catalog falla, Ordering falla. Elige según tu caso; el resto de la guía vale en ambos estilos.

Regla de oro de los eventos: **el que publica no conoce a los que escuchan.** Notifications se suscribe en su propio `AddNotificationsModule()`; Ordering no sabe que existe.

## 6. Datos: cada módulo es dueño de los suyos

- En el sample, cada módulo tiene su propio almacén en memoria (`ProductStore`, `OrderStore`, `NotificationLog`), todos `internal`: nadie fuera del ensamblado puede leerlos (un test lo verifica).
- En un proyecto real: un **esquema** (o `DbContext`) por módulo. Sin `JOIN` entre esquemas de módulos distintos y sin foreign keys entre ellos: se referencia por **id** y, si necesitas datos del otro, se los pides por contrato.
- Grzybek: *"Modules don't share data so it is not possible nor desirable to create a transaction which spans more than one module"*. Por eso el sample **compensa a mano**: si la 2.ª línea del pedido no tiene stock, Ordering libera lo reservado de la 1.ª (`ReleaseAsync`).

## 7. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

```bash
mkdir MiShop && cd MiShop
dotnet new sln -n MiShop

# Compartido
dotnet new classlib -n Shop.SharedKernel -o src/Shared/Shop.SharedKernel

# Por cada módulo (Catalog, Ordering, Notifications)
dotnet new classlib -n Catalog.Contracts -o src/Modules/Catalog/Catalog.Contracts
dotnet new classlib -n Catalog.Core      -o src/Modules/Catalog/Catalog.Core
dotnet add src/Modules/Catalog/Catalog.Core reference src/Modules/Catalog/Catalog.Contracts

# Host y tests
dotnet new web   -n Shop.Host  -o src/Shop.Host
dotnet new xunit -n Shop.Tests -o tests/Shop.Tests
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj src,tests)   # PowerShell
```

En cada `*.Core` que exponga endpoints, añade `<FrameworkReference Include="Microsoft.AspNetCore.App" />` al `.csproj`.

**Orden de construcción:**

1. **SharedKernel**: `IIntegrationEvent`, `IEventBus`, `IIntegrationEventHandler<T>`, un `InMemoryEventBus`. Mantenlo diminuto.
2. **Primer módulo sin dependencias (Catalog)**: contratos primero (`ICatalogApi`, DTOs), luego el Core con todo `internal`, y una clase pública `CatalogModule` con `AddCatalogModule()` y `MapCatalogEndpoints()`.
3. **Test de ese módulo solo**, sin host ni HTTP (ver sección 9).
4. **Segundo módulo (Ordering)** que referencia únicamente `Catalog.Contracts`.
5. **Módulo reactivo (Notifications)**: solo suscripciones a eventos.
6. **Host**: llama a `AddSharedKernel()`, `AddXModule()` y `MapXEndpoints()`.
7. **Tests de fronteras** (sección 9) antes de que el proyecto crezca.

### La pieza clave: el entry point del módulo

```csharp
public static class CatalogModule              // ← lo único público de Catalog.Core
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddSingleton<ProductStore>();           // internal
        services.AddScoped<ICatalogApi, CatalogApi>();   // CatalogApi es internal
        return services;
    }
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app) { ... }
}
```

El host cablea todo en `Program.cs` sin ver ni una clase interna:

```csharp
builder.Services.AddSharedKernel();
builder.Services.AddCatalogModule();
builder.Services.AddOrderingModule();
builder.Services.AddNotificationsModule();
```

## 8. Qué va en cada sitio

| Pregunta | Respuesta |
|---|---|
| ¿Dónde va la regla "no se vende más de lo que hay en stock"? | Dentro de `Catalog.Core` (`Product.TryReserve`), nadie más la repite |
| ¿Dónde va "un pedido necesita al menos una línea"? | `Ordering.Core` (`Order.Validate`) |
| ¿Quién decide el precio unitario? | Catalog; Ordering lo recibe en `ReservationResult` |
| ¿Dónde va el DTO que devuelve la API? | En `Contracts` |
| ¿Dónde va la lógica de "avisar al cliente"? | `Notifications.Core`, nunca en Ordering |
| ¿Dónde va un helper que usan dos módulos? | Duplícalo, o deja que lo exponga el módulo dueño. **No lo metas al SharedKernel por comodidad** (ver errores) |

## 9. Tests: tres niveles

### Nivel 1: cada módulo aislado
`CatalogModuleTests` construye un `ServiceCollection` con **solo** `AddCatalogModule()`. `OrderingModuleTests` registra un `FakeCatalog` (detrás de `ICatalogApi`) y un bus que registra eventos. **Si un módulo no se puede probar solo, está acoplado.**

### Nivel 2: fronteras como reglas ejecutables (`ModuleBoundaryTests`)
Por reflexión sobre los ensamblados:

- Ningún `X.Core` referencia a otro `Y.Core`.
- Ningún `Contracts` referencia a un `Core`.
- Cada `Core` exporta **un solo tipo público**: `XModule`.
- Los `Contracts` solo exponen interfaces, enums y records sellados (datos).
- El grafo de dependencias es **exactamente** el declarado (Catalog ← Ordering ← Notifications) y **no tiene ciclos**.
- `SharedKernel` no conoce a ningún módulo.
- Ningún almacén de datos (`*Store`, `*Log`) es visible fuera de su ensamblado.

### Nivel 3: el monolito completo (`ApiTests`, con `WebApplicationFactory`)
Hacer un pedido baja el stock y llega la notificación; cancelar devuelve el stock; un pedido sin stock no deja rastro; **un suscriptor que explota no rompe el pedido ni a los demás suscriptores**.

### Prueba de mutación (que los tests de frontera muerden)
Añadí a `Ordering.Core` una referencia a `Catalog.Core` y una línea que la usa. Resultado: falla `Core_NeverReferences_AnotherModulesCore(name: "Ordering")` (46/47). Revertí el cambio.

Detalle que aprendí al hacerlo: el compilador de C# **no escribe en el ensamblado las referencias que no se usan**. Una referencia de proyecto "muerta" no la ve un test de reflexión; la ve la revisión del `.csproj`. Por eso la defensa real es en capas: la estructura de proyectos (compilador) + los tests (uso real) + revisión de PRs.

## 10. Verificación del binario con REA

Analicé los `.dll` compilados con REA (metadatos estáticos: no ejecuta nada).

| Hecho | Evidencia |
|---|---|
| `Ordering.Core.dll` referencia `Ordering.Contracts`, `Catalog.Contracts` y `Shop.SharedKernel`, y **ningún `*.Core`** | `inspect_managed_artifact`, `ev_7b6e3cc7…2062` |
| En `Ordering.Core.dll` solo `OrderingModule` tiene visibilidad pública; `Order`, `OrderLine`, `OrderStatus`, `OrderStore` y `OrderingApi` son `NotPublic` (los tipos generados por el compilador son privados anidados) | `inspect_managed_members`, `ev_26b32c7e…9ce9bc0` |

Lo que REA **no** prueba: que el sistema se comporte bien en ejecución (para eso están los 47 tests).

## 11. Receta: añadir el módulo "Shipping"

Requisito: cuando se hace un pedido, crear un envío.

1. `dotnet new classlib` ×2: `Shipping.Contracts` y `Shipping.Core`; añádelos a la solución.
2. `Shipping.Core` referencia `Ordering.Contracts` y `SharedKernel`. **Nada más.**
3. En `ShippingModule.AddShippingModule()` registra `IIntegrationEventHandler<OrderPlaced>` con un handler `internal`.
4. Su almacén propio (`ShipmentStore`, `internal`).
5. En el host: `AddShippingModule()` y `MapShippingEndpoints()`.
6. Añade `Shipping` a la lista de `ModuleBoundaryTests`. Si olvidaste algo, el test dice qué.

**Ordering no se toca.** Esa es la prueba de que el diseño funciona.

## 12. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| Un solo proyecto con carpetas "Catalog", "Ordering" | La frontera es una sugerencia; cualquier `using` la salta | Proyectos separados + `internal` + tests |
| `Core` público "para facilitar los tests" | Todo el mundo acaba usándolo | Prueba por la API pública del módulo |
| Un `Contracts` con lógica de negocio | Se convierte en un Core visible | Solo interfaces, DTOs y eventos |
| `SharedKernel` que crece (entidades, utilidades de negocio) | Es un monolito oculto: todo depende de él | Solo lo técnico mínimo |
| `JOIN` o FK entre esquemas de módulos | Los datos ya no son de nadie | Referencia por id; pide los datos por contrato |
| Eventos con todo el estado del agregado | Cualquier cambio rompe a los suscriptores | Eventos pequeños, estables y versionados |
| Dos módulos que se llaman entre sí | Ciclo: no se pueden separar jamás | Extrae un tercero o cambia a evento |
| Módulos = capas técnicas ("Data", "Services") | No sirven para separar el negocio | Módulos por **capacidad de negocio** |

## 13. Y para pasar a microservicios

La ventaja del monolito modular: **las costuras ya están hechas**. Para extraer `Catalog`:

1. Cambia la implementación de `ICatalogApi` (en Ordering se inyecta una interfaz) por un cliente HTTP.
2. Sustituye `InMemoryEventBus` por un broker real.
3. Mueve el esquema del módulo a su propia base de datos.
4. Despliega `Catalog.Core` con su propio host.

Eso solo es fácil si respetaste las reglas 2–4. Si hubo `JOIN` entre módulos, la extracción es una reescritura. Microsoft sugiere medir antes de decidir: *"Sometimes it's better to relax a constraint than to chase architectural purity"*. Muchos sistemas nunca necesitan este paso.

## 14. Límites de este sample (honestidad)

- **Eventos en memoria y síncronos**: si el proceso muere después de guardar el pedido y antes de publicar, el evento se pierde. En producción se necesita un *outbox*. Un handler que falla se registra en el log pero **no se reintenta**.
- **Sin transacción entre módulos**: `Ordering` reserva en Catalog y luego guarda su pedido. Si el proceso cae entre ambas cosas, queda stock reservado sin pedido. Hay que compensar o usar una saga.
- **Almacenes en memoria**: no hay persistencia real ni esquemas separados; esa parte del principio 4 está **descrita pero no demostrada** en el sample.
- **El aislamiento se basa en `internal` + proyectos + tests**, no en procesos: un módulo puede seguir consumiendo CPU o bloqueando hilos de los demás.
- **El sample usa llamada síncrona entre módulos** (ver sección 5). Es una decisión de diseño, no la única válida.

## 15. Checklist

- [ ] Cada módulo tiene `Contracts` y `Core` como proyectos separados
- [ ] `Core` solo expone su `XModule`; todo lo demás `internal`
- [ ] Ningún `Core` referencia a otro `Core`
- [ ] Las dependencias entre módulos son explícitas y no tienen ciclos
- [ ] Cada módulo tiene sus propios datos
- [ ] Cada módulo se prueba solo
- [ ] Hay tests de frontera y se mutaron al menos una vez
- [ ] El `SharedKernel` es diminuto y sin negocio
- [ ] Sabes qué eventos pueden perderse y qué pasa si ocurre
