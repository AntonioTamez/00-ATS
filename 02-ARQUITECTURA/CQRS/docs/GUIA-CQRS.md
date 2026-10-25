# Guía: CQRS paso a paso

Para un dev junior que quiere construir un proyecto básico que **separa lo que cambia datos de lo que los lee**. El sample (cuentas bancarias) tiene 56 tests, incluidas reglas de arquitectura ejecutables, y sus binarios están analizados con REA.

> Fuentes: [Martin Fowler – CQRS](https://martinfowler.com/bliki/CQRS.html) y [Microsoft – CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs). Las citas están copiadas de esas páginas. La estructura de proyectos, los nombres y las reglas de este sample son **decisiones de este kit**, no un estándar.

---

## 1. Qué es y qué problema resuelve

CQRS significa **Command Query Responsibility Segregation**: separar las operaciones que **cambian** datos (*commands*) de las que los **leen** (*queries*).

El problema, según Microsoft: en una arquitectura CRUD clásica se usa un solo modelo para leer y escribir, y al crecer *"read and write operations often have different performance and scaling requirements"*. Aparecen desajustes (*"The read and write representations of data often differ"*), contención de bloqueos y consultas complejas. Combinar ambas responsabilidades puede producir *"an overly complicated model"*.

Idea central:

| | Command (escritura) | Query (lectura) |
|---|---|---|
| Qué hace | Cambia el estado | Devuelve datos |
| Modelo | Rico: reglas e invariantes (aquí, `Account`) | Plano: DTOs hechos para la pantalla |
| Devuelve | Casi nada (un id, o nada) | Datos, nunca cambia nada |
| Se nombra | Con intención de negocio: `DepositCommand` | Con la pregunta: `GetAccountQuery` |

Microsoft lo dice así: *"Commands should represent specific business tasks instead of low-level data updates"* y *"Queries never alter data"*.

## 2. Antes de seguir: ¿lo necesitas?

Fowler es tajante: *"Like any pattern, CQRS is useful in some places, but not in others"*, *"Many systems do fit a CRUD mental model, and so should be done in that style"*, *"CQRS is a significant mental leap for all concerned, so shouldn't be tackled unless the benefit is worth the jump"* y *"you should be very cautious about using CQRS"*.

Microsoft indica que puede **no** ser adecuado cuando *"the domain or the business rules are simple"* o cuando *"a simple CRUD-style user interface and data access operations are sufficient"*.

Si tu app es un CRUD, **no uses CQRS**. Este kit existe para que lo entiendas, no para que lo apliques a todo.

## 3. Los tres niveles de CQRS

| Nivel | Qué es | Coste |
|---|---|---|
| **1. Comandos y consultas separados, mismo almacén** | Dos caminos de código (handlers distintos, modelos distintos) sobre la misma base de datos | Bajo. Es el punto de partida razonable |
| **2. Almacenes separados** | La escritura va a una base y la lectura a otra (o a una vista materializada) alimentada por eventos | Medio-alto: **consistencia eventual** |
| **3. Con Event Sourcing** | El estado de escritura *son* los eventos | Alto. Es otra técnica, no obligatoria |

**Este sample es nivel 2, en memoria:** el estado de escritura (`Account`) y el de lectura (`ReadModelStore`) son almacenes distintos, unidos por un log de eventos y un *projector*. **No es Event Sourcing:** el estado se guarda tal cual; los eventos son un registro que alimenta la lectura. Microsoft lo describe así: *"A common pattern is to have the write model publish events when it updates the database, which the read model uses to refresh its data"*.

## 4. Estructura del sample

```
Bank.Domain          ← MODELO DE ESCRITURA: Account (reglas), eventos, IAccountRepository
Bank.Commands        ← lado escritura: ICommand, handlers. Referencia SOLO a Bank.Domain
Bank.Queries         ← lado lectura: IQuery, handlers, DTOs, IAccountReadStore. NO referencia a nadie
Bank.Infrastructure  ← donde se encuentran: repositorio de escritura, log de eventos, almacén de lectura, projector
Bank.Api             ← composition root: endpoints POST (commands) y GET (queries)
```

Regla de oro: **Queries no conoce a Domain ni a Commands.** Por eso es imposible que una consulta aplique una regla de negocio o modifique una cuenta.

```
POST /accounts/{id}/deposit
  → DepositHandler → Account.Deposit() → repositorio guarda + añade MoneyDeposited al log
                                             │
                          (más tarde)        ▼
                                      Projector aplica el evento
                                             │
GET /accounts/{id} → GetAccountHandler → IAccountReadStore ← fila ya calculada
```

## 5. Quién puede referenciar a quién

| Proyecto | Puede referenciar | NO puede referenciar |
|---|---|---|
| `Bank.Domain` | Nada | Todos los demás |
| `Bank.Commands` | `Bank.Domain` | `Bank.Queries`, Infrastructure, Api |
| `Bank.Queries` | Nada del proyecto | Domain, Commands, Infrastructure, Api |
| `Bank.Infrastructure` | Domain, Queries | Api, Commands |
| `Bank.Api` | Todos (composition root) | — |

## 6. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

```bash
mkdir MiBanco && cd MiBanco
dotnet new sln -n MiBanco
dotnet new classlib -n Bank.Domain         -o src/Bank.Domain
dotnet new classlib -n Bank.Commands       -o src/Bank.Commands
dotnet new classlib -n Bank.Queries        -o src/Bank.Queries
dotnet new classlib -n Bank.Infrastructure -o src/Bank.Infrastructure
dotnet new web      -n Bank.Api            -o src/Bank.Api
dotnet new xunit    -n Bank.Tests          -o tests/Bank.Tests

dotnet add src/Bank.Commands       reference src/Bank.Domain
dotnet add src/Bank.Infrastructure reference src/Bank.Domain src/Bank.Queries
dotnet add src/Bank.Api            reference src/Bank.Commands src/Bank.Queries src/Bank.Infrastructure src/Bank.Domain
```

**Orden de construcción:**

1. **Domain** (el modelo de escritura): `Account` con constructor privado, fábrica `Open`, y métodos `Deposit`/`Withdraw`/`Close` que protegen las reglas y **levantan eventos**. Test unitario de cada regla.
2. **Commands**: `ICommand<TResult>`, `ICommandHandler<,>`, un `record` por intención y un handler por comando (cargar → llamar al método de dominio → guardar).
3. **Repositorio de escritura** que guarda el estado **y los eventos a la vez** (en la vida real: la misma transacción, una tabla *outbox*).
4. **Queries**: DTOs planos, `IAccountReadStore` (solo preguntas), un `record` y un handler por consulta.
5. **Projector**: lee el log desde donde se quedó y actualiza el almacén de lectura. Debe ser **idempotente** (no aplicar un evento dos veces).
6. **Api**: `POST` para comandos (devuelven `201/204`) y `GET` para consultas.
7. **Tests de arquitectura** (sección 9).

## 7. Las piezas clave

### Un comando = una intención
```csharp
public sealed record DepositCommand(Guid AccountId, decimal Amount) : ICommand<Unit>;

public sealed class DepositHandler(IAccountRepository accounts, TimeProvider clock)
    : ICommandHandler<DepositCommand, Unit>
{
    public async Task<Unit> HandleAsync(DepositCommand c, CancellationToken ct = default)
    {
        var account = await accounts.LoadAsync(c.AccountId, ct);   // 404 si no existe
        account.Deposit(c.Amount, clock.GetUtcNow());              // aquí viven las reglas
        await accounts.SaveAsync(account, ct);                     // estado + eventos
        return Unit.Value;                                         // no devuelve datos para mostrar
    }
}
```

### Una consulta = una pregunta
```csharp
public sealed record GetAccountQuery(Guid AccountId) : IQuery<AccountSummary?>;

public sealed class GetAccountHandler(IAccountReadStore store) : IQueryHandler<GetAccountQuery, AccountSummary?>
{
    public Task<AccountSummary?> HandleAsync(GetAccountQuery q, CancellationToken ct = default) =>
        store.GetSummaryAsync(q.AccountId, ct);                    // sin reglas, sin dominio
}
```

### El projector (la costura entre los dos lados)
Lee los eventos pendientes, los aplica al almacén de lectura y avanza su *checkpoint*. Como retoma desde la última secuencia aplicada, ejecutarlo dos veces no duplica nada. Además, `RebuildAsync()` vacía el almacén de lectura y lo reconstruye **desde el log completo**: la lectura es desechable.

## 8. Consistencia eventual: lo que más confunde

Entre el comando y la siguiente ejecución del projector, **la lectura está desactualizada**. En el sample:

- `POST /accounts` devuelve `201` con `Location: /accounts/{id}`, pero un `GET` inmediato puede dar `404` (hay un test que lo demuestra).
- Un `ProjectionWorker` en segundo plano ejecuta el projector cada 100 ms, así que en la práctica la lectura se pone al día enseguida (otro test lo comprueba).

Microsoft lo advierte: *"the read data might not show the most recent changes immediately. This delay results in stale data"* y hay que pensar cómo se maneja *"a user acts on stale data"*.

Qué hacer en una app real: mostrar al usuario el resultado del comando desde la respuesta (no volver a consultar), usar *polling* breve, o aceptar el retraso donde el negocio lo permita.

## 9. Tests

| Qué | Dónde | Idea |
|---|---|---|
| Reglas del dominio | `AccountTests` | Sin infraestructura. Una regla = un test, y cada rechazo no cambia nada ni emite eventos |
| Lado de comandos | `CommandSideTests` | Cada comando deja exactamente sus eventos en el log; un rechazo no deja nada |
| Lado de lectura | `ReadSideTests` | Staleness hasta que corre el projector, idempotencia, retomar, **rebuild**, paginación, filtros |
| Reglas de arquitectura | `ArchitectureTests` | Ver abajo |
| Extremo a extremo | `ApiTests` | HTTP real; el worker de fondo se quita para controlar cuándo se proyecta |

`ArchitectureTests` verifica por reflexión: Queries no referencia a ningún proyecto `Bank.*`; Commands solo a Domain; todo comando es un `record` sellado llamado `*Command` en el lado de comandos (y lo mismo con las consultas); **cada** mensaje tiene **un** handler; los comandos devuelven solo `Guid` o `Unit`; `IAccountReadStore` solo tiene métodos `Get*`/`List*`; los handlers de consulta dependen únicamente del almacén de lectura; los DTO son inmutables; el almacén de lectura mutable ni siquiera es visible fuera de Infrastructure.

### Pruebas de mutación (que las reglas muerden)
Probadas en una copia de trabajo para no tocar el código real:

1. Hice que `Bank.Queries` referenciara `Bank.Domain` y lo usara → falla `ReadSide_KnowsNothingOfTheWriteSide` (55/56).
2. Añadí a `IAccountReadStore` un método de escritura `SetBalanceAsync`. Con un método abstracto **el compilador ya lo impide** (el almacén no lo implementa). Con una implementación por defecto compila, y entonces falla `ReadStore_OnlyOffersQuestions` (55/56).

## 10. Verificación del binario con REA

Analicé los `.dll` compilados con REA (metadatos estáticos; no ejecuta nada).

| Hecho | Evidencia |
|---|---|
| `Bank.Queries.dll` referencia solo ensamblados del framework (`System.Runtime`, `System.Collections`, `System.Linq`, `Microsoft.Extensions.DependencyInjection.Abstractions`): **ningún `Bank.*`** | `inspect_managed_artifact`, `ev_1a8e81f4…6c37` |
| `Bank.Commands.dll` referencia `Bank.Domain` y ningún otro proyecto `Bank.*` | `inspect_managed_artifact`, `ev_6aa44179…e0f9` |

REA **no** prueba el comportamiento en ejecución (para eso están los 56 tests).

## 11. Receta: añadir "congelar cuenta" y "cuentas congeladas"

**Un comando nuevo (`FreezeAccountCommand`):**
1. Regla en `Account.Freeze()` + evento `AccountFrozen` + test del dominio.
2. `FreezeAccountCommand` + `FreezeAccountHandler`. El test de arquitectura te dirá si olvidaste el handler.
3. El projector aprende el evento nuevo (un `case` en `Apply`).
4. Endpoint `POST /accounts/{id}/freeze`.

**Una consulta nueva ("cuentas congeladas"):**
1. Método `ListFrozenAsync` en `IAccountReadStore` (empieza por `List`: el test lo exige).
2. `ListFrozenAccountsQuery` + handler.
3. Endpoint `GET`.
**No tocas el dominio.** Esa es la ventaja: leer se amplía sin riesgo para las reglas.

## 12. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| Un comando que devuelve el objeto completo | Mezclas escritura y lectura otra vez | Devuelve un id; el cliente consulta |
| Consultas que pasan por el agregado | Cargas lógica de dominio para mostrar datos | Lee de un modelo plano |
| Poner reglas en el handler de comando | El dominio queda anémico | El handler orquesta, la regla vive en `Account` |
| Olvidar la consistencia eventual en la UI | El usuario no ve lo que acaba de hacer | Diseña la pantalla para eso |
| Proyectar sin idempotencia | Un reintento duplica datos | Checkpoint / ids de evento procesados |
| Usar CQRS en un CRUD | Doble de código sin beneficio | No lo uses |
| Creer que CQRS exige Event Sourcing o un broker | Complejidad innecesaria | Son técnicas independientes |
| Un handler de consulta que "arregla" datos | Rompe la regla "las consultas no cambian nada" | Mueve eso a un comando |

## 13. Límites de este sample (honestidad)

- **Todo en memoria y en un proceso.** No hay base de datos real, ni broker, ni *outbox* real (el repositorio guarda estado y log bajo un mismo `lock` como sustituto de una transacción).
- **El projector es un solo proceso**: con varias instancias haría falta coordinar quién proyecta (o particionar).
- **El repositorio en memoria devuelve la misma instancia** de `Account`: no hay aislamiento entre lecturas y no hay concurrencia optimista (dos comandos simultáneos sobre la misma cuenta no se detectarían como conflicto). En una base real hay que añadir versión/ETag.
- **No es Event Sourcing**: el log sirve para reconstruir la lectura, no el estado de escritura.
- **Sin mediador ni pipeline** (validación, transacciones, logging): los endpoints inyectan el handler directamente. Es deliberado para verlo claro; una librería o un decorador pueden añadirse después.
- **Un solo modelo de lectura**; en la vida real puede haber varios (uno por pantalla o por consumidor).

## 14. Checklist

- [ ] ¿Realmente necesito CQRS, o me basta un CRUD?
- [ ] Los comandos usan lenguaje de negocio y devuelven a lo sumo un id
- [ ] Las consultas devuelven DTOs planos y no cambian nada
- [ ] El lado de lectura no referencia dominio ni comandos
- [ ] Sé qué pasa cuando la lectura está desactualizada y la UI lo contempla
- [ ] El projector es idempotente y puedo reconstruir la lectura
- [ ] Hay tests de arquitectura y probé al menos una mutación
- [ ] Sé qué parte del sistema es "de verdad" (escritura) y cuál desechable (lectura)
