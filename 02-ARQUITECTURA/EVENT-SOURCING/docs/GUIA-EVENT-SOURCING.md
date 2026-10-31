# Guía: Event Sourcing paso a paso

Para un dev junior que quiere entender qué cambia cuando **lo que se guarda no es el estado, sino los hechos que lo produjeron**, y construir un proyecto básico. El sample (libro de movimientos de stock de un almacén) tiene 91 tests, incluidos conflictos de concurrencia, snapshots, viaje en el tiempo y evolución de eventos; sus binarios están analizados con REA.

> Fuentes: [Microsoft – Event Sourcing pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing) y [Martin Fowler – Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html). Las citas están copiadas de esas páginas. Los nombres, el formato de los eventos, la política de snapshots y las reglas de los tests son **decisiones de este kit**, no un estándar.

---

## 1. Qué es

Fowler: *"Event Sourcing ensures that all changes to application state are stored as a sequence of events."* Microsoft: *"Instead of storing only the current state of the data in a relational database, store the full series of actions taken on an object in an append-only store. The store acts as the system of record"*.

| | CRUD (estado) | Event Sourcing |
|---|---|---|
| Qué se guarda | La fila con el valor actual | La secuencia de hechos |
| "Stock = 7" | Sobrescribe 10 con 7 | Guarda "se recibieron 10", "se enviaron 3" |
| Historial | Se pierde (salvo que añadas una auditoría aparte) | **Es** el dato |
| Estado actual | Se lee | Se **reconstruye** repitiendo los eventos |
| Pregunta "¿cuánto había el martes?" | No se puede | Se repiten los eventos hasta el martes |
| Corregir un error | `UPDATE` | Un **nuevo** evento que compensa |

Fowler: *"We can determine the application state at any point in time."* Microsoft nombra el proceso de reconstruir: *rehydration*.

## 2. Antes de seguir: ¿lo necesitas?

Probablemente no en todo el sistema. Microsoft abre con una advertencia: *"Event sourcing is a complex pattern that introduces significant trade-offs... It's costly to migrate to or from an event sourcing solution... For most systems and most parts of a system, traditional data management is sufficient."*

Cuándo sí: capturar la **intención** del cambio, tener auditoría o historial, *"minimize or completely avoid conflicting updates"*, o reconstruir/reproducir estados. Cuándo no: *"straightforward CRUD operations that don't require auditability, replay, or historical reconstruction"*, prototipos o sistemas de vida corta, datos de referencia casi estáticos, o equipos sin experiencia con arquitecturas dirigidas por eventos. Y una pista práctica: *"Apply it selectively to the parts of your system that it benefits the most, such as a payment ledger"*.

## 3. Event Sourcing NO es CQRS, NI Event-Driven

| | Pregunta que responde | En el catálogo |
|---|---|---|
| **Event Sourcing** | ¿Cómo guardo el estado? (como eventos) | Este kit |
| **CQRS** | ¿Separo el modelo de escribir del de leer? | [`CQRS/`](../../CQRS) |
| **Event-Driven** | ¿Cómo se avisan las partes entre sí? (mensajes) | [`EVENT-DRIVEN/`](../../EVENT-DRIVEN) |

Se combinan a menudo, pero son independientes. Un detalle importante: los eventos **de este sample son eventos de persistencia** (hechos del dominio guardados en el almacén). No se publican a un broker. Microsoft lo advierte: *"event sourcing events are typically low level, and it might be necessary to generate specific integration events instead."*

## 4. Estructura del sample

```
Ledger.Domain          ← StockItem (el agregado), los eventos, las reglas. No sabe cómo se guardan
Ledger.Infrastructure  ← almacén de eventos, serializador (con upcasters), repositorio, snapshots, servicio, proyección
Ledger.Api             ← endpoints: comandos, consultas, historial, "máquina del tiempo"
Ledger.Tests
```

```
POST /items/A/ship {quantity: 4}
  1. Cargar:   leer los eventos del stream "stock-A" (o snapshot + los posteriores) → StockItem
  2. Decidir:  item.Ship(4, ...)  → ¿hay stock? si no, DomainException y NO se escribe nada
  3. Guardar:  añadir el evento StockShipped al stream, esperando que siga en la versión que leímos
```

