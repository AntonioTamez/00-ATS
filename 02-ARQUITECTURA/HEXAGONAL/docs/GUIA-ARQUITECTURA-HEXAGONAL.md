# Guía: Arquitectura Hexagonal (Puertos y Adaptadores) paso a paso, .NET 10

Con esta guía un dev junior puede construir un proyecto hexagonal desde cero. El resultado final es el proyecto de referencia en [`../sample`](../sample) (una biblioteca que presta libros). Compara siempre con él.

> Fuentes: [Hexagonal Architecture – Alistair Cockburn (2005)](https://alistair.cockburn.us/hexagonal-architecture/). Estructura .NET: guías de [Milan Jovanović](https://milanjovanovic.tech/blog/clean-architecture-dotnet) y [luisllamas](https://www.luisllamas.es/en/aspnet-core-clean-architecture-layers/).
> Para comparar con Clean Architecture: [HEXAGONAL-VS-CLEAN.md](HEXAGONAL-VS-CLEAN.md).

---

## 1. Qué problema resuelve

Cockburn la creó por dos problemas que, según él, tienen **la misma causa**: la lógica de negocio enredada con las interacciones externas.

- **Lado usuario:** la lógica se filtra a la UI. No se puede probar sin UI, ni usar desde un batch u otro programa.
- **Lado servidor:** la lógica queda atada a la base de datos. Si la BD no está disponible o cambia, el desarrollo se frena.

Su objetivo, textual: permitir que una aplicación sea manejada por igual por usuarios, programas, tests automáticos o scripts batch, y se desarrolle y pruebe aislada de sus bases de datos y dispositivos.

## 2. La idea central: adentro vs afuera

La asimetría que importa **no es izquierda/derecha ni arriba/abajo, es dentro/fuera**.

```
                 ACTORES PRIMARIOS (conducen la app)
              ┌── REST ──┐  ┌── CLI ──┐  ┌── Tests ──┐
              │ adapter  │  │ adapter │  │ (también) │
              └────┬─────┘  └────┬────┘  └─────┬─────┘
                   ▼             ▼             ▼
            ╔══════ PUERTOS DE ENTRADA (driving) ══════╗
            ║  IAddBook  ILendBook  IReturnBook  IList ║
            ║                                          ║
            ║              HEXÁGONO (Core)             ║
            ║     Dominio + Servicios de aplicación    ║
            ║                                          ║
            ║  IBookRepository  IClock  IOverdueNotif. ║
            ╚══════ PUERTOS DE SALIDA (driven) ════════╝
                   ▲             ▲             ▲
              ┌────┴─────┐  ┌────┴────┐  ┌─────┴─────┐
              │ InMemory │  │ JsonFile│  │ Console   │
              │ adapter  │  │ adapter │  │ notifier  │
              └──────────┘  └─────────┘  └───────────┘
                 ACTORES SECUNDARIOS (la app los conduce)
```

Vocabulario (con las palabras de Cockburn):

| Término | Significado |
|---|---|
| **Puerto** | "Una conversación con propósito". Una interfaz definida por el núcleo. |
| **Adaptador** | Traduce entre un puerto y una tecnología concreta. Varios adaptadores pueden enchufarse al mismo puerto. |
| **Primario / driving** | Quien **maneja** la app: usuario, test, otro programa. Llama a un puerto de **entrada**. |
| **Secundario / driven** | Lo que la app **maneja**: BD, email, reloj. El núcleo lo llama a través de un puerto de **salida**. |
| **Simetría** | Una vez separado dentro/fuera, todos los actores externos se tratan igual a nivel arquitectónico. |

El hexágono es solo un dibujo: no son seis lados. Cockburn sugiere de 2 a 4 puertos como típico, a criterio.

## 3. Regla de dependencia (cómo se aplica en código)

- El **núcleo no referencia a ningún adaptador** (ni ASP.NET, ni EF, ni JSON file, ni consola).
- Los **adaptadores referencian al núcleo**, nunca al revés.
- Los adaptadores **no se conocen entre sí**.
- Los puertos (interfaces) **los define el núcleo**, aunque los implemente el exterior.

## 4. Mapa del proyecto de referencia

| Proyecto | Rol | Referencia a |
|---|---|---|
| `Library.Core` | El hexágono: dominio, puertos de entrada/salida, servicios | nadie (solo abstracciones de BCL/DI) |
| `Library.Adapters.Outbound` | Adaptadores **secundarios**: `InMemoryBookRepository`, `JsonFileBookRepository`, `SystemClock`, `ConsoleOverdueNotifier` | Core |
| `Library.Adapters.Rest` | Adaptador **primario** HTTP + composition root | Core, Outbound |
| `Library.Adapters.Cli` | Adaptador **primario** de consola + composition root | Core, Outbound |
| `Library.Tests` | Dominio, servicios, contrato de repositorios, arquitectura, adaptadores | todos |

Dentro de `Library.Core`:

```
Domain/                  Book, Loan, DomainException   (reglas del negocio)
Ports/Inbound/           IAddBook, ILendBook, IReturnBook, IListBooks + DTOs/commands
Ports/Outbound/          IBookRepository, IClock, IOverdueNotifier
Services/                implementaciones de los puertos de entrada (casos de uso)
DependencyInjection.cs   AddLibraryCore()
```

## 5. Crear el proyecto desde cero

```bash
mkdir Library && cd Library
dotnet new sln -n Library

dotnet new classlib -n Library.Core             -o src/Library.Core
dotnet new classlib -n Library.Adapters.Outbound -o src/Library.Adapters.Outbound
dotnet new web      -n Library.Adapters.Rest     -o src/Library.Adapters.Rest
dotnet new console  -n Library.Adapters.Cli      -o src/Library.Adapters.Cli
dotnet new xunit    -n Library.Tests             -o tests/Library.Tests

# El núcleo no referencia nada. Los adaptadores apuntan al núcleo.
dotnet add src/Library.Adapters.Outbound reference src/Library.Core
dotnet add src/Library.Adapters.Rest     reference src/Library.Core src/Library.Adapters.Outbound
dotnet add src/Library.Adapters.Cli      reference src/Library.Core src/Library.Adapters.Outbound
dotnet add tests/Library.Tests           reference src/Library.Core src/Library.Adapters.Outbound src/Library.Adapters.Rest src/Library.Adapters.Cli

dotnet add src/Library.Core              package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/Library.Adapters.Outbound package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet add src/Library.Adapters.Cli      package Microsoft.Extensions.DependencyInjection
dotnet add tests/Library.Tests           package Microsoft.AspNetCore.Mvc.Testing

dotnet sln add src/*/*.csproj tests/*/*.csproj
```

## 6. Construir de adentro hacia afuera

### 6.1 Dominio (`Domain/Book.cs`)
- Constructor privado + `Book.Create(...)`: no se crea una entidad inválida.
- Reglas dentro de la entidad: no prestar un libro ya prestado, prestatario obligatorio, devolver solo si está prestado.
- `Rehydrate(...)` existe para que un adaptador de persistencia reconstruya un libro guardado sin saltarse el diseño.
- Sin HTTP, sin SQL, sin JSON.

### 6.2 Puertos de entrada (`Ports/Inbound`)
Preguntas que responde: "¿qué puede pedirle el mundo a mi app?". Un puerto por intención:

```csharp
public interface ILendBook { Task<BookDto> ExecuteAsync(LendBookCommand command, CancellationToken ct = default); }
```

Reciben commands y devuelven DTOs. Las entidades nunca cruzan la frontera.

### 6.3 Puertos de salida (`Ports/Outbound`)
Preguntas que responde: "¿qué necesita mi app del mundo?":

```csharp
public interface IBookRepository { Task AddAsync(Book b, CancellationToken ct); /* ... */ }
public interface IClock { DateTimeOffset UtcNow { get; } }
public interface IOverdueNotifier { Task NotifyOverdueAsync(Book b, Loan l, CancellationToken ct); }
```

El puerto se diseña **desde la necesidad del núcleo**, no desde la tecnología (no `ISqlBookTable`).

### 6.4 Servicios (`Services`)
Implementan los puertos de entrada orquestando dominio + puertos de salida:

```csharp
// ReturnBookService: buscar → regla del dominio → guardar → notificar si hubo retraso
var loan = book.Return();
await books.UpdateAsync(book, ct);
if (loan.IsOverdueAt(clock.UtcNow)) await notifier.NotifyOverdueAsync(book, loan, ct);
```

### 6.5 Adaptadores secundarios (`Library.Adapters.Outbound`)
Dos adaptadores para el **mismo** puerto `IBookRepository`: en memoria y archivo JSON. El formato de almacenamiento (`BookRecord`) es privado del adaptador; el núcleo no lo ve.

### 6.6 Adaptadores primarios (`Rest` y `Cli`)
Traducen su mundo a llamadas a puertos de entrada. **Dos puertas, mismo núcleo**: eso es lo que hace visible el valor del patrón. Cada uno tiene su propio composition root (`Program.cs`) que elige qué adaptadores secundarios enchufar:

```csharp
services.AddLibraryCore().AddOutboundAdapters(jsonFilePath);
```

## 7. Testing

| Qué | Cómo | Archivo |
|---|---|---|
| Dominio | Unit test directo | `CoreTests.cs` (`DomainTests`) |
| Servicios | Por los puertos de entrada, con fakes/spies de los puertos de salida | `CoreTests.cs` (`ServiceTests`) |
| Adaptadores secundarios | **Test de contrato**: la misma suite corre contra cada implementación | `RepositoryContractTests.cs` |
| Adaptadores primarios | REST con `WebApplicationFactory`; CLI con un `StringWriter` | `DrivingAdapterTests.cs` |
| Arquitectura | Reflection sobre referencias de ensamblados | `ArchitectureTests.cs` |

El **test de contrato** es la herramienta hexagonal por excelencia: si un adaptador nuevo pasa el contrato, el núcleo puede usarlo sin enterarse.

```bash
dotnet test     # 28 tests en el proyecto de referencia
```

## 8. Verificación sobre el binario (con REA)

Se inspeccionaron los ensamblados **compilados** con REA (análisis estático de metadatos .NET, sin cargar ni ejecutar el código). Lo observado:

| Observación | Evidencia |
|---|---|
| `Library.Core.dll` referencia solo `System.Runtime`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `System.Collections`, `System.Linq`. **Ningún adaptador ni ASP.NET.** | `ev_60988a2c…a3ef` |
| Su metadata contiene 4 interfaces de entrada (`IAddBook`, `ILendBook`, `IReturnBook`, `IListBooks`), 3 de salida (`IBookRepository`, `IClock`, `IOverdueNotifier`), 4 servicios, y el dominio (`Book`, `Loan`, `DomainException`) | `ev_ede6aab3…4fdd1` |
| `Library.Adapters.Outbound.dll` referencia `Library.Core` y librerías del framework; **ningún otro ensamblado `Library.*`** | `ev_c50d47ac…0b10` |

Limitación: REA hizo análisis estático de metadatos; no ejecuta ni descompila a C#. Las referencias de `Rest` y `Cli` las cubre el test de arquitectura, no se inspeccionaron con REA.

## 9. Receta: añadir una funcionalidad

Ejemplo: "renovar el préstamo".

1. **Dominio:** método `Book.Renew(...)` con su regla + test.
2. **Puerto de entrada:** `IRenewLoan` + command.
3. **Servicio:** `RenewLoanService` + test con fakes; registrarlo en `AddLibraryCore`.
4. **Puerto de salida:** solo si el núcleo necesita algo nuevo del exterior.
5. **Adaptadores primarios:** endpoint REST y comando CLI.
6. `dotnet test` en verde, incluidos arquitectura y contrato.

## 10. Errores comunes

| Error | Por qué está mal | Correcto |
|---|---|---|
| Que `Core` referencie ASP.NET/EF | El núcleo deja de ser independiente | Solo abstracciones; lo técnico va en adaptadores |
| Puertos con forma de tecnología (`ISqlRepo`) | La BD se filtra al núcleo | Puerto según la necesidad del negocio |
| Lógica de negocio en el controller/CLI | Se duplica en cada puerta | En dominio/servicios |
| Un adaptador llama a otro | Acoplamiento lateral | Todo pasa por el núcleo |
| Devolver entidades por los puertos | Expone el modelo interno | DTOs |
| Un solo adaptador y puertos que nunca varían | Costo sin beneficio | Evaluar si el patrón se justifica |

## 11. Checklist de revisión

- [ ] `Core` no referencia ningún adaptador ni framework web/persistencia.
- [ ] Cada puerto tiene un propósito claro y vive en `Core`.
- [ ] Puertos de entrada = intenciones; puertos de salida = necesidades.
- [ ] Hay un test de contrato por cada puerto de salida con más de una implementación.
- [ ] Los adaptadores no se referencian entre sí.
- [ ] Las entidades no cruzan los puertos: se usan DTOs/commands.
- [ ] Los errores se traducen a protocolo (HTTP, exit code) solo en el adaptador.
- [ ] Existe un test de arquitectura y pasa.

## 12. Matiz honesto

Cockburn **no prescribe la estructura interna** del hexágono (cuántas capas hay dentro): solo la frontera. En este sample se usa dominio + servicios; otros equipos usan más capas. Y los adaptadores primarios, como un test o un batch, también cuentan: por eso se puede probar la app "sin UI ni BD".
