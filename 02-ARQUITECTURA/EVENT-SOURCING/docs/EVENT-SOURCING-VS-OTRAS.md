# Event Sourcing vs CRUD / CQRS / Event-Driven / Audit Log / DDD

Event Sourcing responde a **cómo se guarda el estado**. Se confunde con tres cosas parecidas pero distintas. Este documento las separa y dice cuándo combinarlas.

> Fuentes: [Microsoft – Event Sourcing](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing), [Fowler – Event Sourcing](https://martinfowler.com/eaaDev/EventSourcing.html), [Fowler – What do you mean by "Event-Driven"?](https://martinfowler.com/articles/201701-event-driven.html). Ejemplos: [`EVENT-SOURCING/sample`](../sample), [`CQRS/sample`](../../CQRS/sample), [`EVENT-DRIVEN/sample`](../../EVENT-DRIVEN/sample), [`DDD/sample`](../../DDD/sample).

---

## 1. Qué responde cada cosa

| Idea | Responde a… |
|---|---|
| **CRUD** | ¿Guardo el valor actual? |
| **Event Sourcing** | ¿Guardo los hechos y derivo el valor actual? |
| **Audit log** | ¿Guardo, aparte, un registro de quién cambió qué? |
| **CQRS** | ¿Separo el modelo de escribir del de leer? |
| **Event-Driven** | ¿Cómo se comunican las partes: con mensajes? |
| **DDD** | ¿Cómo modelo las reglas del negocio? |

## 2. Event Sourcing vs un "audit log" (la comparación más útil)

| | CRUD + tabla de auditoría | Event Sourcing |
|---|---|---|
| Fuente de verdad | La fila actual | Los eventos |
| El historial | Un extra que puede desincronizarse del estado | **Es** el estado: no puede desincronizarse |
| Reconstruir el pasado | No (solo ves cambios sueltos) | Sí: se repite hasta el momento deseado |
| Coste | Bajo | Alto |
| Corrección de un error | `UPDATE` (y una nota en la auditoría) | Evento compensatorio |

Si solo necesitas saber "quién cambió qué y cuándo", **una tabla de auditoría suele bastar y cuesta mucho menos**. Event Sourcing se justifica cuando quieres reconstruir estados, derivar nuevos modelos de lectura desde el historial o capturar intención.

## 3. Event Sourcing vs CQRS

| | Event Sourcing | CQRS |
|---|---|---|
| Separa | Cómo se guarda (eventos) | Lecturas de escrituras |
| ¿Necesita al otro? | No | No |
| Cómo se relacionan | Las proyecciones de ES son un modelo de lectura natural | CQRS puede usar un estado normal |

Microsoft: *"Event sourcing is commonly combined with the CQRS pattern by performing the data management tasks in response to the events and by materializing views from the stored events."* Fowler: *"Strictly CQRS isn't really about events."* En el [kit de CQRS](../../CQRS) el estado se guardaba normal y los eventos solo alimentaban la lectura: allí **no** había Event Sourcing. Aquí los eventos **son** el estado.

## 4. Event Sourcing vs Event-Driven

| | Event Sourcing | Event-Driven |
|---|---|---|
| Los eventos son… | El registro persistente del agregado | Mensajes entre participantes |
| Viven… | En el almacén, para siempre | En un broker, mientras se entregan |
| Orden y propiedad | Por stream, con versión | Por cola, sin garantía global |
| Pregunta típica | "¿Qué pasó con este artículo?" | "¿Quién necesita enterarse?" |

Microsoft insiste en no confundir un almacén de eventos con un broker: *"Don't confuse an event store with an eventstream message broker. Message brokers such as Apache Kafka typically lack per-entity stream queries and optimistic concurrency."* Pueden convivir: el almacén guarda los hechos y un proceso los publica al broker (para eso hace falta un *outbox*, ver [`EVENT-DRIVEN/`](../../EVENT-DRIVEN)).

## 5. Event Sourcing vs DDD

DDD y Event Sourcing se llevan muy bien: el **agregado** de DDD es la unidad que tiene su propio stream, y los **eventos de dominio** son los eventos guardados. Pero el [sample de DDD](../../DDD/sample) guardaba estado y levantaba eventos de dominio como notificación; aquí esos eventos son la propia persistencia. El agregado de este kit (`StockItem`) es un agregado DDD con constructor privado, sin setters públicos y que protege sus invariantes.

## 6. Cuándo usar Event Sourcing

### Úsalo cuando
- Necesitas **auditoría fuerte e historial reconstruible** (libro mayor, inventario, finanzas).
- Quieres **capturar la intención** de cada cambio.
- Quieres poder **derivar modelos de lectura nuevos** del pasado sin migrar datos.
- La **contención** de escrituras sobre el mismo dato es un problema real.
- Tu equipo ya trabaja con eventos y consistencia eventual.

### No lo uses cuando (Microsoft)
- Es un **CRUD** sin necesidad de auditoría ni reconstrucción.
- Es un **prototipo** o un sistema de vida corta.
- Necesitas **lecturas siempre al día** sin margen de retraso.
- Los datos son **estáticos o de referencia**.
- El equipo **no tiene experiencia** con arquitecturas dirigidas por eventos.

## 7. Cómo se combina

```
DDD (agregados)                  → la unidad de consistencia = un stream
Event Sourcing                   → persiste los hechos del agregado
  └ proyecciones (CQRS)          → modelos de lectura derivados
  └ outbox + broker (Event-Driven) → publicar eventos de integración a otros sistemas
Modular Monolith / Microservicios → cada módulo/servicio decide si lo usa (no hace falta en todos)
```

Microsoft recomienda **usarlo de forma selectiva**: un libro mayor de pagos sí; el perfil de usuario no.

## 8. Qué muestra cada sample

| Aspecto | DDD | CQRS | EVENT-DRIVEN | **EVENT-SOURCING** |
|---|---|---|---|---|
| ¿Qué se persiste? | Estado | Estado (+ log para la lectura) | Estado de cada participante | **Solo eventos** |
| Los eventos son… | Notificaciones internas | Alimentan la lectura | Mensajes entre participantes | **La fuente de verdad** |
| Concurrencia | — | Sin controlar | Máquina de estados | **Optimista por versión de stream** |
| Corregir un error | Modificar el agregado | Comando nuevo | Compensación por evento | **Evento compensatorio** |
| Estado pasado | No | No | No | **Sí (as-of)** |
| Reconstruir lecturas | — | Sí (projector) | — | **Sí, desde el almacén** |

## 9. Errores al comparar

- "Event Sourcing = CQRS": son independientes.
- "Event Sourcing = Event-Driven": uno es persistencia, el otro comunicación.
- "Un audit log es lo mismo": el log puede desincronizarse del estado; con Event Sourcing no hay otro estado.
- "Con eventos ya tengo auditoría gratis y sin coste": se paga en evolución de esquemas, concurrencia, proyecciones y datos personales.
- "Se puede borrar un evento erróneo": no; se compensa. Y los datos personales requieren diseño específico.
- "Todo el sistema debería ser Event Sourcing": Microsoft aconseja aplicarlo solo donde justifica su complejidad.
