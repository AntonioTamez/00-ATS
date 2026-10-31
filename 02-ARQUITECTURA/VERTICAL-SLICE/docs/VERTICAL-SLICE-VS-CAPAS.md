# Vertical Slice vs Clean / Hexagonal / Onion

Las tres arquitecturas anteriores comparten una intuición (negocio al centro, dependencias hacia adentro) y se **organizan por capas**. Vertical Slice cambia el eje: se organiza por **funcionalidad**. Este documento explica qué significa eso en la práctica y cuándo elegir cada enfoque.

> Fuentes: [Vertical Slice – Bogard](https://www.jimmybogard.com/vertical-slice-architecture/), [Milan Jovanović](https://milanjovanovic.tech/blog/when-to-choose-vertical-slice-architecture), y las de cada arquitectura en sus carpetas.
> Ejemplos: [`CLEAN/sample`](../../CLEAN/sample), [`HEXAGONAL/sample`](../../HEXAGONAL/sample), [`ONION/sample`](../../ONION/sample), [`VERTICAL-SLICE/sample`](../sample).

---

## 1. La diferencia de fondo: el eje de organización

| | Clean · Onion · Hexagonal | Vertical Slice |
|---|---|---|
| **Eje de organización** | Por **rol técnico** (dominio, aplicación, infraestructura, adaptadores) | Por **funcionalidad / petición** |
| **Regla estructural** | Las dependencias apuntan hacia el centro | Minimizar acoplamiento **entre** slices, maximizarlo **dentro** |
| **Abstracciones** | Obligatorias en las fronteras (puertos, repositorios, casos de uso) | Solo cuando una slice las necesita; sin capas obligatorias |
| **Acceso a datos** | Detrás de una interfaz definida por el núcleo | El handler suele usar el `DbContext` directamente |
| **Enfoque de dominio** | Uniforme en toda la app | **Cada slice elige** (transaction script, entidad rica, proyección directa) |
| **Qué toca una funcionalidad nueva** | Una pieza en varias capas | Una carpeta nueva |
| **Proyectos .NET** | Varios, para que el compilador imponga la dependencia | Normalmente uno; las fronteras las verifican tests |
| **Quién impone las reglas** | El compilador (referencias entre proyectos) + tests | Convenciones + tests (el compilador no ayuda dentro de un proyecto) |

Frase de Bogard que lo resume: *"Minimize coupling between slices, and maximize coupling in a slice."* Y su crítica a las capas: la cadena controller → servicio → repositorio impone abstracciones que rara vez ayudan en la mayoría de las peticiones.

## 2. No es "lo contrario": ejes distintos

Clean/Hexagonal/Onion responden **cómo protejo el negocio de la tecnología**. Vertical Slice responde **cómo organizo el código para que los cambios sean locales**. Por eso se pueden combinar (sección 6).

## 3. Qué muestran los cuatro samples

| Aspecto | CLEAN | HEXAGONAL | ONION | VERTICAL SLICE |
|---|---|---|---|---|
| Proyectos de producción | 4 | 4 (núcleo + 3 adaptadores) | 5 (anillos) | **1** |
| Para añadir una funcionalidad | Dominio → Application → Api | Dominio → puerto → servicio → adaptadores | Modelo → servicio de dominio → aplicación → Api | **Una carpeta** + 2 líneas de registro |
| Acceso a datos | `ITodoRepository` | `IBookRepository` (2 adaptadores) | `IOrderRepository` (en el anillo 2) | `AppDbContext` directo |
| Lecturas | Igual que escrituras | Igual | Igual | **Proyección directa**, sin entidad |
| Reglas de negocio | Entidad | Entidad | Entidad + servicio de dominio | Entidad compartida (`Ticket`) |
| Test distintivo | Arquitectura por capas | Contrato por adaptador | Arquitectura por anillos | **Aislamiento entre slices** |
| Cómo se verifica la frontera | Referencias de ensamblados | Referencias de ensamblados | Referencias de ensamblados | Texto + **llamadas IL** (REA) |

Honestidad: nada de esto es exclusivo. Puedes tener `DbContext` directo en una app Clean, o proyecciones directas para las lecturas. La diferencia es qué es **obligatorio** y qué es **opcional**.

## 4. Fortalezas y costos de cada eje

| | Por capas (Clean/Onion/Hexagonal) | Vertical Slice |
|---|---|---|
| **Fortalezas** | Fronteras impuestas por el compilador · dominio protegido y reutilizable · muy testeable en aislamiento · reglas compartidas en un solo sitio | Cambios locales · menos ceremonia · cada funcionalidad evoluciona a su ritmo · añadir no rompe lo existente |
| **Costos** | Más archivos e indirección · una funcionalidad toca varias capas · abstracciones que a veces no aportan | Posible duplicación entre slices · exige criterio para refactorizar · sin fronteras de compilador dentro de un proyecto |
| **Riesgo típico** | Sobre-ingeniería en CRUD simple | Acoplamiento silencioso entre slices o `Common` convertido en una capa |

## 5. Cuándo usar cada una

### Prioriza Vertical Slice cuando
- Las funcionalidades son en su mayoría **independientes** y hay mucho CRUD o consultas distintas entre sí.
- Quieres **iterar rápido** con poca ceremonia.
- Varias personas trabajan en **funcionalidades en paralelo** y quieres evitar conflictos en capas compartidas.
- El equipo sabe **reconocer code smells y refactorizar** (Bogard lo pide explícitamente).

### Prioriza capas (Clean / Onion / Hexagonal) cuando
- Hay **lógica de negocio compleja compartida** por muchas funcionalidades (dominio rico, DDD).
- Necesitas **fronteras garantizadas por el compilador**.
- El valor está en **aislar integraciones** (Hexagonal) o proteger un dominio longevo (Onion).

### Ninguna cuando
- Es un prototipo, un script o un CRUD mínimo: un proyecto ordenado basta.

## 6. Cuándo combinarlas (y cómo)

La combinación más común y útil: **organización vertical por fuera, rigor de capas solo donde la complejidad lo pide**.

1. **Slices como estructura principal.** Una carpeta por funcionalidad.
2. **Dominio compartido rico donde haga falta**, como `Ticket` en el sample: las reglas viven en la entidad y las slices que escriben la usan. Si el dominio crece, esa entidad puede evolucionar hacia Onion/DDD.
3. **Dentro de una slice compleja**, aplica puertos y adaptadores: una slice que integra un proveedor externo puede definir su propio puerto y adaptador sin imponer ese patrón a las demás.
4. **Slices dentro de módulos.** En un monolito modular, cada módulo tiene sus slices y las fronteras entre módulos sí se refuerzan con proyectos separados (tema del siguiente documento del catálogo).
5. **Tests de arquitectura** que protejan el eje elegido (aislamiento entre slices o dependencias entre capas).

Estructura combinada de ejemplo:

```
src/MyApp.Api/
├─ Common/                     ← entidades con reglas, DbContext, errores (poco y justificado)
└─ Features/
   ├─ OpenTicket/              ← transaction script + entidad compartida
   ├─ GetTicket/               ← proyección directa
   └─ ImportFromCrm/           ← slice compleja: define su propio puerto y adaptador
```

## 7. Guía rápida de decisión

| Tu situación | Haz esto |
|---|---|
| Muchas funcionalidades independientes, equipo con buen criterio de refactor | Vertical Slice |
| Dominio complejo con reglas compartidas entre muchas funcionalidades | Onion/Clean (o Vertical Slice con un dominio compartido rico) |
| Varias integraciones o tecnologías intercambiables | Hexagonal, o slices con puertos donde haga falta |
| Equipo junior aprendiendo | Empieza por Clean/Onion (más guía); pasa a slices cuando entiendan cuándo mover lógica al dominio |
| CRUD simple / prototipo | Ninguna; basta un proyecto ordenado |
| Quieres fronteras impuestas por el compilador | Capas en proyectos separados (o módulos) |

## 8. Errores al comparar

- "Vertical Slice es no tener arquitectura": falso. Tiene una regla clara (acoplamiento entre slices mínimo) y se verifica con tests.
- "Vertical Slice reemplaza a Clean": son ejes distintos; se combinan.
- "En slices no hay dominio": falso; Bogard propone elegir el enfoque por slice y refactorizar hacia un dominio rico cuando aparezcan los *smells*.
- "Duplicar código es siempre malo": aquí una pequeña duplicación entre slices suele ser preferible a acoplarlas, siempre que las **reglas** de negocio no se copien.
- "Sin repositorios no se puede testear": las slices se prueban contra un `DbContext` real en memoria, sin mocks.
