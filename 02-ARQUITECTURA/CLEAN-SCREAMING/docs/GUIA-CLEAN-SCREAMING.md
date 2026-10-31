# Guía: Clean Architecture + Screaming Architecture

Para un dev junior que ya conoce las dos ideas por separado y quiere **juntarlas en un proyecto real**: que la estructura grite el negocio (Screaming) y que, dentro de cada parte del negocio, las dependencias apunten hacia adentro (Clean), con el **compilador** vigilando ambas cosas. El sample es la misma clínica del kit de Screaming, reconstruida así. Tiene 70 tests y sus binarios están analizados con REA.

> Fuentes: [Robert C. Martin – The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) y [Robert C. Martin – Screaming Architecture](https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html). Las citas están copiadas de esos artículos. **La combinación concreta (un conjunto de proyectos por capacidad, adaptadores entre capacidades, las reglas de los tests) es una decisión de este kit**, no una receta de los artículos.
> Antes de seguir conviene haber visto [`CLEAN/`](../../CLEAN) y [`SCREAMING/`](../../SCREAMING).

---

## 1. Qué aporta cada una (y qué le falta)

| | Clean Architecture | Screaming Architecture |
|---|---|---|
| Responde a | ¿Hacia dónde apuntan las dependencias? | ¿Qué comunica la estructura? |
| Su regla | *"source code dependencies can only point inwards"*; *"Nothing in an inner circle can know anything at all about something in an outer circle"* | *"Your architectures should tell readers about the system, not about the frameworks you used in your system"* |
| Lo que NO dice | Cómo se llaman las carpetas ni cómo se agrupa | Hacia dónde apuntan las dependencias |
| En su kit | Los proyectos se llaman `Domain`, `Application`, `Infrastructure`, `Api`: **gritan las capas** | Una sola carpeta por capacidad, pero **sin fronteras que defienda el compilador** |

Cada una deja un hueco que la otra cubre:

```
Solo Clean       → dependencias perfectas, árbol que grita "capas"
Solo Screaming   → árbol que grita "clínica", fronteras defendidas por un test de texto
Clean + Screaming → árbol que grita "clínica" Y fronteras defendidas por el compilador
```

## 2. Los tres árboles

```
CLEAN (solo)              SCREAMING (solo)           CLEAN + SCREAMING
src/                      src/Clinic/                src/
├ TodoApp.Domain          ├ Patients/                ├ Patients/
├ TodoApp.Application     ├ Appointments/            │  ├ Clinic.Patients.Domain
├ TodoApp.Infrastructure  ├ Billing/                 │  ├ Clinic.Patients.Application
└ TodoApp.Api             └ Program.cs               │  └ Clinic.Patients.Adapters
                                                     ├ Appointments/  (los mismos 3)
  ¿De qué trata?            ¿De qué trata?           ├ Billing/       (los mismos 3)
  No se sabe.               De una clínica.          ├ Clinic.SharedKernel
                                                     └ Clinic.Host
                                                     ¿De qué trata? De una clínica.
                                                     ¿Cómo está hecha? Con círculos de Clean.
```

La primera línea del árbol dice **qué hace** el sistema; la segunda, dentro de cada carpeta, dice **cómo está construido**.

## 3. Estructura del sample

```
src/
├─ Patients/       Clinic.Patients.Domain   ← ENTIDADES (círculo 1): Patient y sus reglas
│                  Clinic.Patients.Application ← CASOS DE USO (círculo 2): RegisterPatient, GetPatient + puertos
│                  Clinic.Patients.Adapters ← ADAPTADORES (círculo exterior): repositorio en memoria + endpoints HTTP
├─ Appointments/   (Domain, Application, Adapters) ← Appointment, BookAppointment, CancelAppointment...
├─ Billing/        (Domain, Application, Adapters) ← Invoice, ChargeLateCancellationFee, ListInvoices
├─ Clinic.SharedKernel   ← solo RuleViolationException y NotFoundException
└─ Clinic.Host           ← composition root: enchufa las tres capacidades
```

Cada círculo es un **proyecto**, así que "no pueden saber nada del exterior" lo hace cumplir el compilador y no la disciplina.

## 4. Las dos dimensiones de dependencia

**Dentro de una capacidad (regla de Clean):**

```
Adapters ───► Application ───► Domain ───► SharedKernel
 (exterior)    (casos de uso)   (entidades)
```

**Entre capacidades (la regla nueva que aparece al combinar):**

```
Billing.Adapters ──► Appointments.Application      Appointments.Adapters ──► Patients.Application
   (solo desde un adaptador, y solo a la capa Application de la otra capacidad)
```

