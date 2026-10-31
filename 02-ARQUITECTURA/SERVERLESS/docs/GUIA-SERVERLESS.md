# Guía: Arquitectura Serverless / FaaS

Para un dev junior. La idea: escribís **funciones chiquitas, sin estado**, y una **plataforma** se encarga de dispararlas (por HTTP, por un mensaje en una cola o por un reloj), reintentarlas, limitarlas y apagarlas. El sample es un procesador de pedidos (`Orders`) con tres funciones y una plataforma FaaS **simulada en tu máquina**. Tiene 76 tests, 13 mutaciones y sus binarios analizados con REA.

> **Qué es y qué NO es este kit.** Enseña el **modelo de programación y las reglas de diseño** de las funciones, probadas contra una plataforma local que imita lo esencial: disparadores, contexto por invocación, timeout, reintentos, cola de mensajes envenenados, concurrencia máxima e instancias nuevas o calientes. **No despliega nada en un proveedor**, no mide latencias reales de arranque en frío, no escala solo, no factura. Está explícito en la sección 12.
>
> **Fuentes y qué se verificó.** Citas copiadas de:
> - Martin Fowler, [Serverless Architectures](https://martinfowler.com/articles/serverless.html) (definición, estado, duración, arranque en frío, desventajas). Ojo: el artículo es viejo; **no uso sus cifras** (por ejemplo, los límites de tiempo de entonces).
> - Microsoft Learn: [Azure Functions best practices](https://learn.microsoft.com/en-us/azure/azure-functions/functions-best-practices), [Improve Azure Functions performance and reliability](https://learn.microsoft.com/en-us/azure/azure-functions/performance-reliability), [Queue storage trigger](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-queue-trigger) y [Reliable event processing](https://learn.microsoft.com/en-us/azure/azure-functions/functions-reliable-event-processing).
>
> Uso Azure Functions como referencia concreta porque su documentación es la que pude leer. Los nombres de este sample (`QueueMessage`, cola de mensajes muertos) son propios y **no son la API de Azure**.

---

## 1. La idea en una frase

Fowler: "Fundamentally, FaaS is about running backend code without managing your own server systems." Y describe la plataforma como "custom code run in managed, ephemeral containers on a “Functions as a Service” (FaaS) platform".

Dos palabras importan: **gestionados** (no operás servidores) y **efímeros** (la instancia que corre tu función puede desaparecer o aparecer en cualquier momento). De la segunda sale casi todo el diseño.

## 2. Las tres piezas

```
Faas.Runtime       La PLATAFORMA (lo que hace el proveedor): dispara, reintenta, limita, descarta. No sabe de pedidos.
Orders.Functions   Las FUNCIONES: PlaceOrder (HTTP), ReserveStock (cola), DailySummary (timer).
Orders.Local       La raíz de composición: el "func start" de tu laptop. Elige el store y registra cada función.
```

```
 HTTP  ──► PlaceOrder ──(mensaje "orders")──► ReserveStock ──(evento "order-events")──►  ...
                │                                  │
                ▼                                  ▼
            IOrderStore  ◄────────────────  IOrderStore            DailySummary (timer) ──► "reports"
            (todo el estado vive acá, fuera de las funciones)
```

Las funciones **no se llaman entre sí**: se comunican por colas. Microsoft: "it's best to use storage queues for cross-function communication."

## 3. Los tres disparadores

| Disparador | Función | Qué hace la plataforma si falla |
|---|---|---|
| HTTP `POST /orders` | `PlaceOrder` | Responde 500 genérico (nunca filtra el mensaje de la excepción) o 504 si se pasa del timeout. **No reintenta**: reintenta el cliente |
| Cola `orders` | `ReserveStock` | Reentrega el mensaje; tras 3 entregas lo manda a mensajes muertos |
| Timer | `DailySummary` | Lo registra y **no reintenta**: el próximo tick es el reintento |

## 4. Las reglas de una función serverless

### 4.1 Sin estado
Fowler: "you should not assume that state from one invocation of a function will be available to another invocation." Microsoft: "Functions should be stateless and idempotent if possible. Associate any required state information with your data."

En el sample, **todo** lo que debe sobrevivir vive detrás de un puerto (`IOrderStore`). Las funciones tienen un único campo, de solo lectura, con ese puerto. Dos redes:
- Test de arquitectura: `FunctionsKeepNoStateBetweenInvocations` (campos de solo lectura, sin colecciones mutables).
- Test de comportamiento: `TheResultIsTheSame_WhetherThePlatformKeepsInstancesWarmOrCreatesANewOneEveryTime` corre el mismo escenario con instancias calientes y con una instancia nueva por invocación. El resultado debe ser **idéntico**; solo cambia cuántas instancias se crearon.

### 4.2 Idempotentes
La plataforma entrega **al menos una vez**. Microsoft: "Functions guarantees *at-least-once* delivery: Your code and dependent systems might need to account for the fact that the same event could be processed twice." Y: "If a queue item was already processed, allow your function to be a no-op."

En el sample hay dos niveles:
1. `PlaceOrder`: el header `Idempotency-Key` es el id del pedido. La misma petición dos veces crea un pedido y manda un mensaje (la segunda responde 200).
2. `ReserveStock`: `ReserveOnceAsync` reserva el stock **como mucho una vez por mensaje y una vez por pedido**, en un solo paso atómico. Un test lanza 100 copias del mismo mensaje en paralelo y el stock baja una sola vez.

La plataforma **no deduplica** a propósito (`TheSameMessageSentTwice_IsDeliveredTwice_...`): esconder los duplicados haría que tus funciones nunca los vean en desarrollo y fallen en producción.

### 4.3 Cortas: respondé rápido y dejá el trabajo para la cola
Microsoft: "You can pass the HTTP trigger payload into a queue to be processed by a queue trigger function. This approach lets you defer the actual work and return an immediate response." Eso hace `PlaceOrder`: valida, guarda y responde `202 Accepted`; el stock se reserva después. Un test lo prueba: tras el POST el stock no cambió y hay un mensaje pendiente.

### 4.4 Terminan lo que empiezan
Microsoft: "When your function starts any tasks, callbacks, threads, processes, they must complete before your function code returns." Una función que lanza trabajo en segundo plano puede perderlo cuando la instancia se apague. El test `FunctionsDoNotStartBackgroundWork` prohíbe `Task.Run`, hilos y `.Result`/`.Wait()`.

### 4.5 Defensivas
Microsoft: "Assume your function could encounter an exception at any time." Distinguí:
- **Resultado de negocio**: "sin stock" no es un fallo; se anuncia (`OrderRejected`) y el mensaje se completa.
- **Fallo técnico o mensaje corrupto**: lanzá la excepción. La plataforma reintenta y, si no hay caso, lo aparta.

### 4.6 El reloj y el mundo entran por parámetro
Las funciones leen la hora de `context.Now`, no de `DateTime.UtcNow`, y no tocan archivos, consola ni red: usan puertos. Así son deterministas y se prueban llamándolas como a cualquier método (`OrderFunctionTests`). Dos tests lo vigilan (`FunctionsReadTheTimeFromTheContext`, `FunctionsTouchNoFilesConsoleOrEnvironment`).

## 5. Qué hace la plataforma

| Capacidad | Cómo se ve en el sample | Referencia |
|---|---|---|
| Contexto por invocación | Un `FunctionContext` nuevo con `InvocationId`, `Attempt`, `Now` y un token de cancelación | test `EveryInvocationGetsItsOwnContext` |
| Timeout | La plataforma deja de esperar y cuenta un fallo, aunque la función ignore el token | `AnHttpFunctionThatIsTooSlowBecomes504_...` |
| Reintentos y mensajes muertos | Una entrega más tras cada fallo, hasta `MaxDeliveryCount` (3) | Microsoft: tras los intentos fallidos "the functions runtime adds a message to a queue named *&lt;originalqueuename&gt;-poison*" |
| Salidas solo si hubo éxito | Los mensajes de salida se envían **después** de que la función termina bien; lo que devuelve una función que ya expiró nunca se envía | `WhatAFunctionReturnsTooLate_IsNeverSent` |
| Concurrencia máxima | `MaxConcurrency` limita cuántos mensajes de una función corren a la vez | `MaxConcurrencyLimitsHowManyMessagesRunAtOnce` |
| Instancias | Caliente (una instancia reutilizada) o "fría" (una nueva por invocación) | `AWarmPlatformCreatesOneInstance_...` |

Fowler describe el arranque en frío como "creating a new container instance, starting the function host process, etc." Este sample **cuenta** cuántas instancias se crean, pero no simula el tiempo que cuesta.

## 6. Reglas y cómo se verifican

| Regla | Test |
|---|---|
| La plataforma solo referencia el framework | `ThePlatformReferencesOnlyTheFramework` |
| Las funciones referencian los contratos de la plataforma, no la raíz de composición | `TheFunctionsReferenceThePlatformContracts_NeverTheCompositionRoot` |
| Lo mismo en los `.csproj` | `TheProjectFilesSayTheSame` |
| La plataforma no conoce el dominio | `ThePlatformKnowsNothingAboutOrders` |
| Hay tres funciones, una por disparador, públicas y selladas | `ThereAreThreeFunctions_...` |
| Sin estado entre invocaciones | `FunctionsKeepNoStateBetweenInvocations` |
| Reciben solo puertos (interfaces) por constructor | `FunctionsReceiveOnlyPortsThroughTheirConstructor` |
| Ninguna menciona a otra función | `NoFunctionMentionsAnotherFunction_...` |
| No conocen el host ni un adaptador concreto | `FunctionsDoNotKnowThePlatformHostNorAnyConcreteAdapter` |
| El reloj viene del contexto | `FunctionsReadTheTimeFromTheContext` |
| Sin trabajo en segundo plano | `FunctionsDoNotStartBackgroundWork` |
| Sin E/S propia | `FunctionsTouchNoFilesConsoleOrEnvironment` |
| Solo la raíz registra funciones | `OnlyTheCompositionRootRegistersFunctions` |

## 7. Paso a paso para crear el tuyo

1. **Elegí disparador y contrato.** ¿Llega por HTTP, por un mensaje o por un reloj? Eso decide la interfaz.
2. **Sacá el estado afuera.** Definí un puerto para todo lo que debe sobrevivir entre invocaciones.
3. **Escribí la función como un método**: entra un evento y un contexto, sale un resultado y mensajes de salida. Probala llamándola directamente.
4. **Hacela idempotente.** Preguntate: ¿qué pasa si me llega dos veces? ¿Y si me cuelgo justo después de escribir?
5. **Decidí qué es negocio y qué es fallo.** Lo primero se responde; lo segundo se lanza.
6. **Registrala en la raíz de composición** con sus límites (timeout, entregas, concurrencia).
7. **Agregá las reglas de arquitectura como tests** antes de que el proyecto crezca.

```bash
cd sample
dotnet build
dotnet run --project src/Orders.Local -- demo
dotnet run --project src/Orders.Local -- demo --recycle
dotnet test
```

Salida real del primero (resumida):

```
POST /orders [k1] -> 202 {"orderId":"k1"}
POST /orders [k1] -> 200 {"orderId":"k1","status":"already received"}
POST /orders [k3] -> 422 {"error":"customer, sku and a positive quantity are required"}
Drained the queues in 6 invocations
Dead letter [poison-1] after 3 deliveries: malformed message body
Stock: ABC-1=3 XYZ-2=1
Instances created: PlaceOrder=1 ReserveStock=1 DailySummary=1
```

Con `--recycle` todo es igual salvo la última línea: `PlaceOrder=5 ReserveStock=6 DailySummary=1`.

## 8. Receta: agregar una función nueva

Querés enviar un correo cuando un pedido se reserva.
1. `NotifyCustomerFunction : IQueueFunction` en `Orders.Functions`, que reciba un puerto `INotifier`.
2. Sus tests llamándola como método. Probá el mismo mensaje dos veces: ¿manda dos correos? Si sí, necesitás una marca de "ya enviado" detrás del puerto.
3. En `LocalApp.cs`, `.AddQueue("NotifyCustomer", "order-events", () => new NotifyCustomerFunction(notifier))`.

`ReserveStock` no cambia: ni se entera de que hay un consumidor nuevo. Los tests de arquitectura te avisan si la tocás de más (`ThereAreThreeFunctions_...` va a pedir actualizar la lista a propósito).

## 9. Un bug que encontraron los tests

Un mensaje cuyo cuerpo era `[]` (un arreglo JSON, válido pero sin sentido) hacía lanzar a `ReserveStock` una excepción **del framework** ("The requested operation requires an element of type 'Object'...") en vez de "malformed message body". Terminaba igual en mensajes muertos, pero con un motivo inútil para quien lo investiga. El test `ReserveStock_AMalformedMessage_Throws` lo detectó y ahora se comprueba que la raíz sea un objeto.

## 10. Pruebas de mutación (que las reglas muerden)

Cada una se aplicó en una copia temporal y se corrió la suite completa. Las 13 se detectaron.

| # | Mutación | Cayó |
|---|---|---|
| M1 | Una función nombra un adaptador concreto | `FunctionsDoNotKnowThePlatformHostNorAnyConcreteAdapter` |
| M2 | Una función guarda un contador entre invocaciones | `FunctionsKeepNoStateBetweenInvocations` |
| M3 | `PlaceOrder` ignora la clave de idempotencia | 3 tests |
| M4 | El store olvida lo ya procesado | 6 tests (incluye 100 copias en paralelo) |
| M5 | El store puede vender de más | 6 tests |
| M6 | El descarte a mensajes muertos tiene un error de uno | 6 tests |
| M7 | La plataforma nunca recicla instancias | 3 tests |
| M8 | La plataforma ignora `MaxConcurrency` | `MaxConcurrencyLimitsHowManyMessagesRunAtOnce` |
| M9 | La plataforma no aplica el timeout | 4 tests |
| M10 | Un HTTP 500 filtra el mensaje de la excepción | `AThrowingHttpFunctionBecomes500_...` |
| M11 | `DailySummary` lee el reloj del sistema | 3 tests |
| M12 | Una función lanza trabajo en segundo plano | `FunctionsDoNotStartBackgroundWork` |
| M13 | La cola deduplica mensajes en silencio | 4 tests |

Un detalle de método: la primera versión de M10 usaba una cadena que no compilaba; salió como "error de build", no como "detectada". La reescribí hasta que compilara y la prueba la atrapara. Una mutación que no compila no demuestra nada.

Los tests de timeout esperan entre 50 y 500 ms reales. Se corrió la suite completa 6 veces seguidas sin fallos, pero es la zona donde podría aparecer intermitencia en una máquina muy cargada.

## 11. Verificación del binario con REA

REA inspecciona los `.dll` compilados sin ejecutarlos. Es evidencia **estática**.

| Pregunta | Evidencia |
|---|---|
| ¿Qué referencia la plataforma? | `Faas.Runtime.dll` (`ev_d1e3ebde93b61b391bd16cfe1cddb82256b415b9bfbc4dade96d7397aa899b57`): `System.Runtime`, `System.Collections`, `System.Linq`, `System.Threading`. Ninguna `Orders.*` |
| ¿Qué referencian las funciones? | `Orders.Functions.dll` (`ev_a80ad72439b07ee21a3550980a930a7d20a76a3aa84c159d2d42427f8201f714`): `System.Runtime`, **`Faas.Runtime`**, `System.Text.Json`, `System.Collections`, `System.Linq`. No `Orders.Local` |
| ¿Qué usan de la plataforma? | Miembros de `Orders.Functions.dll` (`ev_2158a4a16cfd2774270ad6f203e5bfd702125f23732c883faac4c13c98c4bd50`, 298 aristas de llamada): solo contratos (`HttpRequest`, `HttpResponse`, `QueueMessage`, `OutgoingMessage`, `Outputs`, `FunctionContext.Now`). Ninguna llamada a `FunctionHost` ni al bus |
| ¿Hacen E/S o trabajo propio? | Entre las llamadas a `System.*` de las funciones no aparece `Task.Run`, hilos, `File`, `Console`, `HttpClient` ni `DateTime`/`DateTimeOffset.UtcNow`. La única llamada a `DateTimeOffset` es `ToString`, para formatear `context.Now` |
| ¿Tienen estado? | Los campos de las funciones son `_store` (privado, solo lectura) y, en `PlaceOrder`, `Json` (privado, estático, solo lectura). Los demás campos son de las máquinas de estado que genera el compilador |

Límite: es lectura de metadatos e IL; no prueba qué pasa al ejecutar.

## 12. Cuándo sí y cuándo no, y límites de este sample

### Cuándo sí
- Cargas **dirigidas por eventos** e intermitentes: webhooks, procesar un archivo cuando llega, una tarea cada noche, colas de trabajo.
- Cuando la **elasticidad** y no operar servidores valen más que el control fino.

### Cuándo no, según Fowler
- "FaaS functions are typically limited in how long each invocation is allowed to run." No sirve para procesos largos sin partirlos.
- "Integration testing Serverless apps, on the other hand, is hard." Y depurar en producción "is a different story".
- Dependencia del proveedor: "whatever Serverless features you’re using from one vendor will be implemented differently by another vendor."

### Límites de este sample (honestidad)
- **No es una nube.** Todo corre en un proceso, en memoria. No hay latencia de arranque en frío medida, ni autoescalado, ni facturación por uso, ni límites reales de memoria o duración.
- **El timeout no mata la función.** .NET no puede matar una tarea: si la función ignora el token, sigue corriendo en segundo plano aunque la plataforma ya la dio por fallida. Una plataforma real mata la instancia.
- **El problema del outbox queda sin resolver (y está fijado en un test).** Si la función se cuelga entre "reservé el stock" y "anuncié el evento", el reintento ve que ya estaba hecho y **no anuncia nada**: el stock está bien y el evento se perdió (`KnownLimit_ACrashBetweenTheEffectAndTheAnnouncement_LosesTheEvent`). Se resuelve guardando el evento a enviar en la misma operación atómica que el efecto (patrón *outbox*). No está implementado.
- **Colas sin visibilidad ni orden.** Un mensaje que falla vuelve al final de la cola al instante (como el `visibilityTimeout` por defecto de Azure, que según Microsoft es cero), sin espera creciente y sin conservar el orden. No hay particiones ni lotes.
- **Mensajes duplicados en paralelo.** Con `MaxConcurrency` mayor que 1, dos copias del mismo mensaje pueden ejecutarse a la vez. Microsoft dice que su disparador de colas "automatically prevents a function from processing a queue message multiple times simultaneously"; **este sample no lo replica**. Lo cubre la atomicidad del store (test de 100 copias en paralelo).
- **Los mensajes muertos nadie los consume.** Quedan en una lista para inspeccionarlos; no hay una función que los procese ni alertas.
- **Sin seguridad.** No hay autenticación en el HTTP, ni secretos, ni permisos mínimos por función.
- **Observabilidad mínima.** Solo el registro de invocaciones (función, intento, resultado, error).
- **Sin orquestación.** No hay flujos de varios pasos con estado (lo que Microsoft cubre con Durable Functions o Logic Apps).
- **Los adaptadores son de juguete.** El store en memoria no es una base de datos; solo respeta la atomicidad que el diseño necesita.

## 13. Errores comunes

- **Estado en la instancia** ("lo guardo en un campo, total siempre es la misma instancia"). Funciona hasta que la plataforma crea otra.
- **Asumir que el mensaje llega una vez.** Llega al menos una vez.
- **Una función que llama a otra por HTTP** y se queda esperando. Usá una cola.
- **Hacer todo en el request.** Trabajo largo dentro de la petición HTTP: timeouts y reintentos del cliente.
- **Lanzar con `Task.Run` y devolver.** El trabajo puede morir con la instancia.
- **Leer `DateTime.UtcNow` o variables de entorno dentro de la función.** Hace imposible probarla igual en todas partes.
- **Tratar "sin stock" como excepción.** Genera reintentos inútiles y mensajes muertos.
- **Acoplarte al proveedor en todos lados.** Mantené la lógica detrás de puertos y dejá en la función solo el pegamento con el disparador.

## 14. Checklist

- [ ] Cada función se prueba llamándola como un método.
- [ ] Ninguna guarda estado en la instancia.
- [ ] Sé qué pasa si el mismo mensaje llega dos veces (y tengo un test).
- [ ] Sé qué pasa si me cuelgo justo después de escribir (y qué dice mi diseño sobre los eventos).
- [ ] Lo lento va a una cola; el HTTP responde rápido.
- [ ] Resultado de negocio ≠ fallo técnico.
- [ ] El reloj y la E/S entran por parámetro o por puerto.
- [ ] Funciona igual con instancias calientes y con una nueva cada vez.
- [ ] Hay un lugar donde terminan los mensajes que nadie puede procesar, y alguien lo mira.
