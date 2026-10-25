# Guía: Arquitectura en capas (Layered / N-Tier) paso a paso

Para un dev junior que quiere construir un proyecto básico con la arquitectura "de siempre": presentación, negocio y datos. Es la que explica **qué problema intentan resolver** Clean, Hexagonal, Onion y las demás, y también la que sigue siendo suficiente en muchos casos. El sample (préstamos de una biblioteca) tiene 41 tests, incluidas reglas de capas ejecutables, y sus binarios están analizados con REA.

> Fuentes: [Microsoft – N-tier architecture style](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/n-tier), [Microsoft – Architecture styles](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/) y [Martin Fowler – PresentationDomainDataLayering](https://martinfowler.com/bliki/PresentationDomainDataLayering.html). Las citas están copiadas de esas páginas. Los nombres de proyectos y las reglas de los tests son **decisiones de este kit**.

---

## 1. Qué es

Microsoft: *"An N-tier architecture divides an application into logical layers and physical tiers"*. Y sobre las capas: *"A higher layer can use services in a lower layer, but a lower layer can't use services in a higher layer"*.

Fowler lo resume en tres capas: *"presentation (UI), domain logic (aka business logic), and data access"*, con dependencias que *"generally run from top to bottom through the layer stack"*.

```
┌──────────────────────┐
│ Presentación (Web)   │  recibe HTTP, valida formato, devuelve JSON
├──────────────────────┤
│ Negocio (Business)   │  reglas: máximo 3 libros, 14 días, multa por retraso
├──────────────────────┤
│ Datos (Data)         │  tablas y consultas
└──────────────────────┘
        las flechas de dependencia van SIEMPRE hacia abajo
```

### Capa ≠ nivel (tier)

| | Capa (layer) | Nivel (tier) |
|---|---|---|
| Qué es | Una división **lógica** del código | Una división **física**: máquinas o procesos distintos |
| Ejemplo | Un proyecto `Business` | Un servidor web, un servidor de aplicación y una base de datos |
| En el sample | 3 capas | **1 solo nivel** (un proceso) |

Fowler: *"These layers are logical layers not physical tiers"*. Microsoft: *"You might host each layer in its own tier, but this approach isn't required. You can host several layers on the same tier."*

### Capas cerradas vs abiertas

Microsoft: en una arquitectura de **capas cerradas** *"a layer can only call the next layer immediately down"*; en una de **capas abiertas** *"a layer can call any layers under it"*. El sample usa **cerradas**: la presentación solo habla con negocio; nunca toca datos.

## 2. ¿Cuándo es la opción correcta?

Microsoft la recomienda para *"architectural requirements that are still evolving"*, migrar aplicaciones existentes con pocos cambios, o aplicaciones híbridas, y su tabla de estilos la asocia a un dominio *"traditional business"* con *"frequency of updates"* baja. Beneficios que cita: *"Requires less learning curve for most developers"* y *"Costs relatively little by not rearchitecting the solution"*.

Y sus límites (*Challenges*): *"A middle tier might only perform basic create, read, update, delete (CRUD) operations, which adds latency and complexity without delivering meaningful value"* y *"Monolithic design prevents independent deployment of features"*. También (página de estilos): *"the horizontal layering can make it difficult to introduce changes without affecting multiple parts of the application"*.

Regla práctica: **si tu app es en esencia CRUD con pocas reglas y un equipo, las capas son una opción sensata**. El resto del catálogo existe para cuando eso deja de bastar.

## 3. Estructura del sample

```
Lending.Web        ← presentación: endpoints minimal API. Referencia SOLO a Lending.Business
Lending.Business   ← negocio: servicios + DTOs. Referencia SOLO a Lending.Data
Lending.Data       ← datos: records (filas), repositorios, la "base de datos" en memoria
Lending.Tests
```

```
POST /loans {memberId, bookId}
  → Web: recibe y devuelve JSON
     → Business.LoanService.Borrow(): ¿existe el miembro? ¿está libre el libro? ¿tiene menos de 3 préstamos?
        → Data.BookRepository / MemberRepository / LoanRepository: filas
```

## 4. Las reglas de las capas

| Regla | Por qué | Cómo se verifica |
|---|---|---|
| Datos no referencia a nadie | Es la base de todo | Test + REA |
| Negocio referencia solo a Datos | Una capa usa la inmediatamente inferior | Test + REA |
| Presentación referencia solo a Negocio (**capas cerradas**) | Si llega a Datos, se salta las reglas | Test + REA |
| Las filas de Datos (`*Record`) **nunca salen** de Negocio | Si salen, la UI queda acoplada a las tablas | Test: ningún miembro público de Business usa un `*Record` |
| Negocio expone DTOs propios | La UI no ve el modelo de datos | `BookDto`, `LoanDto`... |
| La presentación no sabe que existe la capa de datos | Cambiar la base no debe tocarla | `AddLendingBusiness()` registra Datos por dentro |

## 5. Qué va en cada capa

| Pregunta | Capa |
|---|---|
| ¿"Un miembro no puede tener más de 3 libros"? | Negocio (`LoanService`) |
| ¿"Devuelve 404 si no existe"? | Presentación (traduce la excepción de negocio a HTTP) |
| ¿"Dame los préstamos activos del miembro"? | Datos (`LoanRepository.GetActiveByMember`) |
| ¿"Multa de 0,50 € por día de retraso"? | Negocio |
| ¿"Formato JSON de la respuesta"? | Presentación |
| ¿"Copiar un registro a un DTO"? | Negocio (el "impuesto del mapeo") |
| ¿"Validar que el email contiene @"? | Negocio. La presentación solo comprueba que el JSON llegue bien |

## 6. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

```bash
mkdir MiBiblioteca && cd MiBiblioteca
dotnet new sln -n MiBiblioteca
dotnet new classlib -n Lending.Data     -o src/Lending.Data
dotnet new classlib -n Lending.Business -o src/Lending.Business
dotnet new web      -n Lending.Web      -o src/Lending.Web
dotnet new xunit    -n Lending.Tests    -o tests/Lending.Tests
dotnet add src/Lending.Business reference src/Lending.Data
dotnet add src/Lending.Web      reference src/Lending.Business
dotnet add tests/Lending.Tests  reference src/Lending.Data src/Lending.Business src/Lending.Web
```

**Orden de construcción: de abajo hacia arriba.**

1. **Datos**: las filas (`BookRecord`...), los repositorios y su registro. Pruébalos solos.
2. **Negocio**: DTOs, excepciones propias (`BusinessRuleException`, `NotFoundException`) y servicios. Aquí viven las reglas. Pruébalos con la capa de datos real (en memoria).
3. **Presentación**: endpoints finos que llaman a un servicio y traducen excepciones a códigos HTTP.
4. **Tests de capas** (sección 8).

## 7. Las piezas clave

### Negocio: reglas, y DTOs hacia arriba
```csharp
public LoanDto Borrow(int memberId, int bookId)
{
    var member = members.GetById(memberId) ?? throw new NotFoundException(...);
    var book   = books.GetById(bookId)     ?? throw new NotFoundException(...);

    if (loans.GetActiveByBook(bookId) is not null)
        throw new BusinessRuleException($"'{book.Title}' is already on loan.");
    if (loans.GetActiveByMember(memberId).Count >= MaxActiveLoans)
        throw new BusinessRuleException(...);

    var saved = loans.Insert(new LoanRecord { ... });
    return ToDto(saved, book, member);          // la fila (LoanRecord) no sale de esta capa
}
```

### Presentación: fina, sin reglas
```csharp
app.MapPost("/loans", (NewLoanRequest request, LoanService loans) =>
{
    var loan = loans.Borrow(request.MemberId, request.BookId);
    return Results.Created($"/loans/{loan.Id}", loan);
});
```

### Una sola llamada para registrar todo lo de abajo
```csharp
builder.Services.AddLendingBusiness();   // dentro llama a AddLendingData(): la Web no necesita conocer Datos
```

## 8. Tests

| Qué | Dónde | Idea |
|---|---|---|
| Capa de datos sola | `DataLayerTests` | Copias de filas, consultas de préstamos activos, `Update` |
| Reglas de negocio | `BusinessLayerTests` | Sobre la capa de datos **real** (en memoria), con reloj falso: 14 días, máximo 3, multa por días de retraso |
| Reglas de capas | `ArchitectureTests` | Ver sección 4 |
| Todo junto | `ApiTests` | HTTP real, 404/422/201 |

### Una "caracterización": el rasgo clásico (y el coste) del diseño en capas
El test `Characterization_BusinessServicesDependOnConcreteDataClasses` **no es una regla a mantener**: documenta un hecho. Los servicios de negocio reciben **clases concretas** de datos (`BookRepository`), no interfaces. Consecuencias:
- Para probar negocio necesitas la capa de datos (aquí es barata porque es en memoria; con una base real, no).
- Cambiar de almacenamiento obliga a tocar el negocio.

Fowler describe la variación que invierte esto: *"A common variation is to arrange things so that the domain does not depend on its data sources"* (con un mapper entre ambos, lo que se conoce como Hexagonal). Es exactamente el paso que dan Clean, Onion y Hexagonal.

### Pruebas de mutación (que las reglas muerden)
En copias temporales, sin tocar el código real:

1. **Web referencia a Data y la usa** → falla `ThePresentationLayer_UsesOnlyTheBusinessLayer_ClosedLayers`.
2. **Business expone una fila de Datos** (`BookRecord Raw(int id)`) → falla `NoDataRecord_IsEverExposedByTheBusinessLayer`.
3. **Se quita la regla de máximo 3 préstamos** → falla `AMember_CannotHaveMoreThanThreeBooksAtOnce`.

### Experimento: ¿cuánto cuesta añadir un campo?
Añadí un campo `Isbn` a los libros, de punta a punta (en una copia temporal). Resultado medido: **3 archivos** tocados (uno por capa: `Records.cs`, `Services.cs`, `Program.cs`), **17 líneas** cambiadas, y los 41 tests siguieron pasando.

Interpretación honesta: en un proyecto así de pequeño el "efecto dominó" de las capas es **real pero barato**; cada capa necesita su parte (la fila, el DTO y su mapeo, la petición). Microsoft advierte de que ese coste crece en sistemas grandes: *"the horizontal layering can make it difficult to introduce changes without affecting multiple parts of the application"*. Este sample no demuestra ese problema a escala; solo muestra el mecanismo.

## 9. Verificación del binario con REA

Analicé los `.dll` compilados con REA (metadatos estáticos; no ejecuta nada).

| Hecho | Evidencia |
|---|---|
| `Lending.Data.dll` referencia solo ensamblados del framework | `inspect_managed_artifact`, `ev_af41221a9…6eb2` |
| `Lending.Business.dll` referencia, de lo nuestro, **solo `Lending.Data`** | `inspect_managed_artifact`, `ev_ea5aa6206…3df4` |
| `Lending.Web.dll` referencia, de lo nuestro, **solo `Lending.Business`** (no `Lending.Data`) | `inspect_managed_artifact`, `ev_6460a6318…ff1c` |

REA **no** prueba que las reglas de negocio funcionen (para eso están los 41 tests).

## 10. Receta: añadir "reservar un libro"

Requisito: un miembro puede reservar un libro que está prestado.

1. **Datos**: `ReservationRecord` y `ReservationRepository` (+ registro en `AddLendingData`). Test del repositorio.
2. **Negocio**: `ReservationDto` y un método `Reserve(memberId, bookId)` en un servicio con sus reglas ("no se reserva un libro libre", "no dos veces"). Tests con la capa de datos real.
3. **Presentación**: `POST /reservations` que llama al servicio.
4. Si además quieres que `Return` avise al primero de la cola: tocas `LoanService` (Negocio) y quizá un nuevo `NotificationService`.

Observa que una funcionalidad **atraviesa las tres capas**. Es la diferencia de fondo con Vertical Slice, que agrupa por funcionalidad.

## 11. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| **Capa de negocio que solo reenvía** a datos (CRUD) | Añade código sin valor (Microsoft lo señala) | Si no hay reglas, simplifica o no la separes |
| Lógica de negocio en los endpoints/controladores | Se duplica y no se puede probar sin HTTP | Mover a la capa de negocio |
| **Devolver las filas de datos a la UI** | La UI queda atada a las tablas | DTOs propios |
| Saltarse capas (la UI consulta datos "porque es más rápido") | Las reglas se esquivan | Capas cerradas + test |
| Reglas en la capa de datos (consultas con lógica, procedimientos) | Las reglas se reparten en dos sitios | Datos solo persiste y consulta |
| Un `Common`/`Utils` al que todos referencian | Dependencia oculta; se convierte en cajón de sastre | Cada cosa en su capa |
| Servicios gigantes ("`OrderService` de 2000 líneas") | Una clase hace todo | Dividir por responsabilidad |
| Pensar que capas = niveles | Despliegues y redes innecesarios | Capas lógicas primero |
| **Probar el negocio solo con la base real** | Tests lentos y frágiles | Un seam (interfaz) hacia datos, o una base en memoria como aquí |

## 12. Hacia dónde evoluciona

| Si duele… | Considera |
|---|---|
| El negocio no se puede probar sin la base | **Clean / Onion / Hexagonal**: invertir la dependencia con interfaces |
| Cada cambio toca las tres capas | **Vertical Slice**: agrupar por funcionalidad |
| El negocio es complejo y está anémico | **DDD táctico**: modelo rico en lugar de servicios con filas |
| Equipos que se pisan | **Monolito modular** |
| Partes con escala distinta | **Microservicios** (con cuidado) |

No es obligatorio: si no duele, no cambies.

## 13. Límites de este sample (honestidad)

- **La "base de datos" es memoria con `lock`**; no hay SQL, EF, migraciones ni transacciones reales.
- **Condición de carrera en `Borrow`**: comprueba si el libro está libre y luego inserta, sin atomicidad. Dos peticiones simultáneas por el mismo libro podrían ganar las dos. En una base real se resuelve con una restricción única o una transacción; aquí no está implementado ni probado.
- **Un solo nivel (proceso)**: demuestra capas lógicas, no niveles físicos.
- **El efecto de "cambios en cascada" solo está medido en un campo simple**, no a escala.
- **El mapeo es manual** y hay poco; en sistemas grandes se automatiza o se vuelve tedioso.
- **No hay autenticación ni paginación.**
- **Dominio simple**: las reglas caben en servicios; con un dominio más rico, este diseño tiende a un modelo anémico.

## 14. Checklist

- [ ] ¿Mi app justifica separar en capas, o es un CRUD que cabe en un proyecto?
- [ ] Cada capa referencia solo a la inmediatamente inferior
- [ ] Las reglas de negocio están en la capa de negocio, no en endpoints ni en consultas
- [ ] Las filas de datos no salen de la capa de negocio
- [ ] Los endpoints son finos
- [ ] Sé cómo pruebo el negocio sin depender de una base real
- [ ] Hay tests de capas y probé al menos una mutación
- [ ] Sé qué parte duele y a qué estilo del catálogo evolucionaría