Los círculos interiores (`Domain`, `Application`) de una capacidad **nunca** referencian a otra capacidad. Solo los adaptadores, el borde, se tocan.

## 5. El problema nuevo: cómo se hablan las capacidades

En el kit de Screaming, `Billing` implementaba directamente una interfaz de `Appointments`. Aquí, además, la regla de Clean impide que un caso de uso conozca a otra capacidad. La solución es el patrón de **puerto + adaptador**, usado también *entre* capacidades:

| Lo que Citas necesita | Puerto (lo define Citas) | Adaptador (lo pone el vecino, en SU borde) |
|---|---|---|
| "¿Existe este paciente?" | `IPatientLookup` en `Appointments.Application` | `PatientLookupAdapter` en `Appointments.Adapters`: llama al caso de uso `GetPatient` de Pacientes |
| "Cobra una cancelación tardía" | `IFeeCharger` en `Appointments.Application` | `FeeChargerAdapter` en `Billing.Adapters`: ejecuta el caso de uso `ChargeLateCancellationFee` |

Así `BookAppointment` y `CancelAppointment` no saben que Pacientes y Facturación existen: solo conocen sus puertos. Es la regla de dependencia de Clean aplicada a la frontera entre capacidades.

## 6. Reglas y cómo se verifican

| Regla | Cómo se verifica |
|---|---|
| **(Clean)** Las entidades solo conocen el kernel compartido | Test + REA |
| **(Clean)** Los casos de uso solo conocen sus propias entidades y el kernel | Test + REA |
| **(Clean)** Domain y Application no tocan ASP.NET Core ni el contenedor de DI | Test + REA |
| **(Clean)** Los puertos solo se implementan en `Adapters` | Test |
| **(Clean)** Lo que cruza la frontera es un DTO simple, nunca una entidad | Test |
| **(Clean)** Las entidades no tienen constructor ni setters públicos | Test |
| **(Ambas)** Ningún círculo interior referencia a otra capacidad | Test + REA |
| **(Ambas)** Las capacidades solo se tocan en `Adapters → Application` de la otra | Test + REA |
| **(Ambas)** El grafo de capacidades va en una dirección: Billing → Appointments → Patients | Test |
| **(Screaming)** Las carpetas de `src/` son capacidades, no roles técnicos ni capas | Test que lee el disco |
| **(Screaming)** Dentro de una capacidad, solo las tres capas esperadas | Test |
| **(Screaming)** El kernel compartido contiene solo las dos excepciones | Test |
| El `Host` solo conoce los adaptadores | Test |

## 7. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

1. **Lista las capacidades** con el lenguaje del negocio (como en Screaming): `Patients`, `Appointments`, `Billing`.
2. **Crea las carpetas y, en cada una, tres proyectos** (`classlib`):

```bash
mkdir MiClinica && cd MiClinica && dotnet new sln -n MiClinica
dotnet new classlib -n Clinic.SharedKernel -o src/Clinic.SharedKernel

# por cada capacidad X:
dotnet new classlib -n Clinic.X.Domain      -o src/X/Clinic.X.Domain
dotnet new classlib -n Clinic.X.Application -o src/X/Clinic.X.Application
dotnet new classlib -n Clinic.X.Adapters    -o src/X/Clinic.X.Adapters
dotnet add src/X/Clinic.X.Domain      reference src/Clinic.SharedKernel
dotnet add src/X/Clinic.X.Application reference src/X/Clinic.X.Domain src/Clinic.SharedKernel
dotnet add src/X/Clinic.X.Adapters    reference src/X/Clinic.X.Application src/X/Clinic.X.Domain

dotnet new web -n Clinic.Host -o src/Clinic.Host        # referencia SOLO los *.Adapters
```

3. **Domain primero** (las reglas que son verdad en cualquier pantalla o base de datos). Pruébalo sin mocks.
4. **Application**: casos de uso + los **puertos** que necesitan. Pruébalo con fakes escritos a mano.
5. **Adapters**: repositorios, endpoints y el **cableado** del módulo (`AddX()`, `MapX()`).
6. **Entre capacidades**: define el puerto en el consumidor; el adaptador va en el borde (de uno u otro lado, pero nunca dentro de un círculo interior).
7. **Host**: `AddPatients(); AddAppointments(); AddBilling();`.
8. **Tests de arquitectura** (sección 6) desde el principio.

## 8. Las piezas clave