## 5. Reglas de diseño

| Regla | Por qué | Cómo se verifica |
|---|---|---|
| Los eventos se nombran en **pasado** | Son hechos ya ocurridos | Test: nombre termina en "ed" |
| Los eventos son **inmutables** (records sellados con `init`) | No se editan | Test |
| El almacén solo ofrece **Append y Read** | No hay `Update` ni `Delete` | Test sobre `IEventStore` |
| Todo evento está **registrado** en el serializador | Si no, no se podría guardar ni leer | Test |
| El agregado no tiene setters ni constructor públicos | El estado solo cambia aplicando eventos | Test + REA |
| `Apply` **nunca valida** | La historia no se discute | Test (reproducir un hecho que las reglas de hoy rechazarían) |
| El dominio no conoce el almacenamiento | Se puede cambiar el almacén | Test + REA |
| Una corrección es un **evento nuevo** (`StockAdjusted`) | La historia no se reescribe | Test |

## 6. Comandos que deciden, eventos que cuentan

```csharp
public void Ship(int quantity, string reference)
{
    EnsureActive();
    if (quantity <= 0) throw new DomainException("The shipped quantity must be positive.");
    if (quantity > OnHand) throw new DomainException($"Cannot ship {quantity}: only {OnHand} on hand.");
    Raise(new StockShipped(quantity, reference.Trim()));     // decide → raise
}

private void Apply(IDomainEvent e)                          // sólo registra lo que YA pasó
{
    switch (e)
    {
        case StockShipped s: OnHand -= s.Quantity; break;
        ...
    }
    Version++;
}
```

Hay **dos caminos** al mismo estado: un comando valida y levanta un evento; reproducir la historia solo aplica eventos, sin validar (si lo hiciera, una regla nueva podría impedir reconstruir el pasado).

### Eventos con intención
Microsoft: *"Design events to capture the business intent behind each change... an event that records 'two seats were reserved' is more valuable than an event that records 'remaining seats changed to 42'."* Por eso `StockReceived(10, supplier)`, `StockShipped(4, reference)` y `StockAdjusted(-2, "damaged")`, no `StockChanged(newValue)`.

## 7. Las piezas

### El almacén de eventos y la concurrencia optimista
```csharp
Task AppendAsync(string streamId, int expectedVersion, IReadOnlyList<NewEvent> events, DateTimeOffset at, ...);
// si el stream ya no está en expectedVersion → ConcurrencyException y no se escribe nada
```
Microsoft: *"Event stores address this scenario by using optimistic concurrency control and reject an append if the stream changed since it was read. Upon rejection, the handler reloads the entity, reevaluates, and retries."* El `StockService` lo hace así, y un test demuestra lo importante: tras recargar, **las reglas se evalúan contra el estado nuevo** (si otro ya envió 3 de 5, tu envío de 4 se rechaza en el reintento).

### Snapshots (una optimización, no la verdad)
Cada 5 eventos se guarda una foto del estado. Cargar = foto + solo los eventos posteriores. Microsoft: *"Snapshots are an optimization, not a replacement for the eventstream."* Los tests prueban que cargar con o sin snapshot da **exactamente el mismo estado**, y que se puede borrar el snapshot y reconstruir.

### Evolución de eventos (upcasting)
Los eventos guardados no se tocan. Si cambia su forma, un *upcaster* los convierte al leerlos. En el sample, `stock-received` v1 guardaba `{"qty":5}`; la v2 usa `{"quantity":5,"supplier":null}`. Se leen los viejos sin reescribir nada. Microsoft: *"Upcasting: Register transformation functions that convert older event schemas to the current schema during deserialization. You can chain upcasters"* (hay un test que encadena dos).

### Viaje en el tiempo
`LoadAsOfAsync(sku, momento)` repite solo los eventos registrados hasta ese momento (ignora snapshots). Responde "¿cuánto había el martes a las 10?".

### Proyecciones (modelos de lectura)
`StockProjection` lee **todos** los eventos en orden global y construye el stock actual y la lista de movimientos con saldo. Es desechable: `RebuildAsync()` la vuelve a crear, y un test comprueba que la reconstrucción es idéntica. Y un nuevo modelo de lectura (p. ej. "total enviado") no exige cambiar la escritura.

