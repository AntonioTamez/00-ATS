# CQRS vs CRUD / Clean / Vertical Slice / DDD / Event Sourcing / Event-Driven

CQRS no compite con casi nada de lo anterior: **responde a una pregunta distinta**. No dice cómo se organizan las capas ni cómo se despliega; dice **cómo separar el modelo que cambia datos del que los lee**.

> Fuentes: [Fowler – CQRS](https://martinfowler.com/bliki/CQRS.html), [Microsoft – CQRS pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs). Ejemplos: [`CQRS/sample`](../sample), [`VERTICAL-SLICE/sample`](../../VERTICAL-SLICE/sample), [`DDD/sample`](../../DDD/sample), [`CLEAN/sample`](../../CLEAN/sample).

---

## 1. Qué responde cada cosa

| Idea | Responde a… |
|---|---|
| **CQRS** | ¿Uso el mismo modelo para escribir y para leer? |
| **Clean / Hexagonal / Onion** | ¿Hacia dónde apuntan las dependencias? |
| **Vertical Slice** | ¿Cómo agrupo el código para que un cambio sea local? |
| **DDD táctico** | ¿Cómo modelo las reglas del negocio? |
| **Event Sourcing** | ¿Guardo el estado o la historia de eventos? |
| **Event-Driven** | ¿Cómo se comunican las partes: con eventos asíncronos? |
| **Modular Monolith / Microservicios** | ¿Cómo divido y despliego el sistema? |

## 2. CQRS frente a cada una

### CRUD (el punto de partida)
Un solo modelo, una sola tabla, un solo camino. Simple y suficiente en muchos sistemas. Fowler: *"Many systems do fit a CRUD mental model, and so should be done in that style"*. **CQRS paga su coste solo cuando leer y escribir tienen necesidades distintas** (reglas complejas al escribir, pantallas con datos combinados al leer, cargas muy diferentes).

### Clean / Hexagonal / Onion
Son ortogonales. En CQRS el lado de comandos suele ser el que usa el dominio rico con estas arquitecturas; el lado de consultas puede **saltarse las capas** y leer proyecciones directas. En el sample, `Bank.Queries` no referencia al dominio en absoluto.

### Vertical Slice
Son compatibles y a menudo se mezclan: cada *slice* es un comando o una consulta (`Handler` + `Request` + `Response`). El [sample de Vertical Slice](../../VERTICAL-SLICE/sample) ya hace una forma ligera de esto (`CloseTicket` escribe, `ListTickets` lee), pero con **un solo modelo y un solo almacén**: es CQRS de nivel 1 sin llamarlo así.

### DDD táctico
El **modelo de escritura** de CQRS es donde encaja el dominio rico (agregados, invariantes). El modelo de lectura **no tiene lógica de dominio**: son DTOs. Microsoft: la lectura *"has no business logic or validation stack"*.

### Event Sourcing
Se confunde mucho con CQRS, pero son independientes. Event Sourcing guarda el estado como una secuencia de eventos; CQRS separa lectura y escritura. Pueden combinarse (Microsoft describe el almacén de eventos como el *write model* y las vistas materializadas como el *read model*), pero Microsoft advierte de que la combinación añade complejidad y consistencia eventual. **El sample no usa Event Sourcing**: guarda el estado de `Account` y mantiene un log aparte para alimentar la lectura.

### Event-Driven Architecture
CQRS de nivel 2 usa eventos para sincronizar el lado de lectura, pero no es EDA: los eventos aquí son internos a un solo sistema. EDA (siguiente tema del catálogo) trata de comunicar sistemas o módulos por eventos.

## 3. Los tres niveles, otra vez

| Nivel | Consistencia | Cuándo |
|---|---|---|
| 1. Mismos datos, modelos y caminos de código separados | Inmediata | Casi siempre es el primer paso; la mejor relación coste/beneficio |
| 2. Almacén de lectura separado (como el sample) | **Eventual** | Lecturas muy distintas de las escrituras, o carga de lectura muy superior |
| 3. + Event Sourcing | Eventual | Cuando la historia de cambios es parte del negocio (auditoría, rehacer estados) |

Empieza por el 1. Sube de nivel solo con un dolor medible.

## 4. Cuándo usar CQRS

### Úsalo cuando
- El **dominio es complejo** y las reglas de escritura merecen su propio modelo.
- Las **lecturas tienen una forma muy distinta** de las escrituras (informes, pantallas agregadas).
- La **carga de lectura es muy superior** a la de escritura y necesitas escalarlas por separado.
- Tienes **interfaces basadas en tareas** ("depositar", "cerrar cuenta") más que formularios de edición.
- Varios usuarios modifican los mismos datos a la vez y quieres comandos granulares (Microsoft).

### No lo uses cuando
- Es un CRUD o las reglas son simples.
- El equipo no puede asumir la consistencia eventual y su UI no la contempla.
- No hay ningún dolor que resolver: Fowler lo califica de *"significant mental leap"*.

## 5. Cómo se combina

```
Clean/Onion + DDD  → modelo de ESCRITURA (Commands + Domain)
Proyecciones planas → modelo de LECTURA (Queries, sin dominio)
Vertical Slice      → organiza cada comando/consulta en su carpeta
Event-Driven        → sincroniza lectura (y a otros módulos) vía eventos
Modular Monolith    → cada módulo puede aplicar CQRS por dentro si le conviene
```

No tiene por qué aplicarse a todo el sistema: Fowler dice que CQRS *"is useful in some places, but not in others"*. Un sistema puede tener módulos CRUD y un módulo CQRS (decisión de diseño de este kit, no una prescripción de las fuentes).

## 6. Qué muestra cada sample

| Aspecto | CLEAN | VERTICAL SLICE | DDD | **CQRS** |
|---|---|---|---|---|
| Modelo de lectura separado del de escritura | No | No (un solo `DbContext`) | No | **Sí: DTOs planos y almacén propio** |
| Lado de lectura sin dependencia del dominio | No | No | No | **Sí, verificado por test y por REA** |
| Consistencia | Inmediata | Inmediata | Inmediata | **Eventual (projector)** |
| Comandos devuelven | Datos | Datos | Datos | **Un id o nada** |
| Reconstruir la lectura desde el log | — | — | — | **Sí (`RebuildAsync`)** |
| Reglas verificadas | Capas | Aislamiento entre slices | Reglas tácticas | **Separación comando/consulta** |

## 7. Errores al comparar

- "CQRS = Event Sourcing": no. Pueden usarse por separado.
- "CQRS necesita un broker o microservicios": no. Puede ser dos handlers sobre la misma base.
- "CQRS es una arquitectura": es un **patrón** aplicable a una parte del sistema.
- "Con CQRS todo es asíncrono": solo el nivel 2 introduce eventual consistency, y en el sample el comando sigue siendo síncrono.
- "Más CQRS = mejor": Fowler recomienda cautela; empieza por el nivel 1 o por no usarlo.