### La regla vive en la entidad; el caso de uso la usa y añade lo suyo
```csharp
// Appointments.Domain: verdad en cualquier contexto
public static Appointment Book(Guid patientId, string? doctor, DateTimeOffset start, DateTimeOffset now) { /* horario, futuro... */ }

// Appointments.Application: orquesta con PUERTOS
public AppointmentDto Execute(Guid patientId, string? doctor, DateTimeOffset start)
{
    if (!patients.Exists(patientId)) throw new NotFoundException(...);          // puerto IPatientLookup
    var appointment = Appointment.Book(patientId, doctor, start, clock.GetUtcNow());
    if (!appointments.TryAdd(appointment)) throw new RuleViolationException(...); // puerto del repositorio
    return AppointmentMapping.ToDto(appointment);                                // sale un DTO, no la entidad
}
```

### La costura entre capacidades, en el borde
```csharp
// Appointments.Adapters
public sealed class PatientLookupAdapter(GetPatient getPatient) : IPatientLookup
{
    public bool Exists(Guid patientId) => getPatient.Find(patientId) is not null;
}

// Billing.Adapters
public sealed class FeeChargerAdapter(ChargeLateCancellationFee charge) : IFeeCharger
{
    public void ChargeLateCancellation(Guid patientId, Guid appointmentId) => charge.Execute(patientId, appointmentId);
}
```

### El módulo de cada capacidad
```csharp
// Host/Program.cs
builder.Services.AddPatients();
builder.Services.AddAppointments();
builder.Services.AddBilling();
...
app.MapPatients(); app.MapAppointments(); app.MapBilling();
```

## 9. Tests

| Qué | Dónde | Idea |
|---|---|---|
| Entidades | `DomainTests` (21) | Objetos planos: reglas de horario, cancelación tardía, facturas |
| Casos de uso | `ApplicationTests` (15) | Con **fakes de los puertos**: el repositorio, "¿existe el paciente?", "cobra la tarifa" son 5 líneas cada uno |
| Todo enchufado | `ApiTests` (8) | HTTP real con reloj fijo; comprueba que los adaptadores entre capacidades funcionan |
| Reglas | `ArchitectureTests` (26) | Las dos mitades: Clean (dependencias) y Screaming (qué grita el disco) |

Los casos de uso se prueban sin servidor web, sin base de datos y sin las otras capacidades: justo lo que prometen los dos artículos.

### Pruebas de mutación (que las reglas muerden)
En copias temporales, sin tocar el código real. Ocho, todas atrapadas:

1. Una entidad de `Patients` empieza a usar `Appointments.Domain` → fallan 2 tests.
2. Un caso de uso de `Appointments` llama directamente a `Patients.Application` → fallan 2 tests.
3. `Patients.Application` importa ASP.NET Core → falla el test de círculos interiores.
4. Aparece una carpeta `Services` en `src/` → fallan 2 tests de estructura.
5. Se quita la regla del fin de semana de la entidad → fallan 3 tests (entidad, caso de uso y la regla por HTTP).
6. Un caso de uso devuelve la entidad en lugar de un DTO → falla el test de frontera.
7. `Patients.Adapters` empieza a depender de `Appointments` → fallan 2 tests del grafo.
8. Nunca se cobra la tarifa → fallan 3 tests (dos de aplicación y uno por HTTP).

## 10. Verificación del binario con REA

| Hecho | Evidencia |
|---|---|
| `Clinic.Appointments.Application.dll` referencia, de lo nuestro, **solo** `Clinic.Appointments.Domain` y `Clinic.SharedKernel`: ni `Patients` ni `Billing` ni ASP.NET Core | `inspect_managed_artifact`, `ev_7486d7e60…a921` |
| `Clinic.Appointments.Adapters.dll` referencia `Appointments.Application`, `Appointments.Domain` y **`Patients.Application`** (la costura), más ASP.NET Core | `ev_b04d72502…9e72` |
| `Clinic.Billing.Adapters.dll` referencia `Billing.Application`, `Billing.Domain` y **`Appointments.Application`** (la costura), más ASP.NET Core | `ev_1a83f0cb1…2593` |

Es la imagen binaria de la regla: el único sitio de `Appointments` que sabe que existe `Patients` es su ensamblado de adaptadores. REA **no** prueba el comportamiento en ejecución (para eso están los 70 tests).

## 11. Receta: añadir la capacidad "Reminders" (recordatorios)

Requisito: antes de cada cita, enviar un recordatorio al paciente.

