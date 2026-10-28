# Comparativa global: las 18 arquitecturas del catálogo

Cada carpeta de este repositorio documenta **una** arquitectura con el mismo formato (guía, proyecto de referencia con tests, portal, comparativa). Este documento las pone **todas juntas**: qué pregunta responde cada una, cuándo elegirla, cómo se combinan y en qué orden conviene leerlas.

> Todo lo que sigue sale de los kits de este repo (`architectures.md` y el README de cada carpeta) y de lo que se verificó al construirlos. No agrega fuentes nuevas. Donde un dato depende de algo que **no re-corrí hoy**, está dicho en la sección 8.

---

## 1. La idea que ordena todo: no compiten, responden preguntas distintas

Elegir "una arquitectura" suele ser mezclar preguntas que no tienen nada que ver. Las 18 carpetas se reparten en seis ejes:

| Eje | Pregunta que responde | Kits |
|---|---|---|
| **Dirección de las dependencias** | ¿Quién puede conocer a quién dentro del código? | Layered, Onion, Hexagonal, Clean |
| **Organización por funcionalidad** | ¿Se agrupa el código por tipo técnico o por lo que el sistema hace? | Vertical Slice, Screaming, Modular Monolith, Clean + Screaming |
| **Modelado del dominio y los datos** | ¿Cómo se expresan las reglas y cómo se guarda el estado? | DDD táctico, CQRS, Event Sourcing |
| **Distribución y ejecución** | ¿Dónde y cómo corren las piezas, y cómo se hablan? | Microservicios, Event-Driven, Serverless |
| **Extensibilidad y flujo** | ¿Cómo se agregan variantes o se encadenan pasos sin tocar lo existente? | Microkernel, Pipes & Filters |
| **Presentación** | ¿Cómo se estructura la interfaz y quién la entrega? | MVC / MVVM, Frontend (Atomic Design + Container/Presentational + Micro-frontends) |

Consecuencia práctica: un sistema real **usa una opción de varios ejes a la vez**. Por ejemplo: Hexagonal (dependencias) + Screaming (organización) + DDD táctico (dominio) + Event-Driven (distribución). Lo que no tiene sentido es elegir dos del mismo eje que se contradicen sin saberlo (por ejemplo, Layered y Hexagonal como regla de dependencias para el mismo código).

## 2. Tabla maestra

"Ejemplo" es el dominio del proyecto de referencia de cada carpeta.

| # | Arquitectura | Eje | Ejemplo | Lo que ganás | Lo que pagás |
|---|---|---|---|---|---|
| 10 | **Layered / N-Tier** | Dependencias | Préstamos de biblioteca | Es simple y todos la conocen | Las reglas tienden a filtrarse hacia los datos; el negocio depende de la capa de abajo |
| 3 | **Onion** | Dependencias | Tienda | Dominio al centro, sin depender de infraestructura | Más proyectos y mapeos que Layered |
| 2 | **Hexagonal** | Dependencias | Biblioteca con REST y CLI | El núcleo habla con el mundo por puertos; se cambian adaptadores sin tocarlo | Más interfaces; sin disciplina se vuelve ceremonia |
| 1 | **Clean** | Dependencias | Todo-list | Regla de dependencia explícita, casos de uso aislados | La más ceremoniosa de la familia |
| 4 | **Vertical Slice** | Funcionalidad | Mesa de ayuda | Cada funcionalidad es autocontenida y cambia en un solo lugar | Riesgo de duplicar reglas entre slices |
| 13 | **Screaming** | Funcionalidad | Agenda de clínica | La carpeta dice qué hace el sistema, no qué framework usa | Es una regla de estructura, no una garantía de diseño |
| 18 | **Clean + Screaming** | Funcionalidad + dependencias | La clínica reconstruida | Capacidades de negocio arriba y círculos de Clean adentro | Muchos proyectos por capacidad |
| 6 | **Modular Monolith** | Funcionalidad / despliegue | Catálogo, pedidos, notificaciones | Fronteras estrictas con un solo despliegue | Hay que hacer cumplir las fronteras con tests |
| 5 | **DDD táctico** | Dominio | Reservas de hotel | Agregados y value objects que protegen invariantes | Es un modo de pensar el modelo, y exige conocer el negocio |
| 7 | **CQRS** | Dominio | Cuentas bancarias | Lecturas y escrituras optimizadas por separado | Consistencia eventual entre el modelo de escritura y el de lectura |
| 12 | **Event Sourcing** | Dominio / persistencia | Libro mayor de depósito | Historia completa, viaje en el tiempo, auditoría gratis | Versionado de eventos, concurrencia, reconstrucción |
| 8 | **Microservicios** | Distribución | Catálogo, pedidos, gateway | Equipos y despliegues independientes | Fallos de red, datos distribuidos, costo operativo |
| 9 | **Event-Driven** | Distribución | Pedidos, inventario, notificaciones | Piezas desacopladas en el tiempo | Duplicados, orden, consistencia eventual, depuración |
| 14 | **Serverless / FaaS** | Distribución / ejecución | Procesador de pedidos | No operás servidores; elasticidad | Dependencia del proveedor, arranque en frío, estado externo |
| 15 | **Microkernel / Plugins** | Extensibilidad | Cotizador de envíos | Variantes nuevas sin recompilar el núcleo | Contrato estable y versionado; no es un sandbox |
| 16 | **Pipes & Filters** | Flujo | Importador de ventas | Pasos independientes, reordenables y de memoria constante | Probar la cadena entera; sin estado compartido fácil |
| 11 | **MVC / MVVM** | Presentación | Carrito de compras | Separa interfaz, lógica de presentación y modelo | Se confunde con arquitecturas de aplicación |
| 17 | **Frontend** | Presentación / entrega | Tienda con catálogo y carrito | Vocabulario común, componentes puros, equipos que entregan por separado | Dependencias duplicadas y contrato de integración |

