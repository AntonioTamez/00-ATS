# Clean + Screaming vs Clean solo, Screaming solo, Modular Monolith y Vertical Slice

La combinación no es una arquitectura nueva: es una forma concreta de aplicar **dos ideas ortogonales a la vez**. Este documento compara el mismo dominio (la clínica) organizado de varias maneras para que se vea qué se gana y qué se paga.

> Fuentes: [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) y [Screaming Architecture](https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html) (Robert C. Martin). Ejemplos: [`CLEAN-SCREAMING/sample`](../sample), [`SCREAMING/sample`](../../SCREAMING/sample), [`CLEAN/sample`](../../CLEAN/sample), [`MODULAR-MONOLITH/sample`](../../MODULAR-MONOLITH/sample).

---

## 1. Dos ejes independientes

| Eje | Pregunta | Opciones |
|---|---|---|
| **Qué grita la estructura** | ¿Qué ve quien abre la carpeta? | Roles técnicos · **Capacidades del negocio** |
| **Hacia dónde apuntan las dependencias** | ¿Quién puede conocer a quién? | Libre · Capas · **Hacia adentro (Clean)** |
| **Quién defiende las fronteras** | ¿Convención, test o compilador? | Convención · Test de texto · **Compilador** |

Combinando valores se obtienen los kits del catálogo:

| Kit | Grita | Dependencias | Fronteras defendidas por |
|---|---|---|---|
| [`LAYERED/`](../../LAYERED) | Roles técnicos | Hacia abajo | Compilador (proyectos por capa) + tests |
| [`CLEAN/`](../../CLEAN) | Roles técnicos (capas de Clean) | Hacia adentro | Compilador + tests |
| [`SCREAMING/`](../../SCREAMING) | **Negocio** | Una dirección entre capacidades | **Test de texto** (un solo proyecto) |
| [`MODULAR-MONOLITH/`](../../MODULAR-MONOLITH) | **Negocio** | Contratos públicos + eventos | Compilador (`internal`) + tests |
| **`CLEAN-SCREAMING/` (este)** | **Negocio** | **Hacia adentro, también entre capacidades** | **Compilador + tests** |

## 2. La misma clínica, tres maneras

| | Screaming solo (`SCREAMING`) | Clean + Screaming (este) | Modular Monolith (`MODULAR-MONOLITH`, otro dominio) |
|---|---|---|---|
| Proyectos en `src/` | 2 (el bueno y el malo de ejemplo) | **11** (3 por capacidad + kernel + host) | 8 |
| ¿El primer nivel grita el negocio? | Sí | Sí | Sí |
| ¿Quién impide que `Patients` use a `Billing`? | Un test que lee los `using` | **El compilador** (no hay referencia) | El compilador |
| Reglas de negocio puras (sin framework) | En archivos de caso de uso | **En proyectos de dominio sin framework** | En `Core` |
| Cómo se hablan las capacidades | Una interfaz del consumidor implementada por el vecino | **Puerto del consumidor + adaptador en el borde** | Contrato público (`Contracts`) + eventos |
| Probar un caso de uso sin las otras capacidades | Sí, con una interfaz | **Sí, con un fake del puerto** | Sí, con un fake del contrato |
| Coste | Mínimo | **Alto** | Medio |

## 3. Qué añade Clean a Screaming

Screaming dice "agrupa por capacidad" pero deja el interior libre. Con Clean dentro de cada capacidad:

- Las **reglas de negocio** viven en un proyecto sin ninguna referencia a frameworks (`*.Domain`): se pueden probar y reutilizar sin nada alrededor.
- Los **casos de uso** definen los **puertos** que necesitan; el framework web, el almacenamiento y las demás capacidades son adaptadores intercambiables.
- Cambiar la base de datos o el framework web afecta a `*.Adapters`, no al negocio.

En el kit de Screaming esto se comprobaba con tests de texto sobre los `using` y el IL. Aquí lo comprueba **el compilador**: un `*.Domain` que intentara usar ASP.NET Core o a otra capacidad **no compilaría** (no tiene la referencia). Los tests están para vigilar que nadie añada esas referencias.

## 4. Qué añade Screaming a Clean

Clean dice "dependencias hacia adentro" pero deja el primer nivel libre; el resultado habitual (el kit de CLEAN) es `Domain / Application / Infrastructure / Api`. Con Screaming:

- El primer nivel dice **qué hace el sistema**: `Patients / Appointments / Billing`.
- Cada capacidad crece o se retira como una unidad: añadir una área nueva es una carpeta nueva con sus tres círculos.
- El cambio típico ("la política de cancelación") toca **una** capacidad.

## 5. Lo que cuesta combinarlas

| Coste | Detalle |
|---|---|
| Proyectos | 3 por capacidad: la solución crece rápido |
| Ceremonia | DTOs de entrada y salida, puertos, adaptadores |
| Costuras entre capacidades | Cada relación necesita un puerto y un adaptador |
| Operaciones que cruzan capacidades | Pierden atomicidad (ver el límite del sample) |
| Disciplina | Hay que decidir en qué capacidad vive cada cosa y en qué círculo |

## 6. Cuándo elegir cada una

| Situación | Elección |
|---|---|
| CRUD o dominio pequeño | Solo Screaming (carpetas por capacidad, un proyecto) |
| Un dominio rico y único, sin capacidades separadas | Solo Clean |
| Varias capacidades, cada una con reglas ricas, equipo mediano o grande, vida larga | **Clean + Screaming** |
| Necesitas contratos publicados y comunicación por eventos entre áreas | Modular Monolith (con Clean dentro de cada módulo, si hace falta) |
| Las capacidades acabarán siendo servicios | Clean + Screaming o Modular Monolith como paso previo; los adaptadores entre capacidades se convertirían en clientes remotos |

## 7. Relación con Vertical Slice y con DDD

- **Vertical Slice** agrupa por *petición*; aquí se agrupa por *capacidad* y, dentro de cada una, por círculo. Se pueden mezclar: dentro de `*.Application` los casos de uso ya son un archivo por acción, que es la mitad de la idea de slice.
- **DDD**: cada capacidad se parece a un *bounded context* pequeño, y `*.Domain` es donde viviría un modelo táctico rico (agregados, value objects). Los nombres de las capacidades deberían salir del lenguaje ubicuo.

## 8. Errores al comparar

- "Clean + Screaming es lo mismo que Modular Monolith": se parecen (ambos agrupan por capacidad y protegen fronteras), pero Modular Monolith define un contrato público y comunicación por eventos; esta combinación pone los **círculos de Clean** dentro de cada capacidad y comunica por **puertos y adaptadores**.
- "Más proyectos = mejor arquitectura": son más fronteras defendidas, a cambio de más ceremonia. En un dominio pequeño sobra.
- "Si el árbol grita el negocio, ya es Clean": no; el árbol puede gritar el negocio con dependencias enredadas (ver el kit de Screaming, que lo vigila con tests de texto).
- "Si tengo Clean, ya grito el negocio": no; Clean puede gritar `Domain/Application/Infrastructure`.
- "Las capacidades no deben hablar entre sí": deben hacerlo, pero por los bordes, con puertos del consumidor.
