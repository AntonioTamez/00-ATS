# Guía: Microservicios paso a paso

Para un dev junior que quiere entender **qué cambia de verdad** cuando las partes de un sistema dejan de compartir proceso, y construir un proyecto básico. El sample (catálogo + pedidos + gateway) tiene 49 tests, incluidos fallos de red simulados y reglas de independencia ejecutables, y sus binarios están analizados con REA.

> Fuentes: [Martin Fowler – Microservices](https://martinfowler.com/articles/microservices.html), [Fowler – MonolithFirst](https://martinfowler.com/bliki/MonolithFirst.html) y [Microsoft – Architecture styles](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/). Las citas están copiadas de esas páginas. La estructura del sample (puertos, nombres, política de reintentos, reglas de los tests) son **decisiones de este kit**, no un estándar.

---

## 1. Qué es

Fowler lo define como *"an approach to developing a single application as a suite of small services, each running in its own process"*, con servicios que *"are independently deployable"*. Microsoft añade: cada servicio *"implements a single business capability within a bounded context and is self-contained with its own data storage"* y se comunican *"through well-defined APIs"*.

Lo que lo distingue de un monolito modular **no es el código, es la red**:

| | Monolito modular | Microservicios |
|---|---|---|
| Llamada entre partes | En proceso: rápida, no falla por red | HTTP/mensajería: lenta, puede fallar, perderse o duplicarse |
| Despliegue | Uno | Uno por servicio |
| Datos | Esquemas separados, misma base | Bases separadas |
| Transacción entre partes | Posible, aunque no recomendada | **Imposible** |
| Fallos | Todo cae o nada | **Parciales**: uno cae y el resto sigue |

Fowler: *"Remote calls are more expensive than in-process calls"* y *"consistency may only be eventual consistency"*.

## 2. Antes de seguir: ¿lo necesitas?

Probablemente no, al principio. Fowler (MonolithFirst): *"you shouldn't start a new project with microservices"*, porque *"Almost all the successful microservice stories have started with a monolith that got too big and was broken up"* y *"even experienced architects working in familiar domains have great difficulty getting boundaries right at the beginning"*. Su recomendación del artículo principal: *"begin with a monolith, keep it modular, and split it into microservices once the monolith becomes a problem"*. Y el coste: *"This premium, essentially the cost of managing a suite of services, will slow down a team"*.

Microsoft: es adecuado para *"complex domains that require frequent changes"*, pero *"Success requires mature development and DevOps practices"*.

Úsalo cuando tengas un dolor concreto que el monolito modular no resuelve: equipos que necesitan desplegar de forma independiente, partes con necesidades de escala o disponibilidad muy distintas, o tecnologías distintas por servicio.

## 3. Las cuatro características que importan

| Característica | En el sample | Fuente |
|---|---|---|
| **Desplegable por separado** | Cada servicio es su propio proyecto web con su propio `Program.cs` | Fowler: *"independently deployable"* |
| **Datos propios** | `StockStore` (Catalog) y `OrderStore` (Orders) son `internal`, nadie más los ve | Fowler: *"letting each service manage its own database"* |
| **Inteligencia en los extremos, tuberías tontas** | El gateway solo enruta; la lógica vive en los servicios | Fowler: *"smart endpoints and dumb pipes"* |
| **Diseñado para el fallo** | Timeouts, un reintento, idempotencia, compensación y respuestas 503/502/504 | Fowler: *"applications need to be designed so that they can tolerate the failure of services"* |

## 4. Estructura del sample

```
Gateway           ← puerta de entrada: /api/catalog/** y /api/orders/** (no tiene negocio)
Catalog.Service   ← productos y stock. Datos propios (StockStore)
Orders.Service    ← pedidos. Datos propios (OrderStore). Llama a Catalog por HTTP
Platform.Tests    ← el ÚNICO proyecto que ve a los tres (para arrancarlos en memoria)
```

```
cliente ──► Gateway ──► Orders.Service ──HTTP──► Catalog.Service
                                   (POST /reservations, DELETE /reservations/{id})
```

## 5. Reglas de independencia

| Regla | Cómo se verifica |
|---|---|
| Un servicio **no referencia a otro servicio** | Test de arquitectura + REA |
| **No hay librería compartida** con tipos de negocio | Test: cada servicio solo referencia ensamblados del framework |
| Cada servicio tiene **su copia del contrato** del vecino | `CatalogClient` en Orders define sus propios DTOs |
| Los datos son **privados** | Test: los `*Store` no son visibles fuera de su ensamblado |
| El gateway **no tiene tipos de negocio** | Test: solo expone `GatewayEntry` |

Los servicios comparten un **protocolo** (HTTP + JSON), no código. Si Catalog cambia su protocolo de forma incompatible, lo único que se rompe en Orders es `CatalogClient`. Fowler lo aclara: *"a good in-process interface is usually not a good service interface"* (por eso no basta con "poner HTTP delante").

## 6. Hablar con otro servicio: las cinco defensas

La red falla de formas que una llamada en proceso nunca conoce. `CatalogClient` aplica:

1. **Timeout** (2 s): jamás esperes para siempre. Sin timeout, un servicio lento arrastra a todos los que lo llaman.
2. **Reintento limitado** (1 reintento) ante red caída, timeout o 5xx. Un 4xx (negocio) **no** se reintenta.
3. **Idempotencia**: reintentar solo es seguro si repetir la petición no repite el efecto. `POST /reservations` lleva un `reservationId`; si Catalog lo recibe dos veces con el mismo contenido, responde `200` en vez de `201` y **no reserva dos veces**. Esto cubre el caso clásico: *Catalog hizo el trabajo, pero la respuesta se perdió*.
4. **Compensación**: no hay transacción distribuida. Si la 2.ª línea falla, Orders deshace la 1.ª llamando a `DELETE /reservations/{id}` (también idempotente: liberar dos veces devuelve el stock una sola).
5. **Fallar con claridad**: Orders responde `503` + `Retry-After` cuando una dependencia no responde, y **no crea el pedido**. El gateway traduce caídas en `502` y timeouts en `504`.

Además `Idempotency-Key` en `POST /orders` protege al cliente que reintenta: misma clave, mismo pedido.

Qué falta en producción: *backoff* con *jitter* entre reintentos y un **circuit breaker** (dejar de llamar a quien está caído). En .NET se añaden con el paquete de resiliencia de `Microsoft.Extensions.Http`; aquí se omiten a propósito para ver el mecanismo desnudo.

## 7. Datos: cada servicio, los suyos

- Orders **no** consulta la tabla de stock: pregunta a Catalog por su API.
- Orders guarda el **precio unitario** que Catalog le devolvió en la reserva (una copia, no una referencia): el pedido no cambia si el precio cambia después.
- No hay `JOIN` entre servicios. Si necesitas datos combinados, los compones en el servicio que los necesita (o con un modelo de lectura, ver CQRS).

## 8. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

```bash
mkdir MiPlataforma && cd MiPlataforma
dotnet new sln -n MiPlataforma
dotnet new web -n Catalog.Service -o src/Catalog.Service
dotnet new web -n Orders.Service  -o src/Orders.Service
dotnet new web -n Gateway         -o src/Gateway
dotnet new xunit -n Platform.Tests -o tests/Platform.Tests
dotnet add tests/Platform.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/Platform.Tests reference src/Catalog.Service src/Orders.Service src/Gateway
```

**Ni** `Orders.Service` **ni** `Gateway` referencian a otro proyecto de la solución.

**Orden de construcción:**

1. **El servicio hoja primero (Catalog)**: sus endpoints, su almacén `internal`, `/health`. Hazlo **idempotente** desde el primer día (id de reserva generado por el cliente).
2. **Tests del servicio solo**, por HTTP (`WebApplicationFactory`).
3. **El servicio que depende (Orders)** con su propio `CatalogClient` (DTOs propios) y un `HttpClient` con nombre y timeout.
4. **Tests con un "enlace" controlable** entre ambos (ver sección 10): ahí se prueban los fallos.
5. **Gateway**: tabla de rutas y reenvío. Sin lógica.
6. **Tests de independencia** (sección 10).

### Ejecutarlo de verdad (tres procesos)
```bash
dotnet run --project src/Catalog.Service --urls http://localhost:5101
dotnet run --project src/Orders.Service  --urls http://localhost:5102   # usa Services:Catalog (por defecto http://localhost:5101)
dotnet run --project src/Gateway         --urls http://localhost:5100   # usa Services:Catalog y Services:Orders
```
Las direcciones se configuran con `Services:Catalog` / `Services:Orders` (variables de entorno `Services__Catalog`, etc.). En el sample, los puertos por defecto son decisión de este kit. **No lo ejecuté como tres procesos reales**: los tests arrancan los tres servicios en memoria y conectan sus `HttpClient` entre sí.

## 9. Las piezas clave

### Un servicio no sabe cómo funciona el vecino, solo su protocolo
```csharp
// Orders.Service/CatalogClient.cs: su PROPIA vista del contrato de Catalog
private sealed record ReservedBody(decimal UnitPrice);

using var response = await SendAsync(
    client => client.PostAsJsonAsync("/reservations", new { reservationId, sku, quantity }, ct), ct);
```

### La reserva idempotente (lado Catalog)
```csharp
if (_reservations.TryGetValue(reservationId, out var existing))
{
    var samePayload = existing.Sku == sku && existing.Quantity == quantity;
    return samePayload && !existing.Released
        ? new ReserveOutcome(ReserveStatus.Ok, existing.UnitPrice, Replayed: true)  // 200: ya estaba hecho
        : new ReserveOutcome(ReserveStatus.IdConflict);                             // mismo id, otra petición: 409
}
```

### Compensar en lugar de hacer rollback
```csharp
var reservationId = Guid.NewGuid();
attempted.Add(reservationId);          // lo anotamos ANTES de llamar: aunque la respuesta se pierda, sabemos qué deshacer
var result = await catalog.ReserveAsync(reservationId, sku, quantity, ct);
if (result.Status != ReserveStatus.Ok) { await CompensateAsync(attempted); ... }
```

## 10. Tests

| Qué | Dónde | Idea |
|---|---|---|
| Un servicio solo | `CatalogServiceTests` | HTTP real en memoria; idempotencia, conflictos, liberar dos veces |
| Dos servicios con red rota | `OrdersServiceTests` | Un `ScriptedLink` entre Orders y Catalog: caído, timeout, 500 permanente, 503 transitorio, **respuesta perdida**, caída tras hacer el trabajo |
| El punto de entrada | `GatewayTests` | Quita el prefijo, reenvía cuerpo/cabeceras/`Location`, 404/502/504, un servicio caído no tumba a los demás |
| Independencia | `ArchitectureTests` | Ningún servicio referencia nada nuestro; los `*Store` no son visibles; el gateway no tiene negocio |

### El enlace controlable (la idea más útil de este kit)
`ScriptedLink` envuelve el *handler* que conecta dos servicios y permite escribir tests como: *"Catalog ejecuta la reserva pero la respuesta se pierde; Orders reintenta; el stock debe bajar una sola vez"*. Sin esto, los fallos distribuidos solo se descubren en producción.

### Pruebas de mutación (que los tests muerden)
Hechas en una copia temporal, sin tocar el código real:

1. **Orders referencia a Catalog.Service y lo usa** → falla `AService_ReferencesNothingOfOurs…(Orders.Service)`.
2. **Quité la idempotencia de la reserva** → fallan 4 o más tests, incluidos `WhenTheReplyIsLost_TheRetryDoesNotReserveTwice…` y `Reserve_Repeated_WithTheSameId_IsIdempotent`.
3. **Quité la compensación** → fallan `WhenALaterLineHasNoStock_TheEarlierReservationsAreUndone` y `…TheOrphanReservationIsReleasedWhenPossible`.

## 11. Verificación del binario con REA

Analicé los `.dll` compilados con REA (metadatos estáticos; no ejecuta nada).

| Hecho | Evidencia |
|---|---|
| `Orders.Service.dll` referencia solo ensamblados del framework (`System.*`, `Microsoft.*`): **ningún proyecto nuestro**, ni `Catalog.Service` | `inspect_managed_artifact`, `ev_a6f31194…469f` |
| `Gateway.dll` referencia solo ensamblados del framework | `inspect_managed_artifact`, `ev_cd14ec26…1ae1` |

REA **no** prueba que los servicios se comporten bien ante fallos (para eso están los 49 tests) ni que se comuniquen por red real.

## 12. Receta: añadir un servicio "Notifications"

1. `dotnet new web -n Notifications.Service`; añádelo a la solución y a `Platform.Tests`.
2. **No** referencia a nadie. Expone, por ejemplo, `POST /notifications`.
3. `Orders.Service` crea su `NotificationsClient` con sus propios DTOs, un `HttpClient` con nombre `notifications` y timeout.
4. Decide: ¿el pedido debe **fallar** si no se puede notificar? Casi seguro que no: es un efecto secundario. Entonces la notificación debería ser **asíncrona** (un evento), no una llamada síncrona. Ese es el siguiente tema del catálogo (Event-Driven).
5. Añade el servicio al gateway (una línea en la tabla de rutas).
6. Test con `ScriptedLink`: *"Notifications caído → el pedido sigue creándose"*.

### Cambiar un contrato sin romper a nadie
- **Añadir** campos opcionales a una respuesta: seguro (el cliente ignora lo que no conoce).
- **Quitar o renombrar** campos o cambiar significado: rompe. Publica `/v2` y retira `/v1` cuando nadie lo use.
- Los clientes deben ser **tolerantes** (leer solo lo que necesitan).

## 13. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| **Monolito distribuido**: servicios que se llaman en cadena y despliegan siempre juntos | Tienes todos los costes de la red y ninguna ventaja | Revisa las fronteras; une servicios que cambian juntos |
| **Base de datos compartida** | Los servicios se acoplan por las tablas | Datos privados; datos ajenos, solo por API |
| **Librería compartida con el dominio** | Todo se versiona y despliega junto | Compartir protocolo, no código |
| **Sin timeouts** | Un servicio lento congela a los demás | Timeout siempre |
| **Reintentar sin idempotencia** | Dobles cobros, doble stock | Ids de petición / claves de idempotencia |
| **Cadenas síncronas largas** (A→B→C→D) | La disponibilidad se multiplica: 4 servicios al 99 % ≈ 96 % | Reducir saltos; usar eventos |
| **Llamadas muy habladoras** (N+1 por HTTP) | Latencia | APIs de más grano o modelos de lectura propios |
| **Empezar con microservicios** | Fronteras equivocadas y coste de operación desde el día 1 | Monolito modular primero (Fowler) |
| **Sin observabilidad** | No sabes qué servicio falló | Logs correlacionados, trazas distribuidas, `/health` |

## 14. Límites de este sample (honestidad)

- **Tres servicios en memoria dentro de un test**, no tres procesos en una red: el *handler* de pruebas sustituye al HTTP real. Sirve para comprobar la lógica de fallos, no la latencia ni la configuración real de red.
- **Sin circuit breaker, backoff ni jitter** (ver sección 6).
- **Compensación de mejor esfuerzo**: si Catalog está caído justo cuando Orders intenta liberar una reserva, esa reserva queda huérfana. En producción hace falta una cola de reintentos o un proceso de reconciliación; aquí no existe.
- **Idempotency-Key en memoria y sin atomicidad**: dos peticiones simultáneas con la misma clave podrían crear dos pedidos.
- **Sin autenticación, descubrimiento de servicios, configuración centralizada ni trazas**.
- **Gateway rudimentario**: para producción se usa un proxy inverso probado (por ejemplo YARP) en vez de reenviar a mano.
- **Sin pruebas de contrato dirigidas por el consumidor** (p. ej. Pact): hoy el contrato entre Orders y Catalog se verifica con tests de integración, pero nada avisa a Catalog si rompe a Orders sin ejecutar ambos.
- **Sin mensajería asíncrona**: toda la comunicación es síncrona. Ver Event-Driven.

## 15. Checklist

- [ ] ¿Tengo un dolor que justifique salir del monolito modular?
- [ ] Cada servicio se despliega solo y tiene sus propios datos
- [ ] Ningún servicio referencia a otro ni a una librería de negocio compartida
- [ ] Toda llamada entre servicios tiene timeout y un plan para el fallo
- [ ] Los reintentos son seguros (idempotencia)
- [ ] Sé qué se compensa y qué pasa si la compensación también falla
- [ ] Hay tests con fallos de red simulados, y probé al menos una mutación
- [ ] Puedo ver, con logs y trazas, qué servicio falló
