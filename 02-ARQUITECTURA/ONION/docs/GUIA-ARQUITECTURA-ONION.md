# Guía: Onion Architecture paso a paso (.NET 10)

Con esta guía un dev junior puede construir un proyecto con arquitectura Onion desde cero. El resultado final es el proyecto de referencia en [`../sample`](../sample) (una tienda con clientes y pedidos). Compara siempre con él.

> Fuentes primarias: [The Onion Architecture, partes 1](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-1/), [2](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-2/) y [3](https://jeffreypalermo.com/2008/08/the-onion-architecture-part-3/) – Jeffrey Palermo (2008).
> Para comparar con las otras: [ONION-VS-CLEAN-VS-HEXAGONAL.md](ONION-VS-CLEAN-VS-HEXAGONAL.md).

---

## 1. Qué problema resuelve

Palermo parte de la arquitectura en capas tradicional (UI → lógica de negocio → acceso a datos) y señala su defecto:

- Cada capa depende de la de abajo, y la lógica de negocio termina **acoplada al acceso a datos**.
- Sus palabras: *"Transitive dependencies are still dependencies"* (las dependencias transitivas siguen siendo dependencias).
- Las técnicas de acceso a datos cambian cada pocos años; un sistema acoplado a ellas envejece mal y acaba reescrito.

## 2. La idea central

Dar la vuelta al diagrama: el **modelo de dominio va en el centro** y todo lo demás, incluida la base de datos, queda **afuera**.

> *"The database is not the center. It is external."*

Los tres principios (casi textuales de Palermo):

1. **Inner layers define interfaces. Outer layers implement interfaces.** Las capas internas definen interfaces; las externas las implementan.
2. **Direction of coupling is toward the center.** Todo el acoplamiento apunta hacia el centro.
3. **Todo el código del núcleo puede compilarse y ejecutarse sin la infraestructura.**

Y una diferencia importante con las capas clásicas: el código de una capa **puede usar directamente cualquier capa más interna** (no solo la inmediatamente inferior). Lo que nunca puede es depender de una capa más externa.

```
   ┌──────────────────────────────────────────────────────┐
   │  Anillo exterior: UI · Infraestructura · Tests       │
   │   ┌────────────────────────────────────────────────┐ │
   │   │  Application Services (casos de uso)           │ │
   │   │   ┌──────────────────────────────────────────┐ │ │
   │   │   │  Domain Services (+ interfaces de repos) │ │ │
   │   │   │   ┌────────────────────────────────────┐ │ │ │
   │   │   │   │  Domain Model (entidades, VOs)     │ │ │ │
   │   │   │   └────────────────────────────────────┘ │ │ │
   │   │   └──────────────────────────────────────────┘ │ │
   │   └────────────────────────────────────────────────┘ │
   └──────────────────────────────────────────────────────┘
          todas las dependencias apuntan al centro
```

### Un matiz honesto sobre los anillos

En las partes 1–3, Palermo habla de un **núcleo** (modelo de dominio y lógica de apoyo alrededor) con **infraestructura, UI y tests afuera**, y dice que el número de capas varía. La división en **cuatro anillos** (Modelo de dominio → Servicios de dominio → Servicios de aplicación → exterior) es la que difunden las fuentes secundarias (por ejemplo [Herberto Graça](https://herbertograca.com/2017/09/21/onion-architecture/) y [NDepend](https://blog.ndepend.com/onion-architecture-layers)). Esta guía usa esos cuatro anillos porque son la forma más clara de enseñarla, pero recuerda que son una convención, no un mandato del autor.

Palermo advierte además que **no es para sitios web pequeños**: encaja en aplicaciones empresariales longevas con comportamiento complejo.

## 3. Qué va en cada anillo

| Anillo | Proyecto | Contiene | NO puede contener | Referencia a |
|---|---|---|---|---|
| 1. Domain Model | `Shop.Domain.Model` | Entidades (`Order`, `Customer`), value objects (`Money`), `DomainException` | Interfaces de repositorio, casos de uso, HTTP, SQL | nadie |
| 2. Domain Services | `Shop.Domain.Services` | **Interfaces de repositorio**, servicios de dominio (`OrderPlacementService`) | Orquestación de casos de uso, I/O | Model |
| 3. Application Services | `Shop.Application` | Casos de uso (`PlaceOrderService`…), DTOs, `IClock` | Reglas de negocio, EF, ASP.NET | Model, Domain Services |
| 4. Exterior | `Shop.Infrastructure`, `Shop.Api`, `Shop.Tests` | Repositorios, reloj, endpoints HTTP, composition root, tests | Lógica de negocio | los anillos internos |

### ¿Entidad, servicio de dominio o servicio de aplicación?

1. ¿La regla afecta a **una sola entidad**? → dentro de la entidad (`Order.Place` exige al menos una línea).
2. ¿La regla involucra **varias entidades** y no encaja en ninguna? → **servicio de dominio** (`OrderPlacementService`: el descuento depende del pedido *y* del cliente).
3. ¿Es **orquestar pasos** (cargar, llamar al dominio, guardar)? → **servicio de aplicación** (`PlaceOrderService`).

## 4. Inversión de dependencias: dónde viven las interfaces

Las interfaces de repositorio (`IOrderRepository`) se definen en el **anillo 2**, justo alrededor del modelo (Palermo: la primera capa alrededor del modelo contiene típicamente las interfaces de repositorio). La implementación (`InMemoryOrderRepository`) vive en el **anillo exterior**. El código de infraestructura "implementa interfaces del núcleo de la aplicación".

Por eso `IClock`, que solo necesitan los servicios de aplicación, se define en el anillo 3, no en el 2: cada interfaz va en el anillo **más interno que la necesita**.

## 5. Crear el proyecto desde cero

```bash
mkdir Shop && cd Shop
dotnet new sln -n Shop

dotnet new classlib -n Shop.Domain.Model    -o src/Shop.Domain.Model
dotnet new classlib -n Shop.Domain.Services -o src/Shop.Domain.Services
dotnet new classlib -n Shop.Application     -o src/Shop.Application
dotnet new classlib -n Shop.Infrastructure  -o src/Shop.Infrastructure
dotnet new web      -n Shop.Api             -o src/Shop.Api
dotnet new xunit    -n Shop.Tests           -o tests/Shop.Tests

# Referencias: solo hacia el centro
dotnet add src/Shop.Domain.Services reference src/Shop.Domain.Model
dotnet add src/Shop.Application      reference src/Shop.Domain.Services src/Shop.Domain.Model
dotnet add src/Shop.Infrastructure   reference src/Shop.Application src/Shop.Domain.Services src/Shop.Domain.Model
dotnet add src/Shop.Api              reference src/Shop.Application src/Shop.Infrastructure src/Shop.Domain.Model
dotnet add tests/Shop.Tests          reference src/Shop.Domain.Model src/Shop.Domain.Services src/Shop.Application src/Shop.Infrastructure src/Shop.Api

dotnet add src/Shop.Application    package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/Shop.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add tests/Shop.Tests        package Microsoft.AspNetCore.Mvc.Testing

dotnet sln add src/*/*.csproj tests/*/*.csproj
```

Nota: `Shop.Infrastructure` referencia **tres** anillos a propósito. Es la regla de Palermo: una capa externa puede usar cualquier capa interna, no solo la contigua.

## 6. Construir de adentro hacia afuera

Orden: **Model → Domain Services → Application → Infrastructure → Api**, cada paso con su test.

### 6.1 Domain Model (`Shop.Domain.Model`)
- `Money` es un **value object** (`readonly record struct`): inmutable, nunca negativo, se compara por valor.
- `Order` y `Customer` protegen sus reglas: constructor privado + `Create(...)`, estado modificable solo por métodos (`AddLine`, `Place`, `Cancel`).
- `Rehydrate(...)` permite a la infraestructura reconstruir una entidad guardada sin romper el diseño.
- Cero dependencias de proyecto. Solo tipos base del framework.

### 6.2 Domain Services (`Shop.Domain.Services`)
- `IOrderRepository` / `ICustomerRepository`: las interfaces que el núcleo exige a la infraestructura.
- `OrderPlacementService`: calcula el descuento (10 % si el subtotal ≥ 100, +5 % si el cliente es fiel) y coloca el pedido registrándolo en el cliente. Vive aquí porque involucra `Order` **y** `Customer`.

### 6.3 Application Services (`Shop.Application`)
- Un servicio por caso de uso: `RegisterCustomerService`, `PlaceOrderService`, `CancelOrderService`, `GetOrderService`.
- Cargan datos vía las interfaces, llaman al dominio y devuelven **DTOs** (`OrderDto`). Las entidades no salen de este anillo.
- Sin `if` de negocio.

### 6.4 Infrastructure (`Shop.Infrastructure`)
- Repositorios en memoria y `SystemClock`, implementaciones `internal` expuestas solo por `AddInfrastructure()`.
- Cambiar a EF Core = otro repositorio y una línea de registro.

### 6.5 Api (`Shop.Api`)
- Endpoints finos que llaman a servicios de aplicación.
- `DomainExceptionHandler` traduce `DomainException` → 422 y `NotFoundException` → 404.
- `Program.cs` es el composition root: el único lugar que conoce todos los anillos.

## 7. Testing por anillo

| Anillo | Tipo de test | Archivo |
|---|---|---|
| Model | Unit puro, nada que simular | `ModelTests.cs` |
| Domain Services | Unit sobre varias entidades reales | `ModelTests.cs` (`DomainServiceTests`) |
| Application Services | Fakes de las interfaces de repositorio y reloj | `ApplicationServiceTests.cs` |
| Reglas de dependencia | Reflection sobre referencias de ensamblados | `ArchitectureTests.cs` |
| Exterior (Api) | Integración con `WebApplicationFactory` | `ApiTests.cs` |

```bash
dotnet test     # 28 tests en el proyecto de referencia
```

## 8. Verificación sobre el binario (con REA)

Se inspeccionaron los ensamblados **compilados** con REA (análisis estático de metadatos, sin cargar ni ejecutar código). Lo observado en `AssemblyRef`:

| Ensamblado | Referencias `Shop.*` observadas | Evidencia |
|---|---|---|
| `Shop.Domain.Model.dll` | ninguna (solo `System.Runtime`, `System.Collections`, `System.Linq`) | `ev_d1b1f95a…97a6` |
| `Shop.Domain.Services.dll` | `Shop.Domain.Model` | `ev_0c0e29b8…f8cb` |
| `Shop.Application.dll` | `Shop.Domain.Model`, `Shop.Domain.Services` (+ DI Abstractions) | `ev_731014a6…9b27` |
| `Shop.Infrastructure.dll` | `Shop.Application`, `Shop.Domain.Services`, `Shop.Domain.Model` (+ DI Abstractions); **no `Shop.Api`** | `ev_d7a0c4df…97bf` |

Esto confirma en el binario que cada anillo depende solo de anillos más internos, y que la infraestructura usa varios anillos directamente (saltándose capas), como describe Palermo.

Limitación: es análisis estático de metadatos; no decompila a C# ni ejecuta. `Shop.Api` y `Shop.Tests` no se inspeccionaron con REA: el test de arquitectura cubre las reglas de los anillos internos.

## 9. Receta: añadir una funcionalidad

Ejemplo: "envío gratis para pedidos de más de 200".

1. **Model:** si es una propiedad del pedido, añádela a `Order` + test. (Aquí no: es una política.)
2. **Domain Services:** extiende `OrderPlacementService` (afecta a pedido y cliente) + test.
3. **Application:** si cambia el DTO o hay un caso de uso nuevo, añádelo + test con fakes.
4. **Infrastructure:** solo si hay una interfaz nueva que implementar.
5. **Api:** expón el campo/endpoint.
6. `dotnet test` en verde, incluido arquitectura.

## 10. Errores comunes

| Error | Por qué está mal | Correcto |
|---|---|---|
| Interfaces de repositorio en `Infrastructure` | El núcleo dependería de lo externo | Definirlas en Domain Services |
| `Order` con atributos de EF | Acopla el modelo a la persistencia | Mapear en Infrastructure (`Rehydrate`) |
| Reglas multi-entidad dentro del caso de uso | Se duplican y salen del dominio | Servicio de dominio |
| Devolver entidades desde la Api | Expone el modelo interno | DTOs |
| Un anillo interno referencia uno externo | Rompe la regla central | Invertir con una interfaz |
| Aplicarla a un sitio web pequeño | Palermo mismo lo desaconseja | Estructura más simple |

## 11. Checklist de revisión

- [ ] `Domain.Model` no referencia ningún otro proyecto.
- [ ] Cada anillo referencia solo anillos más internos.
- [ ] Las interfaces están en el anillo más interno que las necesita; las implementaciones, afuera.
- [ ] Las reglas de una entidad están en la entidad; las multi-entidad, en un servicio de dominio.
- [ ] Los servicios de aplicación solo orquestan.
- [ ] Nada interno devuelve entidades a la Api: DTOs.
- [ ] El núcleo compila y se prueba sin infraestructura.
- [ ] El test de arquitectura existe y pasa.
