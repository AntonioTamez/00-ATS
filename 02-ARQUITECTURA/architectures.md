# Catálogo de arquitecturas a documentar

Cada arquitectura se documenta con el mismo formato: **guía paso a paso**, **proyecto de referencia .NET con tests de arquitectura**, **portal interactivo para novatos** y **comparativa** con las anteriores.

## Estado

| # | Arquitectura | Familia | Estado | Carpeta |
|---|---|---|---|---|
| 1 | Clean Architecture | Capas con regla de dependencia | Hecha | `CLEAN/` |
| 2 | Hexagonal (Puertos y Adaptadores) | Capas con regla de dependencia | Hecha | `HEXAGONAL/` |
| 3 | Onion Architecture | Capas con regla de dependencia | Hecha | `ONION/` |
| 4 | Vertical Slice Architecture | Organización por funcionalidad | Hecha | `VERTICAL-SLICE/` |
| 5 | DDD táctico | Modelado del dominio | Hecha | `DDD/` |
| 6 | Modular Monolith | Modularidad / despliegue | Hecha | `MODULAR-MONOLITH/` |
| 7 | CQRS (con o sin Event Sourcing) | Modelado del dominio | Hecha | `CQRS/` |
| 8 | Microservicios | Distribuidas | Hecha | `MICROSERVICIOS/` |
| 9 | Event-Driven Architecture | Distribuidas | Hecha | `EVENT-DRIVEN/` |
| 10 | Layered / N-Tier | Clásicas (punto de comparación) | Hecha | `LAYERED/` |
| 11 | MVC / MVVM | Patrones de presentación | Hecha | `MVC-MVVM/` |
| 12 | Event Sourcing | Modelado del dominio / persistencia | Hecha | `EVENT-SOURCING/` |
| 13 | Screaming Architecture | Organización por funcionalidad | Hecha | `SCREAMING/` |
| 18 | Clean + Screaming (combinación) | Dependencias hacia adentro + estructura que grita el negocio | Hecha | `CLEAN-SCREAMING/` |
| 14 | Serverless / FaaS | Distribuidas | Hecha | `SERVERLESS/` |
| 15 | Microkernel / Plugins | Extensibilidad | Hecha | `MICROKERNEL/` |
| 16 | Pipes & Filters | Procesamiento por etapas | Hecha | `PIPES-AND-FILTERS/` |
| 17 | Atomic Design + Container-Presentational + Micro-frontends | Frontend | Pendiente | |

## Detalle

### Hermanas de Clean y Hexagonal (comparten la regla de dependencia)
- **Onion Architecture** (Jeffrey Palermo): antecesor directo de Clean, con el modelo de dominio al centro.
- **Vertical Slice Architecture**: organiza por funcionalidad en vez de por capa técnica. Es la alternativa más popular hoy en .NET y la que más se confronta con Clean y Hexagonal en equipos reales.
- **Modular Monolith**: módulos con fronteras estrictas dentro de un solo despliegue. Paso previo natural a microservicios.

### Estructura del dominio
- **DDD (Domain-Driven Design) táctico**: agregados, value objects, bounded contexts. No es una arquitectura de capas, pero es lo que va dentro del núcleo de las anteriores.
- **CQRS** (con o sin Event Sourcing): separar lecturas de escrituras.

### Distribuidas
- **Microservicios**: cuándo sí y cuándo no, con el monolito modular como paso previo.
- **Event-Driven Architecture**: eventos, colas, consistencia eventual, outbox.

### Clásicas, para comparar
- **Layered (N-Tier)**: la arquitectura "de siempre". Sirve para explicar qué problema resolvieron las otras.
- **MVC / MVVM**: patrones de presentación; aclaran la confusión frecuente con las arquitecturas de aplicación.

### Ampliación del catálogo (candidatas añadidas después de completar las 11 originales)
- **Event Sourcing**: el estado como secuencia de eventos; continuación natural de CQRS y Event-Driven, que lo mencionan y lo dejan fuera a propósito.
- **Screaming Architecture**: que la estructura de carpetas "grite" el negocio y no el framework.
- **Serverless / FaaS**: funciones por evento, sin servidor propio; otros costes (arranque en frío, estado externo).
- **Microkernel / Plugins**: un núcleo pequeño más extensiones.
- **Pipes & Filters**: etapas encadenadas (ETL, pipelines de datos).
- **Frontend** (Atomic Design, Container-Presentational, Micro-frontends): la rama que el catálogo original no cubre.

## Orden recomendado

1. ~~**Onion**~~ (hecha: cierra la familia de la regla de dependencia).
2. ~~**Vertical Slice**~~ (hecha): la comparativa con Clean y Hexagonal tiene mucho valor.
3. ~~**DDD táctico**~~ (hecha): completa lo que el sample de Clean solo insinúa con la entidad y sus reglas.
4. ~~**Modular Monolith**~~ (hecha): puente hacia microservicios.
5. El resto según necesidad.
