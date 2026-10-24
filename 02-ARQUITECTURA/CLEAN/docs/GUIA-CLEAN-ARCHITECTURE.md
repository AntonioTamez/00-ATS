# Guía: Clean Architecture paso a paso (.NET 10)

Esta guía te lleva de cero a un proyecto con arquitectura limpia funcionando. El resultado final es el proyecto de referencia en [`../sample`](../sample): compáralo con lo que vayas construyendo.

> Fuentes: [The Clean Architecture – Robert C. Martin (2012)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) y las guías de estructura de solución .NET de [Milan Jovanović](https://milanjovanovic.tech/blog/clean-architecture-dotnet) y [luisllamas](https://www.luisllamas.es/en/aspnet-core-clean-architecture-layers/).

---

## 1. Qué problema resuelve

Sin estructura, la lógica de negocio termina mezclada con el framework web, la base de datos y las librerías. Consecuencias:

- No se puede testear una regla de negocio sin levantar servidor y base de datos.
- Cambiar de base de datos o de framework obliga a reescribir todo.
- Nadie sabe dónde poner el código nuevo.

Clean Architecture separa **lo que cambia poco y vale mucho** (reglas del negocio) de **lo que cambia seguido y es reemplazable** (web, DB, librerías).

## 2. La única regla que importa: la Regla de Dependencia

> "Nada en un círculo interno puede saber nada sobre algo de un círculo externo." — R. C. Martin

Las dependencias de **código fuente** apuntan siempre **hacia adentro**.

```
   ┌──────────────────────────────────────────────┐
   │ Frameworks & Drivers  (Api, DB, Web, UI)     │
   │  ┌────────────────────────────────────────┐  │
   │  │ Interface Adapters (controllers, repos)│  │
   │  │  ┌──────────────────────────────────┐  │  │
   │  │  │ Use Cases (Application)          │  │  │
   │  │  │  ┌────────────────────────────┐  │  │  │
   │  │  │  │ Entities (Domain)          │  │  │  │
   │  │  │  └────────────────────────────┘  │  │  │
   │  │  └──────────────────────────────────┘  │  │
   │  └────────────────────────────────────────┘  │
   └──────────────────────────────────────────────┘
        las flechas de dependencia apuntan al centro
```

## 3. Qué va en cada capa

| Capa | Proyecto | Contiene | NO puede contener | Referencia a |
|---|---|---|---|---|
| Entities | `Domain` | Entidades, value objects, reglas de negocio, excepciones de dominio | HTTP, SQL, DTOs, JSON, DI | nadie |
| Use Cases | `Application` | Casos de uso, **puertos** (interfaces), DTOs | Reglas de negocio, EF Core, ASP.NET | `Domain` |
| Interface Adapters / Infra | `Infrastructure` | Implementaciones de puertos: repositorios, reloj, email | Lógica de negocio | `Application` |
| Frameworks & Drivers | `Api` | Endpoints/controllers, `Program.cs` (composition root), manejo de errores HTTP | Lógica de negocio | `Application`, `Infrastructure` |

Pregunta de bolsillo para decidir dónde va algo:

1. ¿Es una regla que existiría aunque no hubiera software? → **Domain**.
2. ¿Es "lo que hace la aplicación" (orquestar pasos)? → **Application**.
3. ¿Es un detalle técnico (SQL, HTTP, archivos)? → **Infrastructure / Api**.

## 4. El truco clave: inversión de dependencias

El caso de uso necesita guardar datos, pero **no puede** depender de la base de datos (eso sería apuntar hacia afuera). Solución:

1. `Application` **define** la interfaz `ITodoRepository` (el *puerto*).
2. `Infrastructure` la **implementa** (el *adaptador*).
3. `Api` conecta ambas en el *composition root* (`Program.cs`).

Así el control fluye hacia afuera (use case → DB) pero la dependencia de código apunta hacia adentro.

## 5. Crear el proyecto desde cero

Requisitos: SDK .NET 10.

```bash
mkdir TodoApp && cd TodoApp
dotnet new sln -n TodoApp

dotnet new classlib -n TodoApp.Domain         -o src/TodoApp.Domain
dotnet new classlib -n TodoApp.Application    -o src/TodoApp.Application
dotnet new classlib -n TodoApp.Infrastructure -o src/TodoApp.Infrastructure
dotnet new web      -n TodoApp.Api            -o src/TodoApp.Api
dotnet new xunit    -n TodoApp.Tests          -o tests/TodoApp.Tests

# Referencias: solo hacia adentro
dotnet add src/TodoApp.Application    reference src/TodoApp.Domain
dotnet add src/TodoApp.Infrastructure reference src/TodoApp.Application
dotnet add src/TodoApp.Api            reference src/TodoApp.Application src/TodoApp.Infrastructure
dotnet add tests/TodoApp.Tests        reference src/TodoApp.Domain src/TodoApp.Application src/TodoApp.Infrastructure src/TodoApp.Api

dotnet add src/TodoApp.Application    package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/TodoApp.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add tests/TodoApp.Tests        package Microsoft.AspNetCore.Mvc.Testing

dotnet sln add src/*/*.csproj tests/*/*.csproj
```

El compilador ya te protege: si `Domain` no referencia a `Infrastructure`, no puede usarla.

## 6. Construir de adentro hacia afuera

Siempre en este orden: **Domain → Application → Infrastructure → Api**. Cada paso con su test.

### 6.1 Domain: la entidad con sus reglas

Archivo de referencia: `sample/src/TodoApp.Domain/Todos/TodoItem.cs`.

- Constructor privado + método fábrica `Create(...)`: nadie puede crear una entidad inválida.
- Las reglas viven **dentro** de la entidad (`Complete()` falla si ya estaba completada).
- Recibe la hora como parámetro; no llama a `DateTime.Now` (así es testeable).
- Lanza `DomainException`, no un error HTTP.

### 6.2 Application: puertos y casos de uso

Archivos: `Abstractions/ITodoRepository.cs`, `Abstractions/IClock.cs`, `Todos/CreateTodo.cs`, etc.

- Un caso de uso = una clase con un método `ExecuteAsync`.
- Recibe sus dependencias **como interfaces** por constructor.
- Flujo típico: buscar → llamar al método de la entidad → guardar → devolver un **DTO**.
- Nunca devuelve la entidad hacia afuera: devuelve `TodoDto` (datos planos).
- No contiene `if` de negocio; esos están en el Domain.

### 6.3 Infrastructure: adaptadores

Archivos: `Persistence/InMemoryTodoRepository.cs`, `Time/SystemClock.cs`.

- Implementan los puertos. Son `internal`: nadie fuera los conoce, solo la interfaz.
- Exponen un único `AddInfrastructure()` para registrarse en DI.
- Cambiar a EF Core = escribir otro repositorio y cambiar **una línea** de registro.

### 6.4 Api: el borde HTTP

Archivos: `Todos/TodoEndpoints.cs`, `Todos/DomainExceptionHandler.cs`, `Program.cs`.

- Endpoints finos: reciben request → llaman al caso de uso → devuelven resultado.
- `DomainExceptionHandler` traduce `DomainException` → 422 y `NotFoundException` → 404. Las capas internas no conocen códigos HTTP.
- `Program.cs` es el **composition root**: único lugar que conoce todas las capas.

## 7. Testing por capa

| Capa | Tipo de test | Necesita |
|---|---|---|
| Domain | Unit test directo (`DomainTests.cs`) | nada |
| Application | Unit test con fakes escritos a mano (`UseCaseTests.cs`) | nada |
| Arquitectura | Reflection sobre referencias de ensamblados (`ArchitectureTests.cs`) | nada |
| Api | Integración con `WebApplicationFactory` (`ApiTests.cs`) | servidor en memoria |

`ArchitectureTests` convierte la Regla de Dependencia en un test que falla si alguien rompe la regla. Es la red de seguridad del equipo.

```bash
dotnet test
```

## 8. Flujo de una petición (ejemplo real)

`POST /api/todos {"title":"Learn"}`

1. `TodoEndpoints` recibe el JSON → crea `CreateTodoCommand`.
2. `CreateTodo.ExecuteAsync` pide la hora a `IClock`, llama a `TodoItem.Create` (valida reglas).
3. Guarda vía `ITodoRepository` (en runtime es `InMemoryTodoRepository`).
4. Devuelve `TodoDto` → el endpoint responde `201 Created`.
5. Si el título está vacío: `DomainException` → `DomainExceptionHandler` → `422`.

## 9. Cómo añadir una funcionalidad nueva (receta)

Ejemplo: "borrar un todo".

1. **Domain**: ¿hay una regla nueva? (ej. no se borra uno completado). Añádela a la entidad + test.
2. **Application**: si hace falta, añade el método al puerto `ITodoRepository`; crea `DeleteTodo` + test con fake; regístralo en `AddApplication`.
3. **Infrastructure**: implementa el método nuevo del puerto.
4. **Api**: añade el endpoint, mapea errores si hay nuevos.
5. `dotnet test` → todo verde, incluidos los de arquitectura.

## 10. Errores comunes

| Error | Por qué está mal | Correcto |
|---|---|---|
| Poner `[Required]` / EF attributes en la entidad | Acopla Domain a frameworks | Valida en la entidad; mapea en Infrastructure |
| Devolver la entidad desde la API | Expone el modelo interno y acopla clientes | Devuelve DTOs |
| Casos de uso con `if` de negocio | La regla queda fuera del dominio y se duplica | Muévela a la entidad |
| `DateTime.Now` en el dominio | No testeable | Inyecta `IClock` / pasa la hora |
| Repositorio como clase concreta en Application | Dependencia hacia afuera | Interfaz en Application, clase en Infrastructure |
| Crear capas para un CRUD trivial | Sobre-ingeniería | Úsala cuando haya reglas de negocio reales |

## 11. Cuándo NO usarla

Prototipos desechables, scripts, CRUD sin reglas. La arquitectura tiene un costo (más archivos, más indirección); se justifica cuando el negocio es complejo o el sistema vivirá años.

## 12. Checklist de revisión

- [ ] `Domain` no referencia ningún otro proyecto ni paquete de infraestructura.
- [ ] `Application` solo referencia `Domain`.
- [ ] Las interfaces (puertos) están en `Application`, las implementaciones en `Infrastructure`.
- [ ] Las reglas de negocio están en entidades, no en casos de uso ni controllers.
- [ ] Nada interno devuelve entidades a la capa Api: se usan DTOs.
- [ ] Los errores de negocio se traducen a HTTP solo en `Api`.
- [ ] Existen tests de arquitectura y pasan.

## 13. Matiz honesto

Los círculos son **esquemáticos**: Uncle Bob admite más o menos capas mientras se respete la Regla de Dependencia. Patrones como CQRS o MediatR son opcionales y no definen Clean Architecture. En el sample, `Application` usa solo `Microsoft.Extensions.DependencyInjection.Abstractions` para registrar sus casos de uso; es un trade-off pragmático y aceptado en la comunidad .NET (no es ASP.NET ni persistencia).
