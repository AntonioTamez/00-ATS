# Event-Driven vs request/response, Microservicios, Modular Monolith, CQRS y Event Sourcing

Event-Driven responde a **cómo se comunican las partes**: avisando de lo ocurrido en lugar de pedir y esperar. No dice cuántos procesos hay, ni cómo se organiza el interior, ni cómo se guarda el estado.

> Fuentes: [Microsoft – Event-driven architecture style](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/event-driven), [Fowler – What do you mean by "Event-Driven"?](https://martinfowler.com/articles/201701-event-driven.html). Ejemplos: [`EVENT-DRIVEN/sample`](../sample), [`MODULAR-MONOLITH/sample`](../../MODULAR-MONOLITH/sample), [`CQRS/sample`](../../CQRS/sample), [`MICROSERVICIOS/sample`](../../MICROSERVICIOS/sample).

---

## 1. Qué responde cada cosa

| Idea | Responde a… |
|---|---|
| **Request/response (síncrono)** | ¿Le pido algo a otro y espero la respuesta? |
| **Event-Driven** | ¿Cuento lo que pasó y dejo que los interesados reaccionen? |
| **Microservicios / Modular Monolith** | ¿Cómo divido y despliego el sistema? |
| **CQRS** | ¿Separo el modelo de escribir del de leer? |
| **Event Sourcing** | ¿Guardo el estado como secuencia de eventos? |

Son **ejes distintos**: puedes tener microservicios síncronos, un monolito modular con eventos, o microservicios con eventos.

## 2. Los cuatro significados de "event-driven" (Fowler)

| Patrón | Qué es | En el kit |
|---|---|---|
| **Event Notification** | Aviso pequeño: "esto pasó"; quien necesite más datos los pide | **Este sample** |
| **Event-carried State Transfer** | El evento lleva datos suficientes para que el consumidor no vuelva a preguntar | Parcial: `order-placed` lleva sku y cantidad |
| **Event Sourcing** | El estado *es* la secuencia de eventos | No (el estado se guarda tal cual) |
| **CQRS** | Separar lectura y escritura. Fowler: *"Strictly CQRS isn't really about events"* | [`CQRS/`](../../CQRS) |

Costes de cada uno según Fowler: Notification, el flujo *"is not explicit in any program text"*; State Transfer, *"lots of data schlepped around and lots of copies"* y más complejidad en el receptor.

## 3. Frente a cada una

### Request/response
La llamada directa es más **simple, predecible y fácil de depurar**: sabes el resultado en el momento. Microsoft advierte de que no conviene EDA cuando *"synchronous calls meet your latency and throughput requirements"*. Usa eventos cuando necesitas desacoplar tiempo, disponibilidad o el número de interesados.

El mismo flujo "hacer un pedido que reserva stock", visto en los tres kits:

| | Modular Monolith | Microservicios | **Event-Driven** |
|---|---|---|---|
| Orders → Inventory | Llamada en proceso (`ICatalogApi`) | `POST /reservations` por HTTP | **Mensaje `order-placed`** |
| Si Inventory está caído | Falla todo | `503`, no se crea el pedido | **Se acepta el pedido (`202`) y se procesa al volver** |
| ¿Quién espera a quién? | Orders espera | Orders espera (con timeout) | **Nadie espera** |
| Respuesta al cliente | Final | Final | **"Pendiente"** |
| Deshacer | Llamada de compensación | `DELETE` de compensación | **Otro evento** (`order-cancelled`) |
| Duplicados | No existen | Se evitan con idempotencia | **Normales: inbox obligatorio** |
| Entender el flujo | Leer el código | Seguir llamadas | **Seguir mensajes (más difícil)** |

### Microservicios
Compatible y muy frecuente: cada servicio publica eventos y escucha los de otros. Microsoft lista *"microservices"* entre las combinaciones típicas. Los eventos reducen el acoplamiento temporal entre servicios (el kit de microservicios sugería esto como siguiente paso para la notificación). **Microservicios sin eventos** (todo síncrono) y **eventos sin microservicios** (un monolito modular con bus interno) también existen.

### Modular Monolith
Los módulos pueden comunicarse con eventos en memoria (como en el [sample del monolito modular](../../MODULAR-MONOLITH/sample)). Diferencia con este kit: allí el bus es **síncrono y sin garantías** (un handler que falla se registra y se pierde). Aquí hay **outbox, inbox, reintentos y DLQ**, que son las garantías que necesitas al pasar a un broker real.

### CQRS
En el [sample de CQRS](../../CQRS/sample) el *projector* consume un log de eventos para construir la lectura. Es el mismo mecanismo pensado dentro de un solo sistema; aquí los eventos cruzan fronteras entre participantes.

### Event Sourcing
Distinto: ES guarda **los eventos como fuente de verdad**. En este kit los eventos son **mensajes entre partes** y el estado se guarda normalmente. Microsoft los trata por separado y avisa de que combinarlos añade complejidad.

## 4. Coreografía vs orquestación

| | Coreografía (broker, este sample) | Orquestación (mediator) |
|---|---|---|
| Quién dirige | Nadie: cada participante reacciona | Un coordinador conoce y lanza los pasos |
| Acoplamiento | Mínimo | Mayor (todos conocen al coordinador) |
| Ver el proceso completo | Difícil: está repartido | Fácil: está en un sitio |
| Reintentos y reinicios | Hay que diseñarlos en cada participante | Los gestiona el coordinador |
| Riesgo | Inconsistencia difícil de rastrear | El coordinador es cuello de botella |

Microsoft: en broker topology *"No component owns or is aware of the state of any multistep business transaction"*, mientras el mediator *"maintains the state and manages error handling and restart capabilities"*. Para flujos largos con muchos pasos y compensaciones conviene una **saga orquestada**.

## 5. Cuándo usar Event-Driven

### Úsalo cuando
- **Varios** subsistemas deben reaccionar al mismo hecho (pedido, pago, alta de usuario).
- Quieres **desacoplar disponibilidad**: que un participante caído no pare al resto.
- Quieres **añadir consumidores sin tocar al productor**.
- El volumen o la latencia aconsejan procesar de forma asíncrona.

### No lo uses cuando
- El flujo es una petición-respuesta simple.
- Necesitas **consistencia fuerte inmediata**.
- El equipo no puede operar mensajería, DLQ y consistencia eventual.
- Necesitas saber el resultado en el momento. Se puede emular con mensajería request-reply (una cola de petición y otra de respuesta, según Microsoft), pero entonces reinventas la llamada síncrona con más piezas.

## 6. Cómo se combina

```
Modular Monolith / Microservicios   → cómo divido y despliego
Event-Driven                        → cómo se avisan las partes (eventos + outbox + inbox)
CQRS                                → los eventos alimentan los modelos de lectura
DDD                                 → los eventos de dominio nacen en los agregados
Event Sourcing                      → opcional: si la historia es parte del negocio
```

Un sistema real mezcla: **llamadas síncronas** donde necesitas la respuesta (consultar precio) y **eventos** donde basta con avisar (pedido confirmado).

## 7. Qué muestra cada sample

| Aspecto | MODULAR MONOLITH | CQRS | MICROSERVICIOS | **EVENT-DRIVEN** |
|---|---|---|---|---|
| Comunicación | Contrato en proceso + eventos en memoria | Eventos hacia la lectura | HTTP síncrono | **Mensajes asíncronos con broker** |
| Entrega | Síncrona, sin garantías | Idempotente (checkpoint) | Reintentos + idempotencia | **Al menos una vez + inbox + DLQ** |
| Atomicidad estado+aviso | — | Mismo `lock` | — | **Outbox** |
| Fallo del vecino | Falla todo | — | 503 | **Se acumula y se procesa luego** |
| Reglas verificadas | Fronteras de módulos | Comando/consulta | Independencia | **Solo plumbing compartido; publicar solo por outbox** |

## 8. Errores al comparar

- "Event-Driven = microservicios": no. Pueden usarse por separado.
- "Event-Driven = Event Sourcing": no. El primero es comunicación; el segundo, persistencia.
- "Los eventos garantizan desacoplamiento": desacoplan en el tiempo y la disponibilidad, pero **acoplan al formato del mensaje** y complican el seguimiento.
- "Es más fiable": es más resiliente a caídas, pero introduce duplicados, desorden y consistencia eventual que hay que gestionar.
- "Si publico después de guardar, ya está": sin outbox, un fallo entre las dos cosas deja el sistema inconsistente.
