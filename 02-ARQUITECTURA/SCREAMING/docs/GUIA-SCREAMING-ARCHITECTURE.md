# Guía: Screaming Architecture paso a paso

Para un dev junior que quiere que, al abrir la carpeta de un proyecto, lo primero que se lea sea **qué hace el sistema** y no con qué framework está hecho. Es la arquitectura más barata de adoptar y la que más se olvida. El sample (la agenda de una clínica) tiene 42 tests, incluidas reglas de estructura que se comprueban contra un **mal ejemplo** para demostrar que muerden; sus binarios están analizados con REA a nivel de IL.

> Fuente: [Robert C. Martin – Screaming Architecture](https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html). Las citas están copiadas de ese artículo. Todo lo demás (las carpetas del sample, las reglas de los tests, la lista de palabras "técnicas") son **decisiones de este kit**, no parte del artículo.

---

## 1. Qué es

La pregunta del artículo es: *"So what does the architecture of your application scream?"* Su tesis, en sus propias palabras:

- *"Architectures are not (or should not) be about frameworks."*
- *"Frameworks are tools, not ways of life."*
- *"Your architectures should tell readers about the system, not about the frameworks you used in your system."*
- *"You shouldn't need the web server running in order to run your tests."*
- *"The fact that your application is delivered over the web is something you should defer."*

No es una arquitectura con piezas nuevas: es un **principio sobre qué comunica la estructura**. Se puede aplicar encima de cualquier otra (capas, Clean, Vertical Slice, Modular Monolith).

## 2. La prueba de los 5 segundos

Abre la carpeta del proyecto y lee los nombres de primer nivel.

```
Clinic.Technical/ (el MAL ejemplo)      Clinic/ (el bueno)
├─ Controllers/                         ├─ Appointments/
├─ Models/                              ├─ Billing/
├─ Repositories/                        ├─ Patients/
├─ Services/                            └─ Program.cs
└─ Program.cs
```

A la izquierda, ¿qué hace el sistema? No se sabe: podría ser una tienda, un banco, un blog. A la derecha: una clínica con pacientes, citas y facturación. **Eso** es gritar el negocio.

## 3. Por qué importa

| Con carpetas por rol técnico | Con carpetas por capacidad de negocio |
|---|---|
| Todos los proyectos del mundo se parecen | El árbol cuenta de qué trata el sistema |
| Para cambiar "cancelar cita" tocas 4 carpetas | Todo lo de cancelar citas está junto |
| Un recién llegado debe leer código para saber qué hace | Lee las carpetas y lo sabe |
| El framework parece la columna vertebral | El framework es un detalle del borde |
| Los casos de uso arrastran dependencias web | Los casos de uso se ejecutan sin servidor |

## 4. Estructura del sample

```
Clinic/
├─ Program.cs                 ← el cableado: aquí sí se ve el framework web
├─ ErrorHandling.cs           ← traducir fallos del negocio a HTTP
├─ Patients/                  ← capacidad: pacientes
│   ├─ RegisterPatient.cs     ← caso de uso (nombre = lo que hace)
│   ├─ GetPatient.cs
│   ├─ Patient.cs             ← el concepto y su registro
│   └─ PatientsEndpoints.cs   ← el único archivo "web" de esta carpeta
├─ Appointments/              ← capacidad: citas
│   ├─ BookAppointment.cs   CancelAppointment.cs   ListAppointments.cs
│   ├─ Appointment.cs         ← incluye IFeeCharger, lo que Citas necesita de Facturación
│   └─ AppointmentsEndpoints.cs
└─ Billing/                   ← capacidad: facturación
    ├─ ChargeLateCancellationFee.cs   ListInvoices.cs   Invoice.cs
    └─ BillingEndpoints.cs
```

Las dependencias entre capacidades van en **una sola dirección**:

```
Billing ──implementa lo que pide──► Appointments ──consulta──► Patients
```

Appointments define `IFeeCharger` ("cobra una cancelación tardía") sin saber que existe Billing; Billing lo implementa. Patients no depende de nadie.

## 5. Reglas y cómo se verifican

| Regla | Cómo se verifica |
|---|---|
| Las carpetas de primer nivel son capacidades del negocio (`Appointments`, `Billing`, `Patients`) | Test que lee el disco |
| No hay carpetas técnicas en la raíz (`Controllers`, `Services`, `Repositories`, `Models`, `Helpers`...) | Test (lista de palabras) |
| Esas mismas comprobaciones **detectan** el mal ejemplo | Test contra `Clinic.Technical` |
| Cada archivo de una capacidad vive en el namespace de esa capacidad | Test que lee los archivos |
| Cada capacidad tiene casos de uso con nombre de verbo (`Book…`, `Cancel…`) | Test |
| Las dependencias entre capacidades van en una sola dirección | Test sobre los `using` |
| El framework web solo aparece en `*Endpoints.cs`, `Program.cs` y `ErrorHandling.cs` | Test + REA |
| Los casos de uso no mencionan el framework web | Test + **REA a nivel de IL** |

