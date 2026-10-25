# Onion vs Clean vs Hexagonal

Tres arquitecturas, una misma intuición: **el negocio al centro, lo técnico afuera, dependencias hacia adentro**. Este documento explica en qué se diferencian de verdad y cuándo conviene cada una.

> Fuentes: [Hexagonal – Cockburn, 2005](https://alistair.cockburn.us/hexagonal-architecture/), [Onion – Palermo, 2008](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-1/), [Clean – Martin, 2012](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html).
> Ejemplos: [`CLEAN/sample`](../../CLEAN/sample) (TodoApp), [`HEXAGONAL/sample`](../../HEXAGONAL/sample) (Library), [`ONION/sample`](../sample) (Shop).

---

## 1. Línea de tiempo y parentesco

| Año | Autor | Aporte |
|---|---|---|
| 2005 | Cockburn | **Hexagonal**: separar dentro/fuera con puertos y adaptadores |
| 2008 | Palermo | **Onion**: el modelo de dominio al centro; la base de datos es externa. Declara compartir la premisa con Hexagonal (externalizar la infraestructura con adaptadores) |
| 2012 | Martin | **Clean**: combina Hexagonal, Onion, DCI, BCE y otras en un único modelo de círculos concéntricos |

No son rivales: **Clean cita a Hexagonal y a Onion como antecesoras**, y Onion cita a Hexagonal como premisa compartida.

## 2. Qué enfatiza cada una

| | Hexagonal | Onion | Clean |
|---|---|---|---|
| **Pregunta central** | ¿Cómo conecto la app con el exterior? | ¿Cómo evito que el negocio dependa de datos/infraestructura? | ¿Cómo organizo todo para que las reglas no dependan de detalles? |
| **Idea clave** | Puertos y adaptadores, simetría | Modelo de dominio al centro; interfaces adentro, implementaciones afuera | Regla de Dependencia |
| **Estructura interna** | No la prescribe | Núcleo con anillos alrededor del modelo (el número varía) | Entities → Use Cases → Interface Adapters → Frameworks |
| **Vocabulario** | Puerto, adaptador, primario/secundario | Modelo, servicios de dominio, servicios de aplicación | Entities, Use Cases, boundaries |
| **Salto de capas** | No aplica | **Explícito**: una capa externa puede usar cualquier capa interna | Implícito: las dependencias solo apuntan adentro |
| **Sesgo** | Integración y testabilidad | Orientación a objetos / dominio | Casos de uso como centro de la aplicación |
| **Cuándo no** | CRUD sin variación de integraciones | "No es para sitios web pequeños" (Palermo) | CRUD simple, prototipos |

Resumen en una frase:
- **Hexagonal:** cómo se *conecta* la app con el mundo.
- **Onion:** el *dominio* es el centro y lo externo es reemplazable.
- **Clean:** cómo se *organiza* el código por dentro, centrado en casos de uso.

## 3. Equivalencias de conceptos

| Hexagonal | Onion | Clean |
|---|---|---|
| Núcleo (hexágono) | Núcleo: Modelo + Domain Services + Application Services | Entities + Use Cases |
| Dominio | Domain Model | Entities |
| Lógica entre entidades | Domain Services | Entities (reglas de negocio de la empresa) |
| Orquestación | Application Services | Use Cases |
| Puerto de salida | Interfaz definida en un anillo interno | Boundary / interfaz definida por el use case |
| Puerto de entrada | (implícito: se llama al servicio de aplicación) | Input boundary |
| Adaptadores | Anillo exterior (infraestructura, UI) | Interface Adapters + Frameworks & Drivers |

Advertencia: las equivalencias son aproximadas. Cada autor dibuja los límites algo distinto, y los anillos de Onion en cuatro niveles son una convención de las fuentes secundarias, no de Palermo.

## 4. La diferencia práctica más visible: dónde viven las interfaces de repositorio

| | Dónde se define `IRepository` | Samples |
|---|---|---|
| **Onion** (enseñanza habitual) | Anillo 2, **alrededor del modelo**, antes que los casos de uso | `Shop.Domain.Services` |
| **Clean** (enseñanza habitual) | En el anillo de **Use Cases** (Application) | `TodoApp.Application/Abstractions` |
| **Hexagonal** | En el núcleo, como **puerto de salida** | `Library.Core/Ports/Outbound` |

Consecuencia: en Onion, el repositorio es un **concepto del dominio** (muy DDD); en Clean es una **necesidad de un caso de uso**; en Hexagonal es una **conversación con un actor externo**. El código resultante es casi idéntico; cambia el modelo mental.

## 5. Qué muestran los tres samples

| Aspecto | CLEAN | HEXAGONAL | ONION |
|---|---|---|---|
| Proyectos | Domain, Application, Infrastructure, Api | Core + Adapters (Outbound, Rest, Cli) | Model, Domain.Services, Application, Infrastructure, Api |
| Lógica entre entidades | (no aparece) | (no aparece) | **Servicio de dominio** (`OrderPlacementService`) |
| Entrada a los casos de uso | Clase concreta | **Interfaz** (puerto de entrada) | Clase concreta |
| Puertas de entrada | Una (HTTP) | **Dos** (REST y CLI) | Una (HTTP) |
| Adaptadores por interfaz de salida | Uno | **Dos** para el repositorio | Uno |
| Value object | No | No | **Sí** (`Money`) |
| Test distintivo | Arquitectura por capas | Contrato por adaptador | Arquitectura por anillos |

Honestidad: nada de esto es exclusivo de una arquitectura. Podrías tener servicios de dominio en el sample Clean o dos adaptadores en el Onion. Los samples **resaltan** el énfasis de cada una.

## 6. Cuándo usar cada una

### Prioriza Onion cuando
- El dominio es rico y hay reglas que **involucran varias entidades** (servicios de dominio).
- Tu equipo trabaja con vocabulario DDD (value objects, repositorios como concepto del dominio).
- Quieres un dominio ignorante de la persistencia y una app longeva (el tipo de sistema para el que Palermo la propone).

### Prioriza Hexagonal cuando
- Tu dolor es **la integración**: varias puertas de entrada o varias tecnologías de salida intercambiables.
- Quieres tests de contrato por adaptador y mocks como ciudadanos de primera.

### Prioriza Clean cuando
- Quieres los **casos de uso** como eje, con una convención de capas que todo el equipo reconozca.
- Tu organización ya usa plantillas Domain/Application/Infrastructure.

### Ninguna cuando
- Es un CRUD simple, un prototipo, un script o un sitio pequeño: el costo no se paga.

## 7. Cuándo combinarlas (y cómo)

En la práctica, un proyecto .NET "serio" suele mezclar las tres sin nombrarlas:

1. **Anillos de Onion dentro del núcleo:** modelo → servicios de dominio → servicios de aplicación.
2. **Casos de uso como eje** (Clean): un servicio por intención, con DTOs en la frontera.
3. **Rigor hexagonal en la frontera:** distingue puertos de entrada y de salida, adaptadores primarios y secundarios, y añade tests de contrato cuando un puerto tenga más de una implementación.
4. **Verifica con tests de arquitectura** que las referencias solo apunten hacia el centro.

Estructura combinada de ejemplo:

```
src/
├─ MyApp.Domain.Model/        ← entidades y value objects            (Onion 1 / Clean Entities)
├─ MyApp.Domain.Services/     ← servicios de dominio + interfaces de repo (Onion 2)
├─ MyApp.Application/         ← casos de uso + puertos de entrada/salida (Clean Use Cases / Hexagonal)
├─ MyApp.Adapters.Out.*/      ← adaptadores secundarios
└─ MyApp.Adapters.In.*/       ← adaptadores primarios + composition roots
```

## 8. Guía rápida de decisión

| Tu situación | Haz esto |
|---|---|
| Dominio rico con reglas entre entidades | Onion (o Clean con servicios de dominio) |
| Varias puertas de entrada o tecnologías intercambiables | Hexagonal explícito + tests de contrato |
| Equipo que quiere una convención conocida y centrada en casos de uso | Clean |
| Dominio complejo **y** múltiples integraciones | Combinadas (sección 7) |
| CRUD simple / prototipo / sitio pequeño | Ninguna; un proyecto ordenado basta |
| Equipo junior | Empieza por Clean u Onion (más guía interna) y suma el rigor de puertos cuando aparezca la segunda integración |

## 9. Errores al comparar

- "Onion y Clean son lo mismo": son muy parecidas, pero Onion enfatiza el dominio y los anillos alrededor del modelo; Clean, los casos de uso. El resultado práctico es casi idéntico, no la historia ni el énfasis.
- "Más anillos = mejor": no. Lo importante es que las dependencias apunten al centro y que el costo se justifique.
- "Una excluye a las otras": falso; se combinan constantemente.
- "Hay un número fijo de anillos": Palermo dice que varía.