## 3. ¿Qué miro según mi problema?

| Mi problema | Empezá por | Después |
|---|---|---|
| "No sé cómo organizar el código de una API nueva" | **Screaming** y **Layered** (para entender qué resuelven los demás) | Clean o Hexagonal si el dominio es rico; Vertical Slice si no |
| "La lógica de negocio está pegada a la base y al framework" | **Hexagonal** u **Onion** | Clean + Screaming si además hay varias capacidades |
| "Cada cambio toca cinco carpetas" | **Vertical Slice** | Modular Monolith |
| "Las reglas del negocio son complejas y se violan" | **DDD táctico** | CQRS si lecturas y escrituras divergen |
| "Necesito auditoría completa o reconstruir el estado de cualquier fecha" | **Event Sourcing** | CQRS (las proyecciones) |
| "Un equipo pisa al otro en el mismo despliegue" | **Modular Monolith** | Microservicios solo si hace falta desplegar por separado |
| "Necesito que varias piezas reaccionen a lo que pasó" | **Event-Driven** | Outbox e idempotencia (ver sección 5) |
| "Tengo cargas irregulares disparadas por eventos" | **Serverless / FaaS** | Event-Driven |
| "Los clientes piden variantes que se agregan seguido" | **Microkernel / Plugins** | Hexagonal para el núcleo |
| "Proceso archivos o flujos en etapas" | **Pipes & Filters** | Serverless o colas si se distribuye |
| "No sé si lo mío es MVC, MVVM o Clean" | **MVC / MVVM** (no son lo mismo que las arquitecturas de aplicación) | Layered y Clean |
| "El frontend crece y varios equipos lo tocan" | **Frontend** (Atomic Design, Container/Presentational) | Micro-frontends solo si hay equipos que necesitan entregar por separado |

## 4. Una regla de oro para no sobreconstruir

Empezá por lo **más simple que no te haga daño**:

1. Un solo equipo y un solo despliegue → un **monolito modular** con carpetas por funcionalidad (Screaming / Modular Monolith). Casi nunca necesitás más al principio.
2. Si el núcleo del negocio es rico, agregá **Hexagonal / Clean** y **DDD táctico** *dentro* de cada módulo.
3. Recién cuando haya un problema medible (despliegues que se pisan, escalado distinto, equipos autónomos), pasá a **Microservicios, Event-Driven o Serverless**.
4. **CQRS** y **Event Sourcing** son decisiones de dominio y datos con costo propio: no las agregues "por si acaso".

Cada kit de distribución (Microservicios, Event-Driven, Serverless, Frontend con micro-frontends) incluye su propia sección "cuándo no", y todas coinciden en lo mismo: el costo operativo es real y los beneficios dependen de que haya un problema que lo justifique.

## 5. Las combinaciones que el catálogo ya probó

| Combinación | Dónde verla |
|---|---|
| Capacidades de negocio arriba + círculos de Clean adentro | `CLEAN-SCREAMING/` (la clínica reconstruida, 70 tests que revisan ambas ideas) |
| Núcleo hexagonal + puerto cuyo adaptador es "los plugins descubiertos" | `MICROKERNEL/docs/MICROKERNEL-VS-OTRAS.md` |
| Monolito modular → microservicios | `MODULAR-MONOLITH/` y `MICROSERVICIOS/` |
| CQRS + Event Sourcing + Event-Driven (las proyecciones se alimentan de eventos) | `CQRS/`, `EVENT-SOURCING/`, `EVENT-DRIVEN/` |
| Cadena de funciones unidas por colas = pipeline distribuido | `SERVERLESS/` + `PIPES-AND-FILTERS/` |
| El shell de micro-frontends es un pequeño microkernel | `FRONTEND/docs/FRONTEND-VS-OTRAS.md` |
| MVC/MVVM dentro de una arquitectura de capas | `MVC-MVVM/` + `LAYERED/` |