## 6. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

1. **Escribe en una hoja las capacidades del negocio**, con los verbos que usaría un experto: "registrar pacientes", "reservar y cancelar citas", "cobrar penalizaciones". Esos nombres serán tus carpetas. Si te salen "capas", "manejadores", "utilidades", vuelve a empezar.
2. **Crea una carpeta por capacidad** (en singular o plural, pero siempre un sustantivo del negocio).
3. **Un archivo por caso de uso**, con nombre de verbo: `BookAppointment.cs`. La clase hace *una* cosa y se llama `Execute`.
4. **Los casos de uso no conocen el framework.** Reciben tipos simples y devuelven tipos del negocio o lanzan excepciones del negocio.
5. **Un archivo `…Endpoints.cs` por capacidad** contiene lo web: traduce la petición a una llamada al caso de uso.
6. **Las dependencias entre capacidades**: si A necesita algo de B, A define una interfaz con lo que *necesita* y B la implementa (así A no conoce a B).
7. **Tests de los casos de uso sin servidor**, y tests de estructura.

## 7. Las piezas clave

### Un caso de uso: reglas y nada más
```csharp
public sealed class CancelAppointment(AppointmentBook book, IFeeCharger fees, TimeProvider clock)
{
    public CancellationResult Execute(Guid appointmentId)
    {
        var appointment = book.Find(appointmentId) ?? throw new NotFoundException(...);
        if (appointment.Status == AppointmentStatus.Cancelled) throw new RuleViolationException("That appointment is already cancelled.");

        var late = appointment.Start - clock.GetUtcNow() < FreeCancellationNotice;   // 24 h
        book.Replace(appointment with { Status = AppointmentStatus.Cancelled });
        if (late) fees.ChargeLateCancellation(appointment.PatientId, appointment.Id);
        return new CancellationResult(..., late);
    }
}
```
Ni `HttpContext` ni `IResult` ni `Microsoft.AspNetCore`. Se prueba con `new CancelAppointment(...)`.

### El framework, en el borde
```csharp
// Appointments/AppointmentsEndpoints.cs: lo único "web" de la capacidad
app.MapPost("/appointments/{id:guid}/cancel", (Guid id, CancelAppointment cancel) => Results.Ok(cancel.Execute(id)));
```

### Lo que Citas pide, lo que Facturación entrega
```csharp
// Appointments/Appointment.cs
public interface IFeeCharger { void ChargeLateCancellation(Guid patientId, Guid appointmentId); }

// Billing/ChargeLateCancellationFee.cs
public sealed class ChargeLateCancellationFee(InvoiceBook invoices, TimeProvider clock) : IFeeCharger { ... }
```

## 8. Tests

| Qué | Dónde |
|---|---|
| Reglas de negocio **sin servidor web** | `UseCaseTests` (24 tests: horarios, doble reserva, cancelación tardía, límites de 24 h exactas...) |
| El cableado HTTP | `ApiTests` (con reloj fijo) |
| Qué grita la estructura | `ScreamingTests` |

Cita del artículo, hecha test: *"You shouldn't need the web server running in order to run your tests."* Los 24 tests de `UseCaseTests` nunca arrancan un servidor.

### Las reglas de estructura se prueban contra el mal ejemplo
`TheBadExample_IsRecognizedAsFrameworkShaped` apunta las mismas comprobaciones a `Clinic.Technical` y exige que **detecten** `Controllers`, `Models`, `Repositories` y `Services`. Si la detección se rompiera, el test de arriba pasaría por error. Es una prueba de que la regla no es decorativa.

### Pruebas de mutación (que los tests muerden)
En copias temporales, sin tocar el código real. Seis, todas atrapadas:

1. Aparece una carpeta `Services` en la raíz → fallan 2 tests de estructura.
2. `Patients` empieza a depender de `Appointments` → falla `Patients_DoesNotDependOnAnyOtherCapability`.
3. Un caso de uso importa `Microsoft.AspNetCore.Http` → fallan `TheUseCases_NeverMentionTheWebFramework` y `TheWebFramework_AppearsOnlyIn…`.
4. `Appointments` empieza a depender de `Billing` → falla `Appointments_DependsOnlyOnPatients`.
5. Se quita la regla del fin de semana → fallan los 2 casos de fin de semana.
6. Nunca se cobra la penalización → fallan 3 tests (dos de casos de uso y uno por HTTP).

