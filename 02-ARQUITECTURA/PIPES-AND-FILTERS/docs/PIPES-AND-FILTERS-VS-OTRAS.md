# Pipes & Filters vs otras arquitecturas

Pipes & Filters responde una pregunta que las otras no se hacen: **"¿cómo se transforma un flujo de datos, paso a paso?"**. Clean y Hexagonal hablan de hacia dónde apuntan las dependencias; Microkernel, de qué piezas se cargan en ejecución; Pipes & Filters, de **cómo viaja un dato** a través de etapas independientes.

> Fuentes: [Azure Architecture Center – Pipes and Filters](https://learn.microsoft.com/en-us/azure/architecture/patterns/pipes-and-filters), [Wikipedia – Pipeline (software)](https://en.wikipedia.org/wiki/Pipeline_(software)). Ejemplos del repo: [`PIPES-AND-FILTERS/sample`](../sample), [`MICROKERNEL/`](../../MICROKERNEL), [`EVENT-DRIVEN/`](../../EVENT-DRIVEN), [`LAYERED/`](../../LAYERED), [`HEXAGONAL/`](../../HEXAGONAL).

---

## 1. Qué organiza cada una

| Arquitectura | Qué organiza | Unidad |
|---|---|---|
| Layered | Responsabilidades técnicas apiladas | Capa |
| Clean / Hexagonal | La dirección de las dependencias | Anillo / puerto |
| Microkernel | Qué piezas se cargan en ejecución | Plugin |
| Event-Driven | Quién reacciona a qué ocurrió | Evento |
| **Pipes & Filters** | **El orden de transformación de un flujo** | **Filtro** |

## 2. Pipes & Filters vs Layered

| | Layered | Pipes & Filters |
|---|---|---|
| Forma | Pila: la presentación llama a negocio, que llama a datos | Cadena: lo que sale de un paso entra al siguiente |
| El dato | Se entrega por llamadas entre capas | **Fluye**, un elemento a la vez |
| Reordenar | No tiene sentido | Es una característica |
| Reusar una pieza | Solo dentro de su capa | Un filtro sirve en cualquier pipeline con tipos que encajen |

Se combinan: una capa de aplicación puede ejecutar un pipeline como parte de un caso de uso.

## 3. Pipes & Filters vs Microkernel

Se parecen en que ambos tienen un contrato pequeño y piezas que lo implementan, pero resuelven cosas distintas.

| | Microkernel | Pipes & Filters |
|---|---|---|
| Las piezas | Alternativas o extensiones que el núcleo **consulta** | Etapas que **se encadenan**: la salida de una es la entrada de otra |
| Quién las descubre | El núcleo, en ejecución, desde una carpeta | La raíz de composición, en compilación |
| ¿Se hablan entre sí? | No | Solo a través de la tubería |
| Pregunta que responde | "¿Quién sabe hacer esto?" | "¿En qué orden se transforma?" |

Se combinan bien: cada filtro de un pipeline puede ser un plugin que el núcleo descubre. Ese caso necesita sumar las dificultades de ambos (versionado del contrato, carga aislada).

## 4. Pipes & Filters vs Event-Driven

| | Event-Driven | Pipes & Filters |
|---|---|---|
| Acoplamiento | El emisor no sabe quién escucha | Cada filtro sabe el **tipo** que recibe y el que emite; el orden lo decide la composición |
| Flujo | Reacciones que se ramifican | Un camino lineal |
| Transporte típico | Bus o broker | Cualquier cosa: iteradores, colas, hilos |

Microsoft implementa su ejemplo con una secuencia de colas de mensajes: "A component implemented as a filter task listens for a message on this queue, performs its work, and then posts a new or transformed message to the next queue in the sequence". Es la zona donde ambos estilos se tocan: **un pipeline distribuido es event-driven en su transporte**, con todo lo que eso implica (duplicados, idempotencia, orden).

## 5. Pipes & Filters vs Microservicios

| | Microservicios | Pipes & Filters |
|---|---|---|
| Frontera | Servicio de negocio con su propio dato | Un paso de transformación sin dato propio |
| Despliegue | Por servicio | Por filtro **si hace falta**; en este sample, todos en un proceso |
| Escalado | Por servicio | Por filtro, si se distribuyen (Microsoft: poder "run parallel instances of slow filters") |

Un filtro no es un servicio: no tiene datos propios ni una API de negocio. Si distribuís los filtros, ahí sí aparecen los problemas de los sistemas distribuidos.

## 6. Parecidos que no son lo mismo

- **Chain of Responsibility**: cada manejador decide si procesa o pasa, y suele cortar la cadena. En un filtro, **todos** los elementos pasan por todos los filtros (salvo los que se rechazan), y la tarea es transformar.
- **Middleware web** (la cadena de un request en un framework web): se parece en la forma, pero va en request/response y suele ser bidireccional (entra y sale por el mismo paso). Microsoft menciona que el patrón "might not be useful" cuando "the application follows a request-response pattern".
- **LINQ / Streams**: son pipelines de filtros dentro de un proceso. El sample usa exactamente esa mecánica (`IAsyncEnumerable`), pero le agrega nombres de etapa, rechazos con motivo y atribución de fallos.
- **ETL**: es el caso de uso clásico, no un competidor.

## 7. Qué obtenés y qué pagás

| Ganás | Pagás |
|---|---|
| Piezas pequeñas, probables una por una | Cada cambio exige probar la cadena entera: "for any change, the entire filter chain should be tested end to end" |
| Reuso: los mismos filtros en pipelines distintos | Los tipos que viajan son un contrato; cambiarlos rompe filtros vecinos |
| Reordenar y agregar pasos con poco costo | Que los tipos encajen no garantiza que el orden sea equivalente |
| Memoria constante si el flujo es perezoso | Los filtros con estado (deduplicar) rompen esa propiedad |
| Escalar un paso lento por separado, si se distribuye | Complejidad, duplicados, idempotencia, pérdida de datos |

## 8. Cuándo elegir cada una

| Situación | Elección |
|---|---|
| Importar, limpiar y transformar archivos o flujos | **Pipes & Filters** |
| Procesamiento por etapas con requisitos de escala distintos | **Pipes & Filters** |
| Responder a un request con una sola operación | Nada de esto: una función |
| Reglas de negocio ricas con tecnología cambiable | Hexagonal / Clean |
| Muchas variantes del mismo comportamiento, agregadas seguido | Microkernel |
| Reaccionar a hechos del negocio | Event-Driven |
| Pasos que deben completarse juntos o fallar juntos | Una transacción, no un pipeline |

## 9. Errores al comparar

- "Pipes & Filters = microservicios encadenados": el patrón nace en un proceso. Distribuirlo es una decisión aparte con costos propios.
- "Un pipeline es un `foreach` con varios `if`": la diferencia es la independencia y el reuso de cada paso, y la memoria constante.
- "Si uso LINQ ya tengo la arquitectura": tenés la mecánica. La arquitectura es la separación en filtros nombrados, con esquemas, tests por separado, y una raíz de composición que decide el orden.
- "Los filtros deberían compartir una base de datos para coordinarse": si necesitan mucho estado compartido, el patrón no es el adecuado (Microsoft: "The amount of context or state information a step requires makes this approach inefficient").
