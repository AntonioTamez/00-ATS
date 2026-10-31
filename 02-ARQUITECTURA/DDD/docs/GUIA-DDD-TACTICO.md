# Guía: DDD táctico paso a paso (.NET 10)

Con esta guía un dev junior puede modelar un dominio con los bloques tácticos de DDD y construir un proyecto desde cero. El resultado es el proyecto de referencia en [`../sample`](../sample) (reservas de un hotel). Compara siempre con él.

> Fuentes: [Domain-Driven Design Reference – Eric Evans](https://www.domainlanguage.com/wp-content/uploads/2016/05/DDD_Reference_2015-03.pdf) (las citas de Evans se extrajeron del texto del PDF) y [Use tactical DDD to design microservices – Microsoft Learn](https://learn.microsoft.com/en-us/azure/architecture/microservices/model/tactical-domain-driven-design) (las citas de Microsoft vienen de esa página).
> Para ubicar DDD frente a Clean, Hexagonal, Onion y Vertical Slice: [DDD-VS-ARQUITECTURAS.md](DDD-VS-ARQUITECTURAS.md).

---

## 1. Qué es (y qué no es)

**DDD no es una arquitectura de capas.** Es una forma de **modelar el dominio** del negocio en código. Tiene dos mitades:

| Mitad | Pregunta | Ejemplos |
|---|---|---|
| **Estratégico** | ¿Cómo divido el problema y hablo con el negocio? | Lenguaje ubicuo, bounded contexts |
| **Táctico** (esta guía) | ¿Cómo expreso el modelo dentro de un bounded context? | Entidades, value objects, agregados, eventos, servicios, repositorios |

Las arquitecturas (Clean, Hexagonal, Onion…) dicen **dónde viven las dependencias**; DDD táctico dice **cómo es el modelo que vive en el centro**. Son complementarias.

## 2. Qué problema resuelve

Evans lo describe así: cuando el código de dominio está mezclado con UI, base de datos y otras tareas, *"becomes extremely difficult to see and to reason about"*; cambiar una regla obliga a rastrear código de UI y de base de datos, y las pruebas automáticas se vuelven incómodas. Su remedio: *"Isolate the expression of the domain model and the business logic"*.

Microsoft añade el antipatrón que DDD combate: el **modelo de dominio anémico**: cuando la lógica de negocio vive fuera de la entidad, en clases de servicio, *"you create an anemic domain model"* y se pierde el beneficio de DDD. Las validaciones, transiciones de estado y reglas van **dentro de la entidad**.

## 3. Lenguaje ubicuo

Los nombres del código son los nombres del negocio, y se usan igual en reuniones, documentación y tests. En el sample:

| Término del negocio | En el código |
|---|---|
| Reserva | `Reservation` |
| Estancia (check-in … check-out) | `DateRange` (llamado `Stay` en la reserva) |
| Huésped | `GuestInfo` |
| Tarifa por noche | `Room.NightlyRate` |
| Cargo extra (minibar, spa) | `Charge` |
| Confirmar / cancelar / hacer check-in / check-out | `Book`, `Cancel`, `CheckIn`, `CheckOut` |
| "Se confirmó una reserva" | evento `ReservationConfirmed` |

Si el experto del negocio dice "cancelar", el método se llama `Cancel`, no `SetStatus(4)`.

## 4. Los bloques tácticos, uno por uno

### 4.1 Entidad (Entity)
Se define por su **identidad**, no por sus atributos. Evans: *"The model must define what it means to be the same thing."* Dos reservas con los mismos datos son reservas distintas.
- En el sample: `Entity<TId>` iguala por tipo + `Id`. `Reservation`, `Room` y `Charge` son entidades.
- Las entidades **encapsulan comportamiento**, no solo datos (`Reservation.Cancel(...)`).
- Identidad **fuertemente tipada**: `ReservationId` y `RoomId`; el compilador impide pasar un id por otro.

### 4.2 Value Object (objeto de valor)
Sin identidad: se define por sus valores. Evans: *"Treat the value object as immutable. Make all operations side-effect-free functions."* Microsoft: **es la opción por defecto**; promociona a entidad solo si necesitas seguir su identidad en el tiempo.
- `Money`, `DateRange`, `GuestInfo`: inmutables, **válidos desde la construcción** (no existe un `Money` negativo ni un `DateRange` invertido).
- Las operaciones devuelven valores nuevos: `price - price.Percent(10)` no modifica `price`.
- Contienen lógica de dominio útil: `DateRange.Overlaps`, `Money.Percent`.

### 4.3 Agregado (Aggregate)
Un grupo de entidades y value objects tratado como **una unidad de consistencia**, con una **raíz** (aggregate root). Evans: *"allow external objects to hold references to the root only"*, y hay que *"give enforcement responsibility to the root"* de las propiedades e invariantes del conjunto.

```
 Agregado Reservation                          Agregado Room
 ┌──────────────────────────────┐              ┌──────────────────┐
 │ Reservation  (raíz)          │   RoomId     │ Room  (raíz)     │
 │  ├─ GuestInfo   (VO)         │ ───────────► │  ├─ Number       │
 │  ├─ DateRange   (VO)         │  (solo el id)│  └─ NightlyRate  │
 │  ├─ Money RoomCharge (VO)    │              └──────────────────┘
 │  └─ Charge[]    (entidades)  │
 └──────────────────────────────┘
```

Reglas de diseño (Microsoft, coherentes con Evans):
1. **Agregados pequeños**: incluye solo lo que debe ser consistente en una transacción. `Room` no contiene sus reservas: tienen ciclo de vida propio y juntarlas haría competir actualizaciones no relacionadas por el mismo bloqueo.
2. **Referencia a otros agregados solo por identidad**: la reserva guarda `RoomId`, no un `Room`.
3. **Consistencia eventual entre agregados**: Evans: *"Within an aggregate boundary, apply consistency rules synchronously. Across boundaries, handle updates asynchronously."* Por eso, cuando una reserva se confirma, el aviso al huésped sucede como reacción a un evento, no dentro de la misma transacción.
4. **Una transacción modifica un agregado.**

Cómo lo protege el código: constructor privado, setters privados, la lista de cargos expuesta como `IReadOnlyList`, y toda modificación pasa por un método que valida:

```csharp
public void AddCharge(string description, Money amount)
{
    if (Status is not (ReservationStatus.Confirmed or ReservationStatus.CheckedIn))
        throw new DomainException("Charges can only be added to an active reservation.");
    ...
    _charges.Add(new Charge(description.Trim(), amount));   // Charge: constructor internal
}
```

### 4.4 Evento de dominio (Domain Event)
Evans: *"Something happened that domain experts care about."* Son **inmutables**, nombrados en **pasado**, y llevan lo necesario (hora e identidad de lo implicado).
- `ReservationConfirmed`, `ReservationCancelled`, `GuestCheckedOut`.
- *"A record was inserted"* no es un evento de dominio; *"una reserva fue cancelada"* sí (el ejemplo es de Microsoft).
- La raíz **registra** los eventos (`Raise`) y la capa de aplicación los **entrega** después de guardar (`PullEvents` + dispatcher). Cada evento se entrega una vez.

### 4.5 Servicio de dominio (Domain Service)
Para una regla que **no es responsabilidad natural de una entidad ni de un value object**. Evans: *"Sometimes, it just isn't a thing."*
- `BookingService`: ¿está libre la habitación? involucra a `Room` y a todas las demás reservas; no cabe en ninguna de ellas. También calcula el precio (descuento del 10 % desde 7 noches).
- Sin estado, con nombre del lenguaje ubicuo, y **sin** orquestar casos de uso ni tocar infraestructura.

### 4.6 Servicio de aplicación (Application Service)
Orquesta un caso de uso y **no contiene reglas de negocio** (Microsoft). Patrón del sample: *cargar un agregado → llamar a su comportamiento → guardar → entregar sus eventos*.

```csharp
var reservation = await reservations.LoadAsync(id, ct);
reservation.Cancel(clock.Today, clock.UtcNow);       // la regla vive en el agregado
await reservations.UpdateAsync(reservation, ct);
await events.DispatchAsync(reservation.PullEvents(), ct);
```

### 4.7 Repositorio (Repository)
Da la ilusión de una colección en memoria de **agregados completos**. Uno por **raíz**, y el sample lo hace cumplir al compilador:

```csharp
public interface IRepository<TRoot, TId> where TRoot : AggregateRoot<TId> where TId : notnull { ... }
```

No puedes declarar un repositorio de `Charge`. La interfaz la define el dominio; la implementa la infraestructura.

### 4.8 Fábrica (Factory)
Encapsula la creación para que el objeto **nazca válido**: `Reservation.Book(...)`, `Room.Create(...)`. El constructor es privado.

### 4.9 Capas (Layered Architecture)
Evans propone aislar el dominio: *"eliminate any dependency on infrastructure, user interface, or even application logic that is not business logic"*, y añade que patrones como Hexagonal *"may serve as well or better"* mientras logren ese aislamiento. El sample usa cuatro proyectos: `Domain`, `Application`, `Infrastructure`, `Api`.

## 5. ¿Entidad o value object? ¿Dónde va esta regla?

**¿Entidad o value object?** ¿Necesito distinguir dos instancias idénticas en datos y seguirlas en el tiempo? Sí → entidad. No → value object.

**¿Dónde va una regla?**
1. ¿Afecta a **un solo objeto** o a sus datos? → dentro de ese value object o entidad (`Money` no admite negativos).
2. ¿Protege una **invariante del agregado entero**? → en la **raíz** (`Reservation.AddCharge` valida el estado y la moneda).
3. ¿Involucra **varios agregados**? → **servicio de dominio** (`BookingService`).
4. ¿Es **orquestar** (cargar, guardar, notificar)? → **servicio de aplicación**.
5. ¿Es un detalle técnico? → **infraestructura / API**.

## 6. Estructura del proyecto

| Proyecto | Contiene | Referencia a |
|---|---|---|
| `Hotel.Domain` | `Common/` (bloques base y value objects), `Rooms/`, `Reservations/` (agregado, evento, servicio de dominio), `Repositories.cs` | nadie |
| `Hotel.Application` | Servicios de aplicación, dispatcher de eventos, handlers, puertos (`IClock`, `INotifier`), DTOs | Domain |
| `Hotel.Infrastructure` | Repositorios en memoria, `SystemClock`, `LogNotifier` | Application, Domain |
| `Hotel.Api` | Endpoints, traducción de errores a HTTP, composition root | Application, Infrastructure, Domain |

## 7. Crear el proyecto desde cero

```bash
mkdir Hotel && cd Hotel
dotnet new sln -n Hotel

dotnet new classlib -n Hotel.Domain         -o src/Hotel.Domain
dotnet new classlib -n Hotel.Application    -o src/Hotel.Application
dotnet new classlib -n Hotel.Infrastructure -o src/Hotel.Infrastructure
dotnet new web      -n Hotel.Api            -o src/Hotel.Api
dotnet new xunit    -n Hotel.Tests          -o tests/Hotel.Tests

dotnet add src/Hotel.Application    reference src/Hotel.Domain
dotnet add src/Hotel.Infrastructure reference src/Hotel.Application src/Hotel.Domain
dotnet add src/Hotel.Api            reference src/Hotel.Application src/Hotel.Infrastructure src/Hotel.Domain
dotnet add tests/Hotel.Tests        reference src/Hotel.Domain src/Hotel.Application src/Hotel.Infrastructure src/Hotel.Api

dotnet add src/Hotel.Application    package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/Hotel.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/Hotel.Infrastructure package Microsoft.Extensions.Logging.Abstractions
dotnet add tests/Hotel.Tests        package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/Hotel.Tests        package Microsoft.Extensions.DependencyInjection

dotnet sln add src/*/*.csproj tests/*/*.csproj
```

## 8. Construir de adentro hacia afuera

1. **Habla con el negocio** y escribe el glosario (sección 3). Aquí nace el modelo.
2. **Value objects** (`Money`, `DateRange`, `GuestInfo`) con sus tests: son los más baratos de probar y de acertar.
3. **Bloques base**: `Entity<TId>`, `AggregateRoot<TId>`, `IDomainEvent`, `DomainException`.
4. **Agregados** (`Room`, `Reservation` con `Charge`): comportamiento, invariantes y eventos, con tests de cada regla.
5. **Servicio de dominio** (`BookingService`) + interfaces de repositorio.
6. **Aplicación**: un servicio por caso de uso, el dispatcher de eventos y los handlers.
7. **Infraestructura**: repositorios, reloj, notificador.
8. **Api**: endpoints delgados y traducción de errores.

## 9. Testing

| Qué | Cómo | Archivo |
|---|---|---|
| Value objects | Unit puro: igualdad, validación, inmutabilidad | `ValueObjectTests.cs` |
| Agregados | Unit puro: cada regla, cada transición y cada evento | `AggregateTests.cs` |
| Servicio de dominio | Con un repositorio falso: disponibilidad y precio | `BookingServiceTests.cs` |
| Servicios de aplicación | Fakes + **dispatcher real**: prueba que el evento llega al handler | `UseCaseTests.cs` |
| Reglas DDD | Reflection sobre el ensamblado del dominio | `DddRulesTests.cs` |
| Extremo a extremo | `WebApplicationFactory` | `ApiTests.cs` |

`DddRulesTests` convierte el diseño en tests ejecutables:
- las entidades **no tienen constructores públicos** ni **setters públicos**;
- los agregados referencian a otros **solo por identidad**;
- los value objects son **inmutables** y estructuras o sellados;
- los eventos son inmutables;
- todo repositorio pasa por el contrato genérico por **raíz**;
- el dominio no referencia ningún otro proyecto ni ASP.NET/EF.

Comprobación de que esos tests muerden: se hizo público a propósito el setter de `Room.Number` y `Entities_HaveNoPublicSetters_…` falló; se revirtió el cambio.

```bash
dotnet test     # 69 tests en el proyecto de referencia
```

## 10. Verificación sobre el binario (con REA)

Se inspeccionaron los ensamblados **compilados** con REA (análisis estático de metadatos; no ejecuta ni decompila a C#):

| Observación | Evidencia |
|---|---|
| `Hotel.Domain.dll` solo referencia `System.Runtime`, `System.Collections`, `System.Linq` (ningún otro `Hotel.*`, ni ASP.NET, ni EF) | `ev_dccb231e4…000d` |
| `Hotel.Application.dll` referencia `Hotel.Domain` (y DI Abstractions) | `ev_c0d4a7750…8d70` |
| `Hotel.Infrastructure.dll` referencia `Hotel.Domain` y `Hotel.Application`; **no** `Hotel.Api` | `ev_71096cc6…ba88` |
| Constructores: `Room` y `Reservation` **private**; `Charge` **internal** | `ev_41ecfd33b…ce48` |
| Setters: todos los de `Room`, `Reservation` y `Charge` **private**; el de `Id` **protected** | `ev_41ecfd33b…ce48` |
| Las únicas fábricas públicas son `Room.Create` y `Reservation.Book` | `ev_41ecfd33b…ce48` |
| `RoomId.set_Value` y `ReservationId.set_Value` son públicos pero su firma lleva `modreq` (la marca de *init-only*) | `ev_41ecfd33b…ce48` |

**Un caso real de por qué sirve mirar el binario:** el primer análisis de `Hotel.Domain.dll` (`ev_b17d7dcd5…a4a8`) mostró `Room.set_Number` **público**. Era el binario que había quedado compilado de la prueba de mutación anterior: el código fuente ya estaba restaurado, pero el `.dll` no se había recompilado. Tras recompilar, el segundo análisis mostró el setter **privado**. Moraleja: el código fuente y el artefacto compilado pueden divergir; verifica lo que realmente se distribuye.

Limitación: es análisis estático de metadatos. Que el `modreq` sea la marca de *init-only* se confirma además con la regla de reflection del test; REA no decompila para demostrarlo.

## 11. Receta: añadir una regla nueva

Ejemplo: *"El check-out tardío tiene un recargo de 20 €"*.

1. **Lenguaje:** acordar el término con el negocio (¿recargo? ¿"late check-out fee"?).
2. **Agregado:** si es una regla de la reserva, un método en `Reservation` (por ejemplo `CheckOut(now, lateFee)`) que añade el `Charge` y mantiene la invariante de moneda. Tests de cada caso.
3. **Evento:** si el negocio quiere reaccionar ("avisar al huésped"), un evento `LateCheckOutFeeApplied` y su handler.
4. **Aplicación:** modificar el servicio de aplicación solo si cambia la orquestación (¿de dónde sale la hora límite?).
5. **API:** exponer el dato nuevo en el DTO.
6. `dotnet test`, incluidos los tests de reglas DDD.

## 12. Errores comunes

| Error | Por qué está mal | Correcto |
|---|---|---|
| Modelo anémico: entidades con getters/setters y la lógica en servicios | Pierdes el beneficio de DDD | Reglas dentro de la entidad / raíz |
| Setters públicos y constructores públicos | Cualquiera puede dejar el objeto inválido | Setters privados, fábrica, métodos de comportamiento |
| Agregado gigante (`Room` con todas sus reservas) | Contención de bloqueos y modelo difícil | Agregados pequeños; relación por identidad |
| `Reservation` con una propiedad `Room` | Rompe la frontera del agregado | `RoomId` |
| Value object mutable | Pierde igualdad por valor y seguridad | Inmutable, operaciones devuelven valores nuevos |
| Un repositorio por tabla o por entidad hija | Se salta la raíz | Un repositorio por **raíz** |
| Servicio de aplicación con `if` de negocio | La regla se duplica | Moverla al agregado o al servicio de dominio |
| Eventos con nombre técnico (`RowInserted`) | No son del negocio | Pasado y lenguaje del dominio |
| Modificar dos agregados en una transacción | Rompe el límite de consistencia | Eventos y consistencia eventual |

## 13. Límites y trade-offs honestos

- **Concurrencia en la reserva.** `BookingService` comprueba la disponibilidad y luego se guarda: no es atómico. Dos reservas simultáneas del mismo hueco podrían pasar ambas. Con una base de datos real hay que respaldarlo con una restricción o control de concurrencia optimista. Es una consecuencia de la consistencia eventual entre agregados, no un descuido.
- **Persistencia.** El sample usa repositorios en memoria que guardan los mismos objetos. Con EF Core hay que configurar constructores privados, campos de respaldo y conversores para los value objects.
- **Costo.** DDD táctico añade vocabulario, clases y disciplina. Se paga cuando el dominio es complejo y duradero, no en un CRUD.
- **Estratégico primero.** Esta guía cubre lo táctico dentro de un solo bounded context; definir bien los contextos y el lenguaje con el negocio es la otra mitad (y la que más impacto tiene).

## 14. Checklist de revisión

- [ ] Los nombres del código coinciden con el lenguaje del negocio.
- [ ] Cada concepto sin identidad es un value object inmutable y válido desde su creación.
- [ ] Las entidades no tienen constructores ni setters públicos.
- [ ] Cada agregado tiene una sola raíz y una frontera de consistencia pequeña.
- [ ] Los agregados se referencian por identidad.
- [ ] Una transacción cambia un solo agregado; el resto reacciona con eventos.
- [ ] Las reglas viven en el dominio (entidad, raíz o servicio de dominio), no en los servicios de aplicación.
- [ ] Hay un repositorio por raíz, con la interfaz en el dominio.
- [ ] Los eventos son inmutables y están en pasado.
- [ ] Los tests de reglas DDD existen y pasan.
- [ ] El dominio no depende de infraestructura ni de frameworks.