(Mi primer test de "justo 24 horas" reservaba a las 08:00, antes de la apertura de la clínica, y falló por mi error en el test; lo corregí usando un reloj distinto en la cancelación.)

## 9. Verificación del binario con REA

REA analizó `Clinic.dll` (627 llamadas en IL) y agrupó las llamadas por clase:

| Hecho | Evidencia |
|---|---|
| Las 10 clases de casos de uso y almacenes (`RegisterPatient`, `BookAppointment`, `CancelAppointment`, `ChargeLateCancellationFee`...) hacen **0 llamadas** a `Microsoft.AspNetCore` | `inspect_managed_members`, `ev_3da47b6a1…082e` |
| Las llamadas al framework web están solo en `PatientsSetup` (2), `AppointmentsSetup` (3), `BillingSetup` (1) y `Program` (8) | mismo análisis |

Esto complementa al test de texto: un `using` sobrante en un caso de uso lo ve el test de texto (la mutación 3) pero **no** cambiaría el IL; REA mira lo que de verdad se llama. REA **no** prueba el comportamiento en ejecución.

## 10. Receta: añadir "reprogramar una cita"

1. Archivo nuevo `Appointments/RescheduleAppointment.cs` con `Execute(appointmentId, newStart)`: reutiliza las reglas de horario (idealmente extrae esas reglas a un método compartido de la capacidad).
2. Una línea en `AppointmentsEndpoints.cs` y en `AppointmentsSetup`.
3. **No hay carpeta nueva**: es una acción de una capacidad existente.
4. Tests en `UseCaseTests` (sin servidor).

Y si aparece una capacidad nueva ("Notifications"): una carpeta nueva, su namespace, y decide la dirección de su dependencia. Añádela a las listas del test de estructura (`Capabilities`, y el grafo permitido): ese cambio obligatorio es deliberado, para que nadie cree una capacidad sin pensarlo.

## 11. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| Una carpeta `Common`/`Shared`/`Utils` que crece sin control | Es el cajón de sastre: nadie sabe de quién es | Cada cosa en su capacidad; compartir lo mínimo |
| Dentro de cada capacidad, otra vez `Controllers/Services/Repositories` | Vuelves al problema un nivel más abajo | Archivos por caso de uso; subcarpetas solo si el negocio las nombra |
| Capacidades nombradas por entidad técnica (`Data`, `Entities`) | No dicen qué se hace | Nombrar por lo que el negocio hace |
| Nombres de casos de uso vagos (`PatientManager`, `AppointmentService`) | No dicen la acción | `BookAppointment`, `CancelAppointment` |
| Capacidades que se llaman en círculo | No se pueden entender por separado | Una dirección + interfaz que define el consumidor |
| Pensar que cambiar nombres de carpetas es suficiente | Si las clases siguen acopladas al framework, solo cambió la fachada | Casos de uso sin framework y probados sin servidor |
| Aplicarlo con una lista de palabras como único criterio | Una lista no entiende el negocio | Revisar con alguien del negocio: ¿reconoce su sistema? |

## 12. Límites de este sample (honestidad)

- **Las comprobaciones de nombres son heurísticas.** Una lista de palabras detecta el fallo común (`Controllers`, `Services`...), no entiende si "Appointments" es el mejor nombre. Lo que grita un sistema es en parte subjetivo.
- **Es un solo proyecto**: la frontera entre capacidades la comprueba un test de texto sobre los `using`, no el compilador. (En el kit de Modular Monolith la imponen proyectos separados y `internal`.)
- **Todo en memoria**, y las horas de la clínica están en UTC por simplicidad.
- **`Clinic.Technical` es deliberadamente incompleto y malo**: no se debe copiar.
- **El sample es pequeño**: no demuestra cómo escala la organización por capacidades con cientos de casos de uso.
- **El artículo no prescribe estas carpetas ni estas reglas**: la idea es suya; la implementación es de este kit.

## 13. Checklist

- [ ] Abro la carpeta y reconozco el negocio en 5 segundos
- [ ] No hay carpetas técnicas en la raíz (`Controllers`, `Services`...)
- [ ] Cada caso de uso es un archivo con nombre de verbo
- [ ] Los casos de uso no conocen el framework web y se prueban sin servidor
- [ ] El framework vive en archivos de borde (`*Endpoints`, `Program`)
- [ ] Las capacidades dependen unas de otras en una sola dirección
- [ ] Hay tests de estructura y comprobé que detectan un mal ejemplo
- [ ] Una persona del negocio reconocería su sistema en el árbol
