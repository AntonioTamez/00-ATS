# Microservicios vs Monolito / Monolito modular / Layered / Clean / CQRS / Event-Driven

Los microservicios responden a una pregunta de **despliegue y organización de equipos**: "¿cada parte del negocio vive y se despliega por separado?". No dicen cómo se organiza el interior de cada servicio ni cómo se comunican (síncrono o por eventos).

> Fuentes: [Fowler – Microservices](https://martinfowler.com/articles/microservices.html), [Fowler – MonolithFirst](https://martinfowler.com/bliki/MonolithFirst.html) y [Microsoft – Architecture styles](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/). Ejemplos: [`MICROSERVICIOS/sample`](../sample), [`MODULAR-MONOLITH/sample`](../../MODULAR-MONOLITH/sample), [`CQRS/sample`](../../CQRS/sample).

---

## 1. Qué responde cada cosa

| Idea | Responde a… |
|---|---|
| **Monolito (sin fronteras)** | ¿Todo junto? Simple pero se enreda |
| **Monolito modular** | ¿Cómo divido el negocio con fronteras reales, en un solo despliegue? |
| **Microservicios** | ¿Cómo despliego y escalo cada parte por separado? |
| **Layered / Clean / Hexagonal / Vertical Slice** | ¿Cómo organizo el interior de una pieza? |
| **CQRS / DDD** | ¿Cómo modelo la escritura, la lectura y las reglas? |
| **Event-Driven** | ¿Cómo se comunican las partes sin esperarse? |

## 2. El mismo flujo, dos arquitecturas

"Hacer un pedido que reserva stock" en el [Modular Monolith](../../MODULAR-MONOLITH/sample) y aquí:

| | Modular Monolith | Microservicios |
|---|---|---|
| Llamada a Catalog | `await catalog.ReserveAsync(...)` en proceso (`ICatalogApi`) | `POST /reservations` por HTTP |
| ¿Puede fallar la llamada por la red? | No | **Sí**: timeout, caída, 5xx, respuesta perdida |
| Si la 2.ª línea falla | Se libera la 1.ª llamando a `ReleaseAsync` | Se libera con `DELETE /reservations/{id}` **que también puede fallar** |
| Reintentar | Innecesario | Necesario, y solo es seguro con **idempotencia** |
| Datos | Almacén por módulo | Base por servicio |
| Qué ve el cliente si Catalog falla | La excepción del proceso | `503 + Retry-After` |
| Tests de fallos | Raros | Centrales (`ScriptedLink`) |

Lo que más cambia **no es el código de negocio, es todo el manejo del fallo**. Por eso Fowler recomienda empezar modular y dividir cuando haya un dolor real.

## 3. Frente a cada una

### Monolito modular
Mismas fronteras, sin red. Es el punto de partida recomendado y el camino de migración: si el módulo ya habla con los demás solo por un contrato (`ICatalogApi`), extraerlo es **cambiar la implementación de la interfaz por un cliente HTTP** y mover su almacén a una base propia. Si los módulos compartían tablas o tipos, no es mover: es reescribir.

### Layered / N-Tier
Divide por capa técnica. Microsoft señala que esa división horizontal *"can make it difficult to introduce changes without affecting multiple parts of the application"*. Los microservicios dividen por **capacidad de negocio** (cada servicio tiene dentro sus propias capas).

### Clean / Hexagonal / Onion / Vertical Slice
Son para el **interior** de cada servicio. Un servicio con lógica rica puede usar Clean/Hexagonal; uno pequeño puede ser un Vertical Slice o poco más. El sample usa servicios deliberadamente mínimos.

### CQRS
Compatible: un servicio puede aplicar CQRS por dentro, o el modelo de lectura de un servicio puede alimentarse de los eventos de otros (resolviendo el "no hay JOIN entre servicios").

### Event-Driven (siguiente tema)
Cambia la **forma de comunicarse**: en lugar de que Orders espere la respuesta de Catalog (síncrono), se publican eventos. Reduce el acoplamiento temporal pero introduce consistencia eventual y mensajería (duplicados, orden, outbox).

## 4. Cuándo usar microservicios

### Úsalos cuando
- Varios equipos necesitan **desplegar de forma independiente**.
- Partes del sistema tienen **escala o disponibilidad muy distintas**.
- Las fronteras de negocio son **estables y conocidas** (no se descubren a ciegas).
- Hay **madurez operativa**: CI/CD, observabilidad, gestión de configuración y de incidentes. Microsoft: *"Success requires mature development and DevOps practices"*.

### No los uses (todavía) cuando
- Es un proyecto nuevo o el dominio no está claro: *"you shouldn't start a new project with microservices"* (Fowler).
- Un solo equipo pequeño mantiene todo.
- La razón es "porque es moderno".
- No puedes asumir consistencia eventual ni fallos parciales.

## 5. Cómo se combinan

```
Microservicios            → cómo se despliega el sistema
  └ cada servicio puede ser un Vertical Slice, Clean/Hexagonal, o un CRUD simple
  └ dentro, DDD táctico y/o CQRS si el dominio lo pide
  └ entre servicios: HTTP síncrono y/o eventos asíncronos
```

Un sistema real mezcla estilos: unos servicios CRUD, otro con CQRS y dominio rico, y el resto de módulos agrupados en un monolito modular. **No hay que elegir un solo estilo para todo.**

## 6. Qué muestra cada sample

| Aspecto | MODULAR MONOLITH | CQRS | **MICROSERVICIOS** |
|---|---|---|---|
| Unidad | Módulo (proyecto) | Lado de comandos/consultas | **Servicio (proceso)** |
| Comunicación | Contrato en proceso + eventos en memoria | Eventos internos | **HTTP entre servicios** |
| Fallos de red | No existen | No existen | **Simulados y probados** |
| Datos | Almacén por módulo | Dos almacenes | **Base por servicio** |
| Idempotencia | No hace falta | Projector idempotente | **Clave para reintentos seguros** |
| Reglas verificadas | Fronteras entre módulos | Separación comando/consulta | **Independencia de servicios** |

## 7. Errores al comparar

- "Microservicios = código más limpio": no. Son una decisión de despliegue; el código sucio sigue sucio, ahora con red.
- "Más servicios = más desacoplado": servicios que se llaman en cadena y despliegan juntos forman un **monolito distribuido**.
- "Hay que migrar todo": se puede extraer solo lo que duele y dejar el resto.
- "HTTP por delante de un módulo = microservicio": Fowler avisa de que *"a good in-process interface is usually not a good service interface"*; hay que rediseñar la API pensando en fallos y latencia.
- "Los microservicios exigen Kubernetes": no es requisito del estilo.