## 8. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

```bash
mkdir MiLedger && cd MiLedger
dotnet new sln -n MiLedger
dotnet new classlib -n Ledger.Domain         -o src/Ledger.Domain
dotnet new classlib -n Ledger.Infrastructure -o src/Ledger.Infrastructure
dotnet new web      -n Ledger.Api            -o src/Ledger.Api
dotnet new xunit    -n Ledger.Tests          -o tests/Ledger.Tests
dotnet add src/Ledger.Infrastructure reference src/Ledger.Domain
dotnet add src/Ledger.Api reference src/Ledger.Domain src/Ledger.Infrastructure
```

**Orden de construcción:**

1. **Eventos** (en pasado, records inmutables) y el **agregado**: comandos que deciden y `Apply` que aplica. Pruébalo con *given-when-then*: historia de eventos → comando → eventos nuevos o rechazo.
2. **Almacén de eventos** append-only con versión por stream y posición global; test de concurrencia.
3. **Serializador** con registro de tipos y versión de esquema. Desde el primer día guarda la versión.
4. **Repositorio**: cargar (rehidratar) y guardar (añadir con versión esperada).
5. **Servicio de comandos** con reintento ante conflicto.
6. **Proyección** y sus consultas.
7. **Snapshots** solo cuando midas que los streams son largos.
8. **Tests de arquitectura** (sección 5).

## 9. Tests: *given-when-then*

Microsoft: *"Set up past events, issue a command, and assert on the new events produced. This given-when-then approach tests business logic without databases, queues, or projections."*

| Qué | Dónde |
|---|---|
| Reglas del agregado (sin infraestructura) | `DomainTests` |
| Almacén (versiones, posiciones, conflictos) | `EventStoreTests` |
| Serializador (ida y vuelta, upcasting, errores) | `EventSerializerTests` |
| Repositorio y servicio (snapshots, conflictos, viaje en el tiempo) | `RepositoryTests` |
| Proyección (reconstrucción, idempotencia, upcasting) | `ProjectionTests` |
| API | `ApiTests` |
| Reglas de diseño | `ArchitectureTests` |

### Pruebas de mutación (que los tests muerden)
En copias temporales, sin tocar el código real. Seis, todas atrapadas:

1. **`Apply` empieza a validar** (la historia puede rechazarse) → falla `ReplayingHistory_DoesNotRevalidateIt…`.
2. **El almacén ignora la versión esperada** → fallan 6 o más tests (concurrencia, registro duplicado, reintentos).
3. **Cargar con snapshot olvida los eventos posteriores** → fallan 3 (`…GivesTheSameState`, `…CanBeDeleted…`, `…ReadsOnlyTheEventsAfterIt`).
4. **Se quita el upcaster** → fallan los 2 tests de eventos antiguos.
5. **Un evento nuevo sin registrar** → falla `EveryEvent_IsRegisteredInTheSerializer…`.
6. **Un evento con una propiedad mutable** → falla `EveryEvent_IsAnImmutableSealedRecord`.

(Mi primera versión del test de reintento exigía enviar 12 con 10 en stock: el dominio lo rechazaba antes de llegar al conflicto. Lo corregí para que el reintento tenga éxito de verdad.)

## 10. Verificación del binario con REA

| Hecho | Evidencia |
|---|---|
| `Ledger.Domain.dll` referencia solo ensamblados del framework: no conoce almacenamiento ni otros proyectos | `ev_9436fc379…f6bd` |
| `Ledger.Infrastructure.dll` referencia, de lo nuestro, **solo `Ledger.Domain`** | `ev_300b1a339…c86a` |
| En `StockItem`: constructor **privado**, todos los setters **privados**, `Apply`/`Raise` **privados**; públicos solo los comandos, `Rehydrate`, `ToSnapshot` y `PullUncommitted` | `inspect_managed_members`, `ev_91ffb214a…902b` |

REA **no** prueba el comportamiento en ejecución (para eso están los 91 tests).

## 11. Receta: añadir el recuento físico del almacén

Requisito: un empleado cuenta lo que hay y el sistema debe reflejarlo, conservando que fue un recuento.

