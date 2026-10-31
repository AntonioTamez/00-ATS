# Guía: Vertical Slice Architecture paso a paso (.NET 10)

Con esta guía un dev junior puede construir un proyecto con Vertical Slice desde cero. El resultado es el proyecto de referencia en [`../sample`](../sample) (un helpdesk de tickets). Compara siempre con él.

> Fuente primaria: [Vertical Slice Architecture – Jimmy Bogard](https://www.jimmybogard.com/vertical-slice-architecture/). Para cuándo elegirla: [Milan Jovanović](https://milanjovanovic.tech/blog/when-to-choose-vertical-slice-architecture).
> Para comparar con las otras: [VERTICAL-SLICE-VS-CAPAS.md](VERTICAL-SLICE-VS-CAPAS.md).

---

## 1. Qué problema resuelve

En una arquitectura por capas (incluidas Onion y Clean), el código se organiza por **rol técnico**: controllers, servicios, repositorios. Pero un cambio real casi siempre es una **funcionalidad** ("permitir cerrar un ticket"), así que tocas una pieza en cada capa.

Bogard lo plantea así:
- Clean/Onion encajan bien solo en una **minoría** de las peticiones típicas de un sistema.
- La cadena obligatoria "el controller **debe** hablar con un servicio que **debe** usar un repositorio" añade abstracciones que rara vez ayudan.

## 2. La idea central

> *"Remove the gates and barriers across those layers, and couple along the axis of change."*

Organiza el código por **petición** (la funcionalidad), no por capa técnica. Cada *slice* contiene todo lo que esa petición necesita, del HTTP al almacenamiento.

> *"Minimize coupling between slices, and maximize coupling in a slice."*

- **Entre slices:** casi nada. Una slice no llama a otra.
- **Dentro de una slice:** todo junto y cohesionado.

Consecuencia que cita Bogard: *"New features only add code, you're not changing shared code and worrying about side effects."* Una funcionalidad nueva es una carpeta nueva, no un cambio repartido por cinco capas.

```
 POR CAPAS (cada feature atraviesa todas)        VERTICAL SLICE (cada feature es una columna)

            Open Assign Close Get List                   Open  Assign Close  Get   List
 Api         ▒     ▒     ▒    ▒    ▒                     ┃     ┃      ┃     ┃     ┃
 Application ▒     ▒     ▒    ▒    ▒                     ┃     ┃      ┃     ┃     ┃
 Domain      ▒     ▒     ▒    ▒    ▒                     ┃     ┃      ┃     ┃     ┃
 Infra       ▒     ▒     ▒    ▒    ▒                     ┃     ┃      ┃     ┃     ┃
 Nueva feature = una pieza en cada fila                  Nueva feature = una columna nueva
```

## 3. Anatomía de una slice

En el sample, cada slice es una carpeta con un archivo que contiene sus cuatro piezas (el namespace es el nombre de la slice):

| Pieza | Qué es | Ejemplo (`OpenTicket`) |
|---|---|---|
| `Request` | Lo que entra | `record Request(string Title, string? Description)` |
| `Response` | Lo que sale (forma específica de esta slice) | `record Response(Guid Id, string Title, string Status)` |
| `Handler` | La lógica de la petición; usa `AppDbContext` directamente | `Handler(AppDbContext db, TimeProvider clock)` |
| `Endpoint` | El mapeo HTTP de esta slice | `Endpoint.Map(app)` |

```csharp
namespace Helpdesk.Api.Features.OpenTicket;

public sealed record Request(string Title, string? Description);
public sealed record Response(Guid Id, string Title, string Status);

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<Response> Handle(Request request, CancellationToken ct)
    {
        var ticket = Ticket.Open(request.Title, request.Description, clock.GetUtcNow());
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        return new Response(ticket.Id, ticket.Title, ticket.Status.ToString());
    }
}
```

**Cada slice elige su enfoque.** Bogard: cada slice decide su propio enfoque de dominio; empieza simple (transaction script) y refactoriza hacia patrones más ricos cuando aparezcan *code smells*.

| Slice | Tipo | Enfoque elegido |
|---|---|---|
| `OpenTicket`, `AssignTicket`, `CloseTicket` | Comando (escribe) | Carga la entidad `Ticket`, llama a su método, guarda |
| `GetTicket` | Query (lee) | Proyección directa a un DTO con `AsNoTracking`; sin entidad |
| `ListTickets` | Query (lee) | Filtro + paginación + validación propia de la petición |

## 4. Código compartido: poco y justificado

Sin repositorios ni servicios compartidos, ¿qué queda en `Common/`?

| Archivo | Por qué es compartido |
|---|---|
| `Ticket.cs` | Las **reglas del negocio** (título obligatorio, no cerrar dos veces…) viven en la entidad para que no se copien en cada slice que modifica un ticket |
| `AppDbContext.cs` | El acceso a datos; las slices lo usan directamente |
| `Errors.cs` | Un único traductor de errores a HTTP (422, 400, 404) |
| `FeatureRegistration.cs` | El **único** lugar que lista todas las slices (registro DI + mapeo de endpoints) |

Criterio práctico para decidir si algo es compartido: si las reglas de negocio de un concepto las usan varias slices que escriben, van a la entidad compartida; si es solo la forma de una respuesta o una validación de una petición, se queda **dentro de la slice**, aunque se parezca a otra. Una pequeña duplicación entre slices suele costar menos que un acoplamiento entre ellas.

## 5. Crear el proyecto desde cero

```bash
mkdir Helpdesk && cd Helpdesk
dotnet new sln -n Helpdesk

dotnet new web   -n Helpdesk.Api   -o src/Helpdesk.Api
dotnet new xunit -n Helpdesk.Tests -o tests/Helpdesk.Tests

dotnet add tests/Helpdesk.Tests reference src/Helpdesk.Api
dotnet add src/Helpdesk.Api package Microsoft.EntityFrameworkCore.InMemory
dotnet add tests/Helpdesk.Tests package Microsoft.AspNetCore.Mvc.Testing

dotnet sln add src/Helpdesk.Api/Helpdesk.Api.csproj tests/Helpdesk.Tests/Helpdesk.Tests.csproj
```

Un solo proyecto de producción: aquí no hay capas que separar en ensamblados. Las fronteras entre slices las marcan carpetas y namespaces, y se **verifican con tests** (sección 7).

## 6. Construir slice a slice

1. **`Common/`**: `Ticket` (con sus reglas y tests), `AppDbContext`, errores.
2. **Primera slice (`OpenTicket`)**: `Request`, `Response`, `Handler`, `Endpoint`. Su test llama al `Handler` con un `AppDbContext` real en memoria.
3. **`FeatureRegistration`**: una línea de DI y una de mapeo por slice.
4. Repite para cada funcionalidad. Cada slice nueva solo **añade** código.

Registro en `Program.cs`:

```csharp
builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("helpdesk"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddFeatures();                 // los Handler de cada slice
builder.Services.AddExceptionHandler<ErrorHandler>();
...
app.MapFeatures();                              // los Endpoint de cada slice
```

El mediator (por ejemplo MediatR) es **opcional**: aquí cada `Handler` se inyecta y se llama directamente desde su `Endpoint`. El patrón es la organización por petición, no la librería.

## 7. Testing

| Qué | Cómo | Archivo |
|---|---|---|
| Reglas de negocio | Unit test de la entidad compartida | `TicketTests.cs` |
| Cada slice | Llamar al `Handler` con un `AppDbContext` **real** en memoria (base nueva por test). Sin mocks de repositorios: no existen | `SliceTests.cs` |
| Extremo a extremo | `WebApplicationFactory`, una base distinta por test | `ApiTests.cs` |
| Aislamiento entre slices | Test que falla si una slice nombra a otra, o si `Common` depende de una slice (salvo el registro) | `SliceIsolationTests.cs` |

```bash
dotnet test     # 36 tests en el proyecto de referencia
```

Como todo vive en un solo proyecto, **el compilador no impide que una slice use a otra**. Por eso el test de aislamiento es la red de seguridad. Es una verificación de texto (busca nombres de slices ajenas en los archivos): sirve como guardia, pero no ve llamadas hechas por reflexión ni alias.

## 8. Verificación sobre el binario (con REA)

Para tener evidencia que no dependa de mirar el texto, se inspeccionó `Helpdesk.Api.dll` compilado con REA (análisis estático de metadatos y llamadas IL; no ejecuta ni descompila a C#). Se clasificó cada tipo por su namespace (incluyendo los tipos anidados que genera el compilador, como closures y máquinas de estado) y se revisaron todas las llamadas y accesos a campos:

| Observación | Resultado |
|---|---|
| Tipos por slice (incluye anidados) | OpenTicket 7 · AssignTicket 7 · CloseTicket 7 · GetTicket 8 · ListTickets 10 |
| Llamadas (call edges) analizadas | 937 en total |
| Llamadas **dentro de una misma slice** | 195 |
| Llamadas **de una slice a `Common`** | 27 |
| Llamadas **de una slice a otra slice** | **0** |
| Llamadas de `Common` hacia una slice, fuera de `FeatureRegistration` | **0** |
| Accesos a campos entre slices | **0** |
| `FeatureRegistration` alcanza | las 5 slices (comprobación de que el método detecta relaciones) |

Evidencia: `ev_a1ad6012…83c1`. El resto de llamadas (684) apunta a código externo (framework, EF Core) y no se clasifica por slice.

Limitación: es análisis estático; las llamadas por reflexión o resueltas en tiempo de ejecución no aparecen.

## 9. Receta: añadir una funcionalidad

Ejemplo: "reabrir un ticket".

1. Crear `Features/ReopenTicket/ReopenTicket.cs` con `Request`, `Response`, `Handler`, `Endpoint`.
2. Si hay una regla nueva ("solo se reabre un ticket cerrado"), añadir el método `Ticket.Reopen` + test en `TicketTests`.
3. Test de la slice llamando al `Handler`.
4. Añadir **dos líneas** en `FeatureRegistration` (DI y mapeo) y el nombre en la lista de `SliceIsolationTests`.
5. `dotnet test`. No tocaste ninguna otra slice.

## 10. Cuándo NO funciona bien

- **Equipo sin criterio para refactorizar.** Bogard lo dice sin rodeos: si el equipo no sabe cuándo la lógica debe pasar al dominio, *"this pattern is likely not for you"*. Las slices dependen de que reconozcan los *code smells* y refactoricen a tiempo.
- **Lógica de negocio compleja compartida por muchas funcionalidades.** Ahí una capa de dominio rica (Onion/Clean/DDD) gana; en el sample, `Ticket` es un primer paso en esa dirección.
- **Necesidad de fronteras garantizadas por el compilador.** En un solo proyecto no las hay; hace falta disciplina y tests (o separar en proyectos/módulos).
- **Duplicación entre slices:** es la crítica más citada. Se gestiona extrayendo a `Common` solo cuando varias slices comparten *reglas*, no *formas*.

## 11. Errores comunes

| Error | Por qué está mal | Correcto |
|---|---|---|
| Una slice llama a otra ("reutilizo `GetTicket` desde `CloseTicket`") | Recrea el acoplamiento que se quería evitar | Duplicar la consulta pequeña o extraer a `Common` si es una regla |
| Reintroducir `Services/` y `Repositories/` globales | Vuelves a organizar por capa técnica | Cada slice usa `AppDbContext` directamente |
| Meter reglas de negocio en el `Handler` y copiarlas entre slices | Se desincronizan | Reglas en la entidad compartida |
| `Common` crece sin control | Se convierte en una capa más | Entra solo lo que usan varias slices *por sus reglas* |
| Mismo `Response` para todo | Acopla slices por su forma | Una respuesta por slice |
| Aplicarla sin tests de aislamiento | Nada impide que se acoplen | Test de aislamiento + revisión |

## 12. Checklist de revisión

- [ ] Cada funcionalidad es una carpeta con su `Request`, `Response`, `Handler` y `Endpoint`.
- [ ] Ninguna slice referencia a otra.
- [ ] `Common` solo contiene lo que varias slices necesitan; y no depende de ninguna slice (salvo el registro).
- [ ] Las reglas de negocio de un concepto compartido están en su entidad, no repetidas en handlers.
- [ ] Queries y comandos usan el enfoque que les conviene (proyección directa vs. entidad).
- [ ] Cada slice tiene su test llamando al `Handler`.
- [ ] El test de aislamiento existe y pasa.
- [ ] Añadir una funcionalidad no obligó a modificar otras slices.
