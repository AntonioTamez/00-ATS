# DDD táctico vs Clean / Hexagonal / Onion / Vertical Slice

La pregunta correcta no es "¿DDD o Clean?". **DDD no es una arquitectura**: es una forma de modelar el dominio. Las demás son formas de **organizar las dependencias** del código. Este documento explica cómo encajan, cuándo aplicar DDD y cómo combinarlo.

> Fuentes: [Evans – DDD Reference](https://www.domainlanguage.com/wp-content/uploads/2016/05/DDD_Reference_2015-03.pdf), [Microsoft – Tactical DDD](https://learn.microsoft.com/en-us/azure/architecture/microservices/model/tactical-domain-driven-design) y las de cada arquitectura en sus carpetas.
> Ejemplos: [`CLEAN/sample`](../../CLEAN/sample), [`HEXAGONAL/sample`](../../HEXAGONAL/sample), [`ONION/sample`](../../ONION/sample), [`VERTICAL-SLICE/sample`](../../VERTICAL-SLICE/sample), [`DDD/sample`](../sample).

---

## 1. Qué responde cada cosa

| | Responde a… | Ejemplo de decisión |
|---|---|---|
| **DDD estratégico** | ¿Cómo divido el problema y hablo con el negocio? | "Reservas y Facturación son contextos distintos" |
| **DDD táctico** | ¿Cómo expreso el modelo dentro de un contexto? | "Reservation es un agregado; Money es un value object" |
| **Clean / Onion / Hexagonal** | ¿Hacia dónde apuntan las dependencias y dónde viven los detalles técnicos? | "El dominio no referencia a la base de datos" |
| **Vertical Slice** | ¿Cómo agrupo el código para que los cambios sean locales? | "Una carpeta por funcionalidad" |

Una arquitectura sin modelo de dominio rico produce capas limpias con **lógica anémica**; un modelo rico sin arquitectura acaba acoplado a la infraestructura. Se necesitan mutuamente.

## 2. Cómo encaja DDD táctico en cada arquitectura

| Arquitectura | Dónde viven los bloques DDD | Observaciones |
|---|---|---|
| **Clean** | Entities = agregados, entidades y value objects. Use Cases = servicios de aplicación | Las reglas deben estar en las entidades, no en los casos de uso |
| **Onion** | Anillo 1 (Domain Model) = entidades y value objects. Anillo 2 (Domain Services) = servicios de dominio + interfaces de repositorio. Anillo 3 = servicios de aplicación | Es la que usa el vocabulario DDD más literalmente |
| **Hexagonal** | El hexágono = dominio + servicios; los repositorios y notificadores son puertos de salida | Hexagonal no dice cómo es el interior; DDD lo rellena |
| **Vertical Slice** | Una entidad/agregado compartido con las reglas; cada `Handler` = servicio de aplicación de una petición | Las queries pueden saltarse el modelo (proyección directa) |

Evans lo anticipa: propone aislar el dominio en una capa y añade que patrones como Hexagonal *"may serve as well or better"* mientras logren que el modelo no dependa de otros aspectos del sistema. Lo que importa es **el aislamiento**, no el nombre.

## 3. Qué muestra cada sample sobre DDD

| Aspecto | CLEAN | ONION | VERTICAL SLICE | DDD |
|---|---|---|---|---|
| Value objects | No | `Money` | No | `Money`, `DateRange`, `GuestInfo`, ids tipados |
| Agregados con raíz explícita | Entidad simple | `Order`, `Customer` | `Ticket` | `Reservation`, `Room` (+ entidad hija `Charge`) |
| Referencia entre agregados | — | `CustomerId` | — | Solo por identidad (`RoomId`), **verificado por test** |
| Eventos de dominio | No | No | No | **Sí**, con dispatcher y handlers |
| Servicio de dominio | No | `OrderPlacementService` | No | `BookingService` |
| Repositorio por raíz | `ITodoRepository` | `IOrderRepository` | `DbContext` directo | `IRepository<TRoot,TId>` (**el compilador lo exige**) |
| Reglas como tests | Dependencias entre capas | Dependencias entre anillos | Aislamiento entre slices | **Reglas tácticas** (sin setters públicos, VOs inmutables…) |
| Constructores | Públicos / fábrica | Fábrica | Fábrica | **Privados + fábrica** |

Honestidad: los samples anteriores aplican parte del vocabulario DDD sin proponérselo. La diferencia en este es que **el modelo es el protagonista** y las reglas tácticas se verifican.

## 4. Cuándo aplicar DDD táctico

### Aplícalo cuando
- El dominio tiene **reglas ricas y cambiantes** (estados, transiciones, invariantes, políticas).
- El sistema vivirá **años** y el negocio va a evolucionar.
- Tienes **acceso a expertos del dominio** con quienes construir un lenguaje común.
- Hay conceptos que se repiten con matices en varias partes y necesitas un solo lugar para sus reglas.

### No lo apliques (o hazlo ligero) cuando
- Es un **CRUD** sin reglas: tablas y formularios.
- Es un prototipo, un script o un sistema de vida corta.
- No hay acceso al negocio: sin lenguaje ubicuo, el modelo es adivinanza.
- El equipo no domina todavía el modelado: empieza por los **value objects** (el beneficio más barato) antes de agregados y eventos.

Costo real: más clases, más vocabulario y la disciplina de mantener la frontera de los agregados. Si el dominio no lo justifica, no se paga.

## 5. Cuándo combinar (y cómo)

La combinación habitual: **una arquitectura para las dependencias + DDD táctico para el modelo**.

1. **Elige el eje organizativo** (capas con Clean/Onion/Hexagonal, o slices) según tu equipo y tu complejidad técnica.
2. **Coloca el modelo DDD en el centro**: value objects, agregados, eventos y servicios de dominio no dependen de nada externo.
3. **Pon los repositorios como interfaces del dominio** y sus implementaciones afuera.
4. **Servicios de aplicación finos**: cargar un agregado, llamar a su comportamiento, guardar, entregar eventos.
5. **Verifica con tests**: dependencias (arquitectura) **y** reglas tácticas (sin setters públicos, referencias por identidad…).
6. **Usa DDD estratégico para decidir las fronteras mayores**: Microsoft sugiere diseñar un microservicio *"no smaller than an aggregate and no larger than a bounded context"*. En un monolito modular, cada módulo es un bounded context (siguiente tema del catálogo).

Estructura combinada de ejemplo:

```
src/
├─ MyApp.Domain/              ← DDD táctico: VOs, agregados, eventos, servicios de dominio, interfaces de repositorio
├─ MyApp.Application/         ← servicios de aplicación, dispatcher de eventos, puertos
├─ MyApp.Infrastructure/      ← repositorios (EF), mensajería, reloj
└─ MyApp.Api/                 ← composition root, endpoints
```

Con Vertical Slice: la misma idea, pero `Application` se reparte en carpetas por funcionalidad y el dominio (agregados) queda compartido en `Common/`.

## 6. Guía rápida de decisión

| Tu situación | Haz esto |
|---|---|
| Dominio complejo y duradero | DDD táctico dentro de Clean/Onion/Hexagonal |
| CRUD simple | Ni DDD ni capas pesadas; un proyecto ordenado basta |
| Muchas funcionalidades independientes y poco dominio compartido | Vertical Slice; DDD ligero (value objects) donde haga falta |
| Mucha lógica compartida entre funcionalidades | DDD con dominio rico + servicios de aplicación finos |
| Equipo nuevo en DDD | Empieza por value objects y lenguaje ubicuo; añade agregados y eventos cuando aparezcan las invariantes |
| Dudas sobre las fronteras del sistema | DDD estratégico antes que táctico |

## 7. Errores al comparar

- "DDD es una arquitectura de capas": no; es modelado. Evans propone capas como un patrón más, no como su esencia.
- "Si uso Clean ya hago DDD": no necesariamente. Puedes tener capas limpias con entidades anémicas.
- "DDD exige microservicios": no; un bounded context cabe en un monolito.
- "Un agregado por tabla": el agregado se define por **invariantes y consistencia**, no por el esquema de datos.
- "Más patrones DDD = mejor": usa los que resuelvan un problema real; los value objects suelen ser el mejor punto de partida.
- "Los eventos de dominio son lo mismo que los de integración": los internos viven dentro del bounded context; los de integración cruzan fronteras por un broker tras confirmar la transacción (Microsoft).