**Un tema que aparece en tres kits distintos: el *outbox*.** Event-Driven lo implementa (outbox transaccional, consumidores idempotentes), y Serverless lo **muestra como límite abierto**: si una función se cuelga entre "escribí" y "anuncié", el reintento no anuncia nada y el evento se pierde (queda fijado en un test). Es el mismo problema visto desde dos lados: cuando un efecto y un mensaje son dos escrituras separadas, algo puede quedar a medias.

## 6. Lo que todos los kits comparten

- **Reglas de arquitectura ejecutables.** En cada carpeta, la regla de dependencias o de independencia es un test que falla si alguien la rompe (referencias de ensamblados, escaneo de código fuente, tipos y campos por reflexión). Un diagrama se desactualiza; un test rojo no.
- **Pruebas de mutación.** Cada kit aplica cambios que rompen una regla a propósito, en copias temporales, para comprobar que algún test los detecta. Una regla que ninguna mutación logra romper no está protegiendo nada.
- **Honestidad sobre los límites.** Cada guía termina con lo que el sample **no** demuestra (no es una nube, no es un sandbox, el broker está en memoria, etc.).
- **Un proyecto pequeño y real por idea**, con tests, para poder tocar y romper.

## 7. En qué orden leerlos

Una ruta posible, de lo que más ayuda a entender el resto a lo más específico:

1. **Layered** — qué problema resolvieron todas las demás.
2. **Clean, Hexagonal, Onion** — la regla de dependencia (leé las tres comparativas).
3. **Vertical Slice y Screaming** — organizar por funcionalidad; **Clean + Screaming** para unir ambas ideas.
4. **DDD táctico** — lo que va *dentro* del núcleo.
5. **Modular Monolith** — fronteras en un solo despliegue.
6. **CQRS → Event Sourcing → Event-Driven** — de separar lecturas y escrituras a reaccionar a eventos.
7. **Microservicios → Serverless** — cuando hay que distribuir.
8. **Microkernel y Pipes & Filters** — extensibilidad y flujo, en cualquier momento después de Hexagonal.
9. **MVC/MVVM → Frontend** — la rama de presentación, independiente de las anteriores.

## 8. Estado de verificación (sin maquillaje)

Los conteos de tests salen del README de cada kit. **En esta sesión re-corrí únicamente** Microkernel, Pipes & Filters, Serverless y Frontend. No volví a ejecutar los demás, así que su cifra es la que quedó escrita cuando se construyeron.

| Kit | Tests (según su README) | Estado |
|---|---|---|
| Clean | 18 | Verde al construirlo |
| Hexagonal | 28 | Verde al construirlo |
| Onion | 28 | Verde al construirlo |
| Vertical Slice | 36 | Verde al construirlo |
| DDD | 69 | **Pendiente de re-verificar**: último verde 69/69; después de una recompilación, 62/69 por un bloqueo de Windows App Control |
| Modular Monolith | 47 | **Pendiente de re-verificar**: 47/47 antes de recompilar; después, bloqueado por App Control |
| CQRS | 56 | Verde al construirlo |
| Microservicios | 49 | Verde al construirlo |
| Event-Driven | 55 | Verde al construirlo |
| Layered | 41 | Verde al construirlo |
| MVC / MVVM | 66 (su README dice 62 pasaron) | **Pendiente**: 4 pruebas corregidas sin volver a ejecutar, y sin mutaciones |
| Event Sourcing | 91 | Verde al construirlo |
| Screaming | 42 | Verde al construirlo |
| Clean + Screaming | 70 | Verde al construirlo |
| Microkernel | 34 | **Re-corrido en esta sesión**: 34/34, 10 mutaciones atrapadas |
| Pipes & Filters | 71 | **Re-corrido en esta sesión**: 71/71 (6 corridas), 12 mutaciones atrapadas |
| Serverless | 76 | **Re-corrido en esta sesión**: 76/76 (6 corridas), 13 mutaciones atrapadas |
| Frontend | 116 | **Re-corrido en esta sesión**: 116/116 (6 corridas), 16 mutaciones atrapadas, y probado en Chrome 155 |

Los tres pendientes (DDD, Modular Monolith, MVC/MVVM) necesitan que se permita la ejecución de los binarios compilados desde `C:\git` en la política de App Control, o correrlos en otra máquina.

## 9. Lo que este catálogo no resuelve

- **Seguridad** (autenticación, autorización, secretos): los samples no la modelan.
- **Observabilidad en producción** (trazas, métricas, alertas).
- **Despliegue real**: los samples corren en una máquina; no hay pipelines ni infraestructura como código.
- **Rendimiento y carga**: no se midió ninguna latencia, escalado ni costo.
- **Elegir por vos.** Esta comparativa te da preguntas y costos; la decisión depende de tu equipo, tu dominio y tu etapa.