1. **Carpeta y tres proyectos**: `src/Reminders/Clinic.Reminders.{Domain,Application,Adapters}`.
2. `Domain`: `Reminder` con sus reglas (no se recuerda una cita cancelada...). `Application`: caso de uso `SendReminders`, puerto `IReminderSender` y un puerto `IUpcomingAppointments`.
3. `Adapters`: el sender de verdad (aquí, en memoria) y `UpcomingAppointmentsAdapter`, que llama a `ListAppointments` de **Appointments.Application** (una nueva costura, desde el borde).
4. Registrar `AddReminders()`/`MapReminders()` en el Host.
5. **Tests**: añade la capacidad a las listas de `ArchitectureTests` (`Capabilities` y el grafo esperado: `Reminders → Appointments`). Que ese cambio sea obligatorio es deliberado.
6. `Appointments` y `Patients` **no se tocan**.

## 12. Cuándo sí y cuándo no

| Tu situación | Recomendación |
|---|---|
| Dominio con reglas ricas, equipo mediano, vida larga | **Esta combinación** |
| Dominio pequeño o CRUD | Solo Screaming en un proyecto (kit [`SCREAMING/`](../../SCREAMING)); las capas serían ceremonia |
| Un único dominio sin capacidades separadas | Solo Clean (kit [`CLEAN/`](../../CLEAN)) |
| Quieres fronteras más fuertes y contratos publicados entre módulos | [Modular Monolith](../../MODULAR-MONOLITH) |
| Las capacidades necesitan desplegarse por separado | Cada capacidad ya está casi lista para ser un servicio: los adaptadores entre capacidades pasarían a ser clientes remotos |

## 13. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| Volver a poner `Domain`, `Application`... como carpetas de **primer nivel** | El árbol vuelve a gritar capas | Primer nivel = capacidades; las capas, dentro |
| Un caso de uso que referencia a otra capacidad "porque es más rápido" | Se rompe la regla de dependencia entre capacidades | Un puerto propio + un adaptador en el borde |
| Lógica de negocio en los adaptadores | El borde no se puede probar sin framework | Los adaptadores traducen y delegan; las reglas en Domain/Application |
| Devolver la entidad al exterior | Acopla a los consumidores al modelo | DTOs |
| Un `SharedKernel` que crece | Es el lugar donde todo depende de todo | Mantenerlo mínimo (un test lo limita a dos tipos) |
| Mismo puerto definido en el proveedor, no en el consumidor | El consumidor queda atado al proveedor | El consumidor define lo que necesita |
| Crear 3 proyectos para una capacidad trivial | Ceremonia sin beneficio | Capacidades triviales pueden ser una carpeta en un proyecto; sube a tres proyectos cuando haya reglas |

## 14. Límites de este sample (honestidad)

- **11 proyectos para un dominio diminuto.** Es el coste de que el compilador defienda las fronteras; en un caso tan pequeño es claramente desproporcionado y solo se justifica para enseñar.
- **Sin atomicidad entre capacidades.** `CancelAppointment` cancela y *después* cobra la tarifa; si el cobro fallara, la cita quedaría cancelada sin factura. Una solución real necesitaría una transacción, un outbox o una compensación (ver los kits de Event-Driven y Microservicios). No está implementado ni probado.
- **Las llamadas entre capacidades son síncronas y en proceso.** Si las capacidades pasaran a servicios, esos adaptadores serían clientes HTTP y aparecerían todos los problemas de red.
- **`Adapters` agrupa la persistencia y la web** en un proyecto por capacidad; podrían separarse.
- **Los tipos de petición HTTP son públicos** dentro de `Adapters` por simplicidad.
- **Todo en memoria**, horas en UTC.
- **Las reglas de nombres de carpetas siguen siendo heurísticas** (una lista de palabras).
- **El kernel compartido existe** (dos excepciones); es una concesión pragmática que otras variantes evitarían con una excepción por capacidad.

## 15. Checklist

- [ ] El primer nivel de `src/` grita el negocio
- [ ] Dentro de cada capacidad, tres círculos: Domain, Application, Adapters
- [ ] Domain y Application no conocen ASP.NET Core ni el contenedor de DI
- [ ] Los casos de uso se prueban con fakes de los puertos
- [ ] Ningún círculo interior referencia a otra capacidad
- [ ] Las capacidades se tocan solo en `Adapters → Application` del vecino
- [ ] El consumidor define los puertos; el vecino los implementa en su borde
- [ ] Lo que cruza una frontera es un DTO
- [ ] El grafo de capacidades no tiene ciclos
- [ ] Hay tests de arquitectura (de las dos ideas) y probé al menos una mutación
- [ ] Sé qué pasa si una operación entre capacidades falla a medias
