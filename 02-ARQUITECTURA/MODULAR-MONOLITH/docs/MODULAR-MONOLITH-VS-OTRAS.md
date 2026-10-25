# Modular Monolith vs Layered / Clean / Vertical Slice / Microservicios

Todas responden a preguntas distintas. El monolito modular responde a **"¿cómo divido el sistema grande en partes con fronteras reales sin repartirlo por la red?"**. Las demás responden, sobre todo, a "¿cómo organizo el interior?" o "¿cómo lo despliego distribuido?".

> Fuentes: [Grzybek – Modular Monolith with DDD](https://github.com/kgrzybek/modular-monolith-with-ddd) y [Microsoft – Architecture styles](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/). Ejemplos: [`MODULAR-MONOLITH/sample`](../sample), [`CLEAN/sample`](../../CLEAN/sample), [`VERTICAL-SLICE/sample`](../../VERTICAL-SLICE/sample), [`DDD/sample`](../../DDD/sample).

---

## 1. Dos ejes distintos

| Eje | Pregunta | Opciones |
|---|---|---|
| **Cómo se divide el sistema** (macro) | ¿En qué partes y con qué fronteras? | Capas globales · **Módulos** · Microservicios |
| **Cómo se organiza cada parte** (micro) | ¿Qué hay dentro de una parte? | Capas · Clean/Hexagonal/Onion · Vertical Slice · DDD |
| **Cómo se despliega** | ¿Un proceso o varios? | **Monolito** · Distribuido |

El monolito modular está en el eje **macro** (división por capacidad de negocio) y el despliegue es **un solo proceso**. Dentro de cada módulo puedes usar lo que quieras.

## 2. Frente a las demás

| | Layered / N-Tier | Clean / Hexagonal / Onion | Vertical Slice | **Modular Monolith** | Microservicios |
|---|---|---|---|---|---|
| Se corta por | Capa técnica | Capa de dependencias | Funcionalidad | **Capacidad de negocio** | Capacidad de negocio |
| Protege | Poco | El dominio de la infraestructura | La localidad del cambio | **Un área de otra** | Un servicio de otro |
| Fronteras impuestas por | Convención | Referencias + tests | Carpetas + tests | **Proyectos + `internal` + tests** | Red |
| Despliegue | 1 | 1 | 1 | **1** | N |
| Datos | Compartidos | Compartidos | Compartidos | **Por módulo** | Por servicio |
| Complejidad operativa | Baja | Baja | Baja | **Baja** | Alta |

Microsoft describe N-tier como una arquitectura donde *"horizontal layering can make it difficult to introduce changes without affecting multiple parts of the application"*: es el problema que el corte vertical por módulos ataca.

## 3. Cómo se combina

**Módulo = bounded context; dentro, lo que necesites.** El sample deja el interior de cada módulo deliberadamente mínimo, pero cada uno puede tener su estructura:

| Módulo | Interior recomendado | Por qué |
|---|---|---|
| Catalog (CRUD + una regla de stock) | Vertical Slice o un proyecto simple | Poca lógica |
| Ordering (reglas de negocio ricas) | Clean/Onion + DDD táctico | Dominio que cambiará |
| Notifications (reacciona a eventos) | Handlers simples | Es casi infraestructura |

Grzybek lo hace así: *"Each Module has Clean Architecture"*. Es decir, **no hay que elegir** entre Clean y módulos: los módulos son la división de primer nivel y Clean se aplica dentro de cada uno.

Estructura combinada:

```
src/Modules/Ordering/
├─ Ordering.Contracts/            ← API pública + eventos
├─ Ordering.Domain/               ← DDD táctico (internal)
├─ Ordering.Application/          ← casos de uso (internal)
├─ Ordering.Infrastructure/       ← EF, esquema "ordering" (internal)
└─ (entry point: AddOrderingModule)
```

## 4. Cuándo usar cada cosa

| Tu situación | Haz esto |
|---|---|
| App pequeña, un equipo, dominio simple | Un solo proyecto ordenado; **no** módulos todavía |
| Varias áreas de negocio, un equipo o varios, un despliegue | **Monolito modular** |
| Un área concreta tiene necesidades de escala o despliegue propios | Extrae **ese** módulo a un servicio; el resto sigue en el monolito |
| Muchos equipos autónomos con ciclos de release independientes | Microservicios (con DevOps maduro) |
| No sabes aún dónde están las fronteras | Monolito simple + DDD estratégico primero; las fronteras equivocadas son caras en ambos mundos |

### No lo uses (o hazlo ligero) cuando
- Es un CRUD con una sola área: los módulos son burocracia.
- No tienes ni idea de cuáles son las capacidades de negocio: partirás mal y cambiar fronteras duele.
- El equipo no está dispuesto a mantener las reglas (los tests de frontera hay que cuidarlos).

## 5. Monolito modular vs microservicios

| | Monolito modular | Microservicios |
|---|---|---|
| Llamada entre partes | En proceso (rápida, no falla por red) | Red (latencia, fallos parciales) |
| Consistencia | Misma base (aunque esquemas separados) | Eventual, sagas |
| Despliegue independiente | No: se despliega todo junto | Sí |
| Escalado independiente | No (se escala todo el proceso) | Sí |
| Observabilidad | Un proceso | Trazas distribuidas |
| Coste de equipo | Bajo | Requiere *"mature development and DevOps practices"* (Microsoft) |
| Reversibilidad | Alta | Baja |

Idea práctica: **empieza modular; extrae cuando un dolor concreto lo justifique.** Si respetaste las fronteras, extraer un módulo es mover un proyecto y cambiar una implementación de interfaz por un cliente remoto.

## 6. Qué muestra cada sample

| Aspecto | CLEAN | VERTICAL SLICE | DDD | **MODULAR MONOLITH** |
|---|---|---|---|---|
| Unidad de división | Capa | Funcionalidad | Agregado | **Módulo (capacidad)** |
| Qué es público | Casi todo | Endpoints | Agregados | **Un `XModule` + `Contracts`** |
| Reglas verificadas | Dependencias entre capas | Aislamiento entre slices | Reglas tácticas | **Fronteras entre módulos, sin ciclos** |
| Comunicación entre partes | Llamadas directas | Ninguna entre slices | Eventos de dominio internos | **Contrato público + eventos de integración** |
| Datos | Un repositorio | Un `DbContext` | Un repositorio por raíz | **Un almacén por módulo** |

## 7. Errores al comparar

- "Monolito modular = monolito con carpetas": no. Sin proyectos separados y `internal`, las fronteras no existen.
- "Es un paso obligatorio hacia microservicios": no. Es una arquitectura válida por sí sola.
- "Los módulos son capas": no. Un módulo contiene su propio negocio completo; las capas son una forma de organizar su interior.
- "Los eventos de dominio y los de integración son lo mismo": no. Los de dominio viven dentro de un módulo; los de integración cruzan la frontera y son un contrato público (por eso viven en `Contracts`).
- "Con módulos ya no necesito DDD": DDD estratégico ayuda a **encontrar** los módulos; el táctico organiza su interior.
