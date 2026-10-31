# Guía: Event-Driven Architecture paso a paso

Para un dev junior que quiere construir un proyecto básico donde las partes **se avisan de lo que pasó, sin esperarse**. El sample (pedidos + inventario + notificaciones) tiene 55 tests, incluidos fallos del broker, duplicados y mensajes envenenados, y sus binarios están analizados con REA.

> Fuentes: [Microsoft – Event-driven architecture style](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/event-driven) y [Martin Fowler – What do you mean by "Event-Driven"?](https://martinfowler.com/articles/201701-event-driven.html). Las citas están copiadas de esas páginas. Los nombres de tipos de mensaje, el diseño del broker en memoria, el outbox/inbox y las reglas de los tests son **decisiones de este kit**, no un estándar.

---

## 1. Qué es

Microsoft: una arquitectura dirigida por eventos *"consists of event producers that generate a stream of events, event consumers that listen for these events, and event channels (often implemented as event brokers or ingestion services) that transfer events from producers to consumers"*. Lo esencial: *"a producer doesn't know which consumers are listening"*.

| | Llamada directa (request/response) | Eventos |
|---|---|---|
| Quién avisa | El que llama pide algo y **espera** | El que publica cuenta lo que **pasó** y sigue |
| Acoplamiento | A quién llama, y a que esté vivo | Solo al formato del mensaje |
| Si el otro está caído | Falla | Se encola y se procesa al volver |
| Respuesta | Inmediata | Eventual |
| Añadir un interesado | Tocar al que llama | Solo suscribirlo |

Beneficio que Microsoft destaca: *"New consumers can be added without modifying producers or other consumers"*.

## 2. Antes de seguir: ¿lo necesitas?

Microsoft indica que **no** es adecuado cuando:
- *"The workload has simple request-response workflows where synchronous calls meet your latency and throughput requirements"* (el coste operativo no se justifica).
- *"Business transactions require strong consistency across services"*.
- *"Your team doesn't have experience operating distributed asynchronous systems"*.

Y sí cuando *"Multiple subsystems must process the same events"* o necesitas *"to decouple producers and consumers for independent scalability and reliability goals"*.

## 3. Conceptos que conviene separar

| Concepto | Qué es |
|---|---|
| **Evento** | Un hecho **ya ocurrido**, en pasado: `OrderPlaced`. Quien lo publica no espera nada |
| **Comando** | Una orden a alguien concreto: `ReserveStock`. Puede ser rechazada |
| **Mensaje** | El sobre que viaja (puede contener un evento o un comando) |
| **Broker / canal** | Quien transporta y entrega: RabbitMQ, Service Bus, Kafka... |

Modelos del broker (Microsoft): **publish-subscribe** (*"After the event is received, it isn't stored in a durable log, so new subscribers don't see past events"*) o **event streaming** (un log durable donde el cliente puede *"replay events"*). El sample imita el primero.

Topologías (Microsoft): **broker** (*"Components broadcast events to the entire system"*, sin coordinador central) o **mediator** (*"an event mediator that manages and controls the flow of events"*). El sample usa **broker** (coreografía): nadie dirige el flujo completo.

Fowler distingue cuatro cosas que la gente llama "event-driven": *Event Notification*, *Event-carried State Transfer*, *Event Sourcing* y *CQRS*. Este sample es principalmente **Event Notification**: *"nice because it implies a low level of coupling, and is pretty simple to set up"*, con su coste: *"it can be hard to see such a flow as it's not explicit in any program text"*.

Tamaño del mensaje (Microsoft): incluir todos los atributos evita consultas pero *"can lead to data consistency problems"*; incluir solo claves da *"better data consistency"* pero obliga al consumidor a consultar. El sample lleva lo mínimo que cada consumidor necesita.

## 4. Estructura del sample

```
Eda.Messaging       ← fontanería técnica: broker en memoria, outbox, inbox, pump. NO conoce ningún evento de negocio
Eda.Orders          ← participante: pedidos. Datos propios, outbox propio
Eda.Inventory       ← participante: stock. Datos propios, outbox propio
Eda.Notifications   ← participante: avisos. Solo escucha
Eda.Host            ← composition root (y un worker que "hace pasar el tiempo")
Eda.Tests
```

El flujo (coreografía; nadie lo dirige):

```
POST /orders
   │  guarda Pedido(Pending) + mensaje OrderPlaced  ← en UN paso atómico (outbox)
   ▼
 [relay publica]──► orders.order-placed ──► Inventory: reserva stock
                                                │ guarda reserva + mensaje   (outbox)
              ┌─ stock-reserved ◄───────────────┤
              └─ stock-rejected ◄───────────────┘
   ▼
 Orders: Pending → Confirmed / Rejected     (+ mensaje en su outbox)
   ▼
 order-confirmed / order-rejected ──► Notifications: avisa al cliente
```

## 5. Reglas de diseño

| Regla | Por qué | Cómo se verifica |
|---|---|---|
| Un participante solo referencia a `Eda.Messaging` | Si referencia a otro, no es desacoplado | Test de arquitectura + REA |
| El plumbing no conoce a ningún participante | Debe ser reutilizable | Test + REA |
| Cada consumidor tiene **su copia privada** de los mensajes que lee | Se comparte el formato, no el código | Test: los `*Message` no son visibles |
| Se publica **solo a través del outbox**, nunca directo al broker | Si no, el cambio y el aviso pueden desincronizarse | Test: ningún constructor recibe `IMessageBus` |
| Cada consumidor usa un **inbox** | Los duplicados son normales | Test de comportamiento con duplicados en todo el sistema |
| Los datos y colas son privados | Nadie lee el almacén de otro | Test: `*Store`, `*Log`, `*Outbox`, `*Inbox` no visibles |
| Una transición de estado inválida se ignora | Un mensaje tardío no puede corromper el estado | Test con un `stock-reserved` tardío |

## 6. Las defensas que hacen fiable el sistema

Un broker real entrega **al menos una vez**: puede entregar el mismo mensaje dos veces, reordenar, perder la conexión o caerse. Cada defensa responde a un fallo concreto:

| Fallo | Defensa | En el sample |
|---|---|---|
| Guardé el pedido pero no pude avisar (o avisé de un pedido que no se guardó) | **Transactional Outbox**: estado y mensaje se guardan juntos; un *relay* publica después | `Outbox.Atomically` + `OutboxRelay`. Microsoft (página del patrón CQRS): *"Use the Transactional Outbox pattern to persist the state change and event atomically"* |
| El broker está caído | El mensaje queda en el outbox y se envía al volver | Test `WhenTheBrokerIsUnreachable…` |
| Me llega el mismo mensaje dos veces | **Consumidor idempotente** (inbox): registro de ids procesados | `Inbox.ProcessOnceAsync`. Microsoft: *"idempotent message processing"* |
| Mi handler falla | **Reintentos** y luego **dead-letter** | Hasta 3 intentos y a `DeadLetters`; Microsoft: *"forward the event to a dead-letter queue (DLQ) for administrator inspection"* |
| Un mensaje malformado ("veneno") | Se aísla en la DLQ sin bloquear a los demás | Test `APoisonMessage…` |
| Un participante está caído | El broker acumula; los demás siguen | Test `WhenInventoryIsDown…` |
| El productor cambia el formato | **Lector tolerante**: se leen solo los campos necesarios | Test con campos extra |
| Un mensaje tardío o contradictorio | **Máquina de estados**: solo transiciones válidas | `Order.Confirm()` solo desde `Pending` |
| No sé qué mensajes forman una operación | **Correlation id** en cada mensaje | `Message.CorrelationId` (= id del pedido) |

Microsoft sobre el último punto: *"include a correlation ID in every event so that all downstream consumers and logging systems can connect related operations into a single trace"*.

## 7. Consistencia eventual: lo que más confunde

`POST /orders` responde **`202 Accepted`** con `status: Pending`: significa *"lo recibí y lo voy a procesar"*, no *"está hecho"*. Un `GET` inmediato dice `Pending`; segundos después, `Confirmed` o `Rejected`. Hay un test que lo demuestra.

Consecuencias de diseño:
- La pantalla debe mostrar "procesando" y refrescarse (polling, notificación push).
- El cliente no puede asumir que el stock ya bajó.
- Para deshacer algo se publica **otro evento** (`order-cancelled`), no se hace rollback: Microsoft lo llama *Compensating Transaction*.

## 8. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

```bash
mkdir MiEda && cd MiEda
dotnet new sln -n MiEda
dotnet new classlib -n Eda.Messaging -o src/Eda.Messaging
dotnet new classlib -n Eda.Orders    -o src/Eda.Orders
dotnet new classlib -n Eda.Inventory -o src/Eda.Inventory
dotnet new web      -n Eda.Host      -o src/Eda.Host
dotnet new xunit    -n Eda.Tests     -o tests/Eda.Tests
dotnet add src/Eda.Orders    reference src/Eda.Messaging
dotnet add src/Eda.Inventory reference src/Eda.Messaging
dotnet add src/Eda.Host      reference src/Eda.Messaging src/Eda.Orders src/Eda.Inventory
```

En cada proyecto que use endpoints o DI, añade `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.

**Orden de construcción:**

1. **Messaging**: `Message` (sobre con id, tipo, JSON, correlation id), un broker en memoria con colas por suscriptor, `Outbox`, `OutboxRelay`, `Inbox` y un `Pump` que lo mueve todo hasta quedar en calma. Test cada pieza **sola**.
2. **Primer participante (Orders)**: su estado, su máquina de estados, su outbox y su inbox. `POST` guarda estado + mensaje con `Outbox.Atomically` y responde `202`.
3. **Segundo participante (Inventory)** que **escucha** `order-placed` y publica `stock-reserved`/`stock-rejected`. Nunca referencia a Orders: define su propia copia del mensaje.
4. **Cerrar el ciclo**: Orders escucha las respuestas de Inventory.
5. **Participantes que solo escuchan** (Notifications).
6. **Host**: registra todo y llama a `ActivateSubscribers()`.
7. **Tests de fallos y de arquitectura** (sección 10).

### Ejecutarlo
`dotnet run --project src/Eda.Host`: un worker (`PumpWorker`) llama al `Pump` cada 50 ms, así que los mensajes fluyen solos. Lo probé con el host real y `curl`: `POST /orders` respondió `202`, y un `GET /orders/{id}` inmediato ya mostró `Confirmed` (50 ms pasan antes de que llegue la segunda petición), con el stock bajado y la notificación registrada. El estado `Pending` se observa de forma fiable en los tests, donde el tiempo lo controla el test; para verlo a mano, sube el intervalo del `PumpWorker`.

## 9. Las piezas clave

### Guardar el cambio y el mensaje juntos (outbox)
```csharp
outbox.Atomically(() =>
{
    store.Save(order);                                  // el cambio...
    outbox.Add(Message.Create(OrdersMessages.OrderPlaced, new { orderId = order.Id, ... }, order.Id.ToString(), now));  // ...y el aviso
});
return Results.Accepted($"/orders/{order.Id}", new { id = order.Id, status = "Pending" });
```
Nada se envía aquí. El `OutboxRelay` lo publica después; si el broker está caído, queda pendiente.

### Escuchar sin duplicar efectos (inbox)
```csharp
subscriptions.SubscribeOnce("inventory", "orders.order-placed.v1", inbox, OnOrderPlaced);
// SubscribeOnce = Subscribe + "si ya procesé este id de mensaje, no hago nada"
```

### Mi copia del mensaje, solo con lo que necesito
```csharp
internal sealed record OrderPlacedMessage(Guid OrderId, string? Sku, int Quantity);   // Inventory ignora lo demás
```

### La máquina de estados rechaza lo imposible
```csharp
public bool Confirm()
{
    if (Status != OrderStatus.Pending) return false;   // un "stock-reserved" tardío sobre un pedido ya rechazado no hace nada
    Status = OrderStatus.Confirmed;
    return true;
}
```

## 10. Tests

| Qué | Dónde | Idea |
|---|---|---|
| El plumbing solo | `MessagingTests` | Fan-out, asincronía, reintentos, DLQ, veneno, duplicados con y sin inbox, pausa/reanudación, outbox con broker caído, lector tolerante, pump |
| Los tres juntos | `FlowTests` | Camino feliz, rechazo, cancelación (compensación), participante caído, broker caído, duplicados en todo el sistema, veneno, mensaje de tipo desconocido, mensaje tardío |
| Reglas de diseño | `ArchitectureTests` | Ver sección 5 |

### El truco que lo hace testeable: el tiempo lo controla el test
En producción un worker hace pasar el tiempo. En los tests **se quita el worker** y el test llama `Settle()` (relé + entrega hasta que todo se calma). Así se ve el estado *antes* (`Pending`) y *después* (`Confirmed`) sin `Thread.Sleep` ni tests inestables.

### Pruebas de mutación (que los tests muerden)
En una copia temporal, sin tocar el código real:

1. **Orders referencia a Inventory y lo usa** → falla `AParticipant_KnowsOnlyTheMessagingPlumbing…(Orders)`.
2. **`SubscribeOnce` deja de usar el inbox** → fallan `AnInbox_MakesADuplicateDeliveryHarmless` y `DuplicateDeliveries_EverywhereInTheSystem…`.
3. **`Order.Confirm()` pierde su guarda de estado** → falla `AStockReservedMessage_ForAnOrderThatIsAlreadyDecided…`.
4. **El relay no marca como enviados los mensajes** → fallan 5 o más tests (outbox, pump, participante caído).

Aviso honesto: mi primer intento de la mutación 1 no probó nada (rompí el `.csproj` con mi propio `sed`); la repetí bien y entonces falló el test esperado.

## 11. Verificación del binario con REA

Analicé los `.dll` compilados con REA (metadatos estáticos; no ejecuta nada).

| Hecho | Evidencia |
|---|---|
| `Eda.Orders.dll` referencia, de lo nuestro, **solo `Eda.Messaging`** | `inspect_managed_artifact`, `ev_fb9d9cf0d…5072` |
| `Eda.Inventory.dll` referencia, de lo nuestro, **solo `Eda.Messaging`** | `inspect_managed_artifact`, `ev_96cf8d5db…a10f` |
| `Eda.Messaging.dll` referencia **solo ensamblados del framework** | `inspect_managed_artifact`, `ev_76fce057d…5207` |

No analicé `Eda.Notifications.dll` con REA; su regla la comprueba el test de arquitectura. REA **no** prueba el comportamiento en ejecución (para eso están los 55 tests).

## 12. Receta: añadir el participante "Shipping"

Requisito: al confirmarse un pedido, crear un envío y avisar.

1. `Eda.Shipping` (classlib) referenciando **solo** `Eda.Messaging`.
2. Define **su copia** del mensaje `order-confirmed` (solo los campos que usa).
3. Su inbox propio (`ShippingInbox : Inbox`) y su `ISubscriber` que escucha `orders.order-confirmed.v1`.
4. Su estado privado (`ShipmentStore`) y su outbox (`ShippingOutbox`) con un relay registrado.
5. Publica `shipping.shipment-created.v1`.
6. `AddShippingModule()` en el host y añade el assembly a `ArchitectureTests`.
7. **Orders no se toca.** Esa es la prueba de que el desacoplamiento funciona.

## 13. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| Publicar al broker **después** de guardar, sin outbox | Si el proceso cae entre medio, el cambio queda sin aviso | Outbox |
| Consumidores sin idempotencia | Doble cobro, doble email | Inbox / operaciones idempotentes |
| Suponer orden de llegada | Los reintentos y la DLQ reordenan | Diseñar para cualquier orden; máquina de estados |
| Eventos con todo el estado del agregado | Cualquier cambio rompe a los consumidores | Mensajes pequeños, versionados |
| Un tipo de evento compartido en una librería | Todos se despliegan juntos | Cada consumidor su copia |
| Eventos con nombres de comando ("ReserveStock") | Acopla al que publica con quien debe actuar | Eventos en pasado: "OrderPlaced" |
| Cadenas de eventos que nadie puede seguir | Nadie sabe qué pasa al hacer un pedido | Correlation id, trazas, documentación del flujo |
| Sin plan para la DLQ | Los mensajes fallidos se acumulan sin que nadie los mire | Alertas y un proceso de revisión/reproceso |
| Esperar una respuesta síncrona de un evento | Reinventas la llamada con más complejidad | Si necesitas respuesta ya, usa request/response |
| Usar eventos en un CRUD simple | Coste operativo sin beneficio | No lo uses |

## 14. Límites de este sample (honestidad)

- **El broker es un objeto en memoria**, no RabbitMQ/Service Bus/Kafka: no es durable (si el proceso muere, se pierde lo encolado) y no hay particiones ni consumidores competidores.
- **El outbox y el inbox son en memoria**: en una base real, "estado + mensaje" y "efecto + marca de procesado" deben ir en la **misma transacción de base de datos**. Aquí lo modela un `lock`.
- **El inbox marca *después* de ejecutar**: si el proceso muere entre ambas cosas, el efecto se repite al reintentar. La defensa completa es que el efecto sea idempotente por sí mismo (como la guarda `HasReservationFor` de Inventory) o que se haga en la misma transacción.
- **Sin garantía de orden**: los reintentos van al final de la cola.
- **Sin límite de espera (timeout de la saga)**: si Inventory no responde nunca, el pedido queda `Pending` para siempre. Falta un temporizador que lo rechace o lo escale.
- **Pub-sub sin replay**: un suscriptor que llega tarde no ve el pasado (hay un test que lo muestra). Para eso existen los logs de eventos (Kafka, Event Hubs).
- **DLQ sin herramientas** de inspección ni reproceso.
- **Sin registro de esquemas ni versionado real** más allá del sufijo `.v1` y los lectores tolerantes.
- **Un solo proceso**: no se modelan varias instancias de un consumidor ni concurrencia entre ellas.
- **Observabilidad mínima**: solo el correlation id y los logs del broker.

## 15. Checklist

- [ ] ¿Necesito realmente eventos, o me basta una llamada directa?
- [ ] Los mensajes son hechos en pasado, pequeños y versionados
- [ ] Cada participante guarda su cambio y su mensaje de forma atómica (outbox)
- [ ] Cada consumidor tolera duplicados (inbox o efecto idempotente)
- [ ] No dependo del orden de llegada
- [ ] Sé qué pasa si el broker o un participante están caídos
- [ ] Hay DLQ y alguien que la mire
- [ ] Todo mensaje lleva un correlation id
- [ ] La UI y la API entienden "aceptado, pendiente"
- [ ] Hay tests de fallos y probé al menos una mutación
