# Arquitectura Hexagonal vs Clean Architecture

Dos preguntas honestas antes de empezar: **¿son rivales?** No. **¿Cuál elijo?** Depende de qué problema te duele. Este documento te da criterio.

> Fuentes: [Hexagonal – Cockburn, 2005](https://alistair.cockburn.us/hexagonal-architecture/) y [The Clean Architecture – R. C. Martin, 2012](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html). Ejemplos de código: [`../../CLEAN/sample`](../../CLEAN/sample) (TodoApp) y [`../sample`](../sample) (Library).

---

## 1. La relación real

Martin escribe su artículo diciendo que Hexagonal, Onion, DCI, BCE y otras comparten un objetivo (separar responsabilidades en capas, con al menos una para reglas de negocio y otra para interfaces) y los **combina en un solo modelo**. Es decir: **Clean cita a Hexagonal como uno de sus antecesores**. Son parientes, no opuestos.

## 2. Qué enfatiza cada una

| | Hexagonal (2005) | Clean (2012) |
|---|---|---|
| **Pregunta que responde** | ¿Cómo conecto mi app con el mundo exterior sin acoplarme a él? | ¿Cómo organizo todo el código para que las reglas no dependan de detalles? |
| **Idea clave** | Dentro vs fuera. Puertos y adaptadores | Regla de Dependencia: dependencias solo hacia adentro |
| **Prescribe la estructura interna** | **No.** Solo define la frontera | **Sí:** Entities → Use Cases → Interface Adapters → Frameworks |
| **Vocabulario** | Puerto, adaptador, primario/driving, secundario/driven | Entities, Use Cases, Interface Adapters, Frameworks & Drivers, boundaries |
| **Simetría** | Central: todos los actores externos son iguales | No es el foco; el eje es radial (centro → afuera) |
| **Forma del dibujo** | Hexágono (solo ilustrativo) | Círculos concéntricos (esquemáticos) |
| **Cómo cruzan los datos** | El puerto es la API de la conversación | Estructuras simples y aisladas (DTOs) que cruzan cada frontera |
| **Origen** | Problema de lógica filtrada a UI y atada a la BD | Síntesis de varias arquitecturas |

Resumen en una frase:
- **Hexagonal** dice *cómo se conecta la app con el exterior* (puertos y adaptadores).
- **Clean** dice *cómo se organiza la app por dentro* y en qué sentido apuntan las dependencias.

## 3. Cómo se traducen una a la otra

| Concepto Clean | Concepto Hexagonal |
|---|---|
| Entities + Use Cases | El hexágono (núcleo) |
| Interfaz que el use case define y el exterior implementa (boundary / puerto de salida) | Puerto secundario (driven) |
| Interfaz/clase de entrada del use case (input boundary) | Puerto primario (driving) |
| Interface Adapters + Frameworks & Drivers | Adaptadores (primarios y secundarios) |
| Regla de Dependencia | "El núcleo no conoce a los adaptadores" |

## 4. Qué muestran los dos samples (y qué no)

Ambos samples cumplen la misma regla de dependencia y se parecen mucho. Las diferencias reales:

| Aspecto | `CLEAN/sample` (TodoApp) | `HEXAGONAL/sample` (Library) |
|---|---|---|
| Organización | 4 capas: Domain, Application, Infrastructure, Api | Núcleo único + proyectos de adaptadores |
| Entrada al caso de uso | Clase concreta (`CreateTodo`) | **Interfaz** (puerto de entrada `IAddBook`) implementada por un servicio |
| Puertas de entrada | Una (HTTP) | **Dos** (REST y CLI) sobre los mismos puertos |
| Adaptadores por puerto de salida | Uno (en memoria) | **Dos** para `IBookRepository` (memoria y JSON) |
| Tests distintivos | Arquitectura por capas | Arquitectura + **test de contrato** por adaptador |

Honestidad: nada de esto es exclusivo de una u otra. Podrías añadir interfaces de entrada y un segundo adaptador en el sample Clean y sería igual de válido. La diferencia es de **énfasis y vocabulario**, no de capacidades.

## 5. Cuándo usar cada una

### Prioriza vocabulario Hexagonal cuando
- Tu dolor es **la integración**: varias "puertas" de entrada (API, CLI, colas, jobs, tests) o varias tecnologías de salida intercambiables.
- Quieres poder probar la app **sin UI ni BD** y tener mocks/fakes como ciudadanos de primera.
- Hay equipos que trabajan en paralelo contra puertos acordados antes de elegir tecnología.
- Necesitas adaptadores alternativos para el mismo puerto (por ejemplo, otro proveedor de pagos).

### Prioriza vocabulario Clean cuando
- Tu dolor es **la estructura interna**: el dominio crece y necesitas guía sobre qué va en entidades vs casos de uso.
- Quieres una convención clara que todo el equipo reconozca (capas y reglas de dependencia).
- La app tiene reglas de negocio ricas y de larga vida.
- Tu equipo ya usa plantillas/solutions con Domain/Application/Infrastructure.

### Ninguna cuando
- Es un CRUD sin reglas, un prototipo desechable o un script. El costo (más proyectos, más indirección) no se paga.

## 6. Cuándo combinarlas (y por qué casi siempre se hace)

En la práctica, la mayoría de proyectos .NET "Clean" ya son híbridos: **Clean para el interior, puertos y adaptadores en la frontera**. Combinar significa:

1. **Dentro del hexágono**, aplica la organización de Clean: entidades con reglas, casos de uso que orquestan, DTOs en la frontera.
2. **En la frontera**, aplica el rigor hexagonal: puertos de entrada (interfaces por intención) y de salida (definidos por el núcleo, nunca por la tecnología).
3. **Distingue siempre primarios de secundarios**: cada adaptador sabe de qué lado está.
4. **Verifica con tests**: arquitectura (la regla de dependencia) + contrato (cada adaptador cumple su puerto).

Estructura combinada recomendada:

```
src/
├─ MyApp.Domain/              ← Entities (Clean)
├─ MyApp.Application/         ← Use Cases (Clean) + Ports in/out (Hexagonal)
│    ├─ Ports/In/             ← puertos primarios
│    └─ Ports/Out/            ← puertos secundarios
├─ MyApp.Adapters.Out.*/      ← adaptadores secundarios (EF, email, ...)
└─ MyApp.Adapters.In.*/       ← adaptadores primarios (Api, Cli, Worker...) + composition roots
```

## 7. Guía rápida de decisión

| Si tu situación es... | Haz esto |
|---|---|
| Un solo cliente HTTP, una BD, dominio con reglas | Clean (4 capas). Suma puertos solo donde dudes de la tecnología |
| Varios puntos de entrada o tecnologías intercambiables | Hexagonal explícito: puertos de entrada y de salida, tests de contrato |
| Dominio complejo **y** múltiples integraciones | Combinadas (sección 6) |
| CRUD simple / prototipo | Ninguna; un proyecto bien ordenado basta |
| Equipo junior aprendiendo | Empieza con Clean (más guía interna) y añade el rigor de puertos cuando aparezca la segunda integración |

## 8. Errores al comparar

- "Hexagonal es solo para microservicios / Clean solo para monolitos": falso, ambas son independientes del despliegue.
- "Son incompatibles": falso, Clean absorbe la idea de Hexagonal.
- "Más capas = mejor arquitectura": falso. Lo que importa es que las dependencias apunten hacia el núcleo y que el costo se justifique.
- "El hexágono tiene seis puertos": es solo un dibujo. Cockburn sugiere 2 a 4 como típico.