1. **Evento nuevo con intención**: `StockRecounted(int Counted)` (no `StockChanged`). Termina en "ed" y es un record inmutable.
2. **Registrarlo** en `EventSerializer.CreateDefault()` (`.Register<StockRecounted>("stock-recounted")`). Si lo olvidas, `EveryEvent_IsRegisteredInTheSerializer…` falla.
3. **Comando** `Recount(int counted)` en `StockItem`: decide (cantidad ≥ 0, artículo activo) y levanta el evento; `Apply` pone `OnHand = Counted`.
4. **Proyección**: un `case` nuevo que añade un movimiento "Recounted" con la diferencia.
5. **Endpoint** `POST /items/{sku}/recount`.
6. **Tests**: given-when-then del agregado + un test de la proyección.
7. **Los streams antiguos no se tocan**: siguen siendo válidos.

### Si cambia la forma de un evento
Sube `currentVersion`, añade el `Upcast` de la versión anterior y un test con datos antiguos. **No reescribas** los eventos guardados.

## 12. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| Eventos que solo dicen el nuevo valor (`StockChanged(42)`) | Pierdes el porqué; la auditoría no sirve | Eventos con intención |
| Editar o borrar un evento "para arreglar" | Se rompe la historia y las proyecciones | Evento compensatorio |
| Validar al reproducir (`Apply` con reglas) | Una regla nueva rompe la reconstrucción del pasado | `Apply` sin validar |
| Olvidar la versión del esquema en el evento | No puedes evolucionar | Guardarla desde el día 1 |
| Cambiar el nombre/forma de un evento sin upcaster | Los eventos viejos no se pueden leer | Versionar + upcaster |
| Snapshots "como fuente de verdad" | Si se corrompe, pierdes datos | Son una caché; los eventos mandan |
| Streams enormes (un único stream para todo) | Reconstruir cuesta mucho y hay contención | Un stream por agregado |
| Esperar consistencia inmediata en las proyecciones | Lecturas desfasadas | Diseñar para consistencia eventual (aquí se evita poniéndose al día en cada lectura) |
| Usarlo en todo el sistema | Coste enorme sin beneficio | Solo donde aporte (Microsoft) |
| Datos personales dentro de los eventos | Choca con el derecho al olvido | Guardarlos fuera y referenciarlos, o cifrar por sujeto (*crypto-shredding*) |

## 13. Límites de este sample (honestidad)

- **El almacén es memoria con `lock`**: no es durable y no es un producto de Event Store real (EventStoreDB, Marten, SQL...).
- **La proyección se pone al día en cada lectura** (`CatchUpAsync`) para que se vean tus propias escrituras. En un sistema real sería un proceso aparte con consistencia eventual y un broker (ver el kit de Event-Driven).
- **Los eventos no se publican a otros sistemas**; no hay *outbox* ni eventos de integración.
- **Sin efectos secundarios externos.** Fowler avisa de que al reproducir hay que *"wrap any external systems with a Gateway"*, porque esos sistemas *"don't know the difference between real processing and replays"*; aquí no hay emails ni pagos que se repitieran.
- **El viaje en el tiempo ignora los snapshots**, así que en streams largos es lento.
- **El tiempo de cada evento es el del guardado**, no un tiempo de negocio separado.
- **Datos personales y borrado**: no resuelto (ver la tabla de errores).
- **Un stream por artículo**: las invariantes entre varios artículos (un pedido con muchas líneas) no están cubiertas.
- **No hay procesos de larga duración** (sagas) ni gestión de errores de proyección.

## 14. Checklist

- [ ] ¿Realmente necesito historial/auditoría/reconstrucción, o me basta un CRUD?
- [ ] Los eventos están en pasado, tienen intención y son inmutables
- [ ] Guardo la versión del esquema de cada evento
- [ ] Mi `Apply` no valida; mis comandos sí
- [ ] Hay concurrencia optimista y sé qué pasa al reintentar
- [ ] Las correcciones son eventos nuevos, nunca ediciones
- [ ] Los snapshots son prescindibles y probé que dan el mismo estado
- [ ] Las proyecciones se pueden reconstruir
- [ ] Tengo un plan para los datos personales
- [ ] Hay tests given-when-then y de arquitectura, y probé al menos una mutación
