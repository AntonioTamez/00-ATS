# Guía: Arquitectura Pipes & Filters

Para un dev junior. La idea: en vez de un procesamiento gigante que lo hace todo, **una cadena de pasos pequeños e independientes (filtros)**, unidos por **tuberías (pipes)** por donde viajan los datos. El sample importa un CSV de ventas (`Sales`): parsea, valida, descarta duplicados y SKUs bloqueados, y calcula el IVA. Tiene 71 tests, 12 mutaciones y sus binarios analizados con REA.

> **Fuentes y qué se verificó.** Citas copiadas de:
> - [Azure Architecture Center – Pipes and Filters pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/pipes-and-filters) (problema, solución, consideraciones, cuándo sí y cuándo no).
> - Wikipedia, [Pipeline (software)](https://en.wikipedia.org/wiki/Pipeline_(software)) (definición, buffering).
> - [Microsoft Learn – `yield` statement](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/yield) (los iteradores no corren hasta que alguien itera).
>
> Intenté también la página de TPL Dataflow de Microsoft Learn, pero el resumen que obtuve no trajo citas utilizables sobre contrapresión, así que **no cito nada de ella**. La contrapresión de este sample se explica con lo que el código hace y con los tests que lo prueban.

---

## 1. La idea en una frase

Microsoft describe el patrón así: "Break down the processing required for each stream into a set of separate components (or filters), each performing a single task. [...] The filters are composed into pipelines by connecting the filters with pipes. Filters are independent, self-contained, and typically stateless."

Y sobre las tuberías: "Pipes don't perform routing or any other logic. They only connect filters, passing the output message from one filter as the input to the next."

Wikipedia lo resume: "a pipeline consists of a chain of processing elements", y aclara que "This is also called the pipe(s) and filters design pattern".

Las dos propiedades que definen el estilo:

| Propiedad | En este sample |
|---|---|
| Cada filtro conoce **solo** su tipo de entrada y de salida | `ParseCsvFilter` es `IFilter<SourceLine, RawSale>`; no sabe quién va antes ni después |
| Las tuberías no tienen lógica | Una tubería es un `IAsyncEnumerable<T>`: solo pasa elementos |

## 2. Las piezas

```
Sales.Pipeline   El NÚCLEO genérico: IFilter, Pipeline<TIn,TOut>, el buffer. No sabe de ventas.
Sales.Filters    Los FILTROS del dominio: parse-csv, validate-sale, deduplicate, blocked-skus, apply-vat.
Sales.Host       La RAÍZ DE COMPOSICIÓN: la única que nombra los filtros y decide el orden.
```

```
   fuente ──pipe──► parse-csv ──pipe──► validate-sale ──pipe──► deduplicate ──pipe──► blocked-skus ──pipe──► apply-vat ──► salida
   (líneas)         SourceLine→RawSale   RawSale→Sale            Sale→Sale             Sale→Sale              Sale→PricedSale

   Sales.Host ───► Sales.Filters ───► Sales.Pipeline        (los filtros no se conocen entre sí)
```

Los mensajes que viajan (`SourceLine`, `RawSale`, `Sale`, `PricedSale`) son los **esquemas** de las tuberías. Microsoft: "the filters can be arranged in any order so long as the input schema for any filter matches the output schema for the previous filter".

En este sample esa regla la hace cumplir **el compilador**: `Then<TNext>(IFilter<TOut, TNext> filter)` solo acepta un filtro cuya entrada sea la salida actual del pipeline. Una cadena con esquemas que no encajan no compila. Un test (`ThenOnlyAcceptsAFilterWhoseInputIsThePipelinesOutput`) fija esa firma.

## 3. Cómo viaja un dato (y por qué no se carga todo en memoria)

Cada tubería es un `IAsyncEnumerable<T>` y cada filtro lo lee con `await foreach`. Microsoft sobre los iteradores: "Calling an iterator doesn't execute it immediately". Nada corre hasta que alguien pide el siguiente elemento.

Consecuencias, **todas con test**:

| Qué pasa | Test |
|---|---|
| Hacer `Start(...)` no lee nada de la fuente | `NothingIsReadFromTheSourceUntilSomeoneEnumeratesTheOutput` |
| Pedir 2 elementos de 1000 lee como mucho 3, y libera la fuente | `AskingForTwoItemsOutOfAThousand_PullsAtMostThree_AndReleasesTheSource` |
| Cada elemento atraviesa todos los filtros antes de leer el siguiente (como mucho 1 en vuelo con 5000 elementos) | `EachItemTravelsThroughAllTheFiltersBeforeTheNextOneIsRead` |
| 20 000 líneas por el pipeline real, 1 en vuelo | `AHugeFile_FlowsThroughWithoutBeingLoadedFirst` |

Esto es **contrapresión natural**: el consumidor tira (*pull*) y si es lento, toda la cadena espera. La memoria no crece con el tamaño del archivo. Un filtro que acumulara todo antes de emitir rompería esa propiedad (mutación M3).

## 4. Las decisiones de diseño

### 4.1 Rechazar no es fallar
Un dato malo es parte normal del flujo. Un filtro lo **rechaza** con `rejects.Reject(item, reason)` y sigue con el siguiente. El pipeline ata el nombre del filtro al rechazo, así que el resultado dice quién descartó qué y por qué:

```
[blocked-skus] line 4: sku 'BAN-001' is blocked
[validate-sale] line 5: quantity 'x' is not a positive integer
[deduplicate] line 7: duplicate id '1001' (first seen at line 2)
```

Una **excepción** es otra cosa: es un filtro roto. Microsoft avisa: "if a filter or pipe fails, the whole pipeline is likely to fail". Acá, una excepción de un filtro termina el run con una `PipelineException` que nombra al filtro (`Filter == "boom"`). Cada filtro "must determine what to do in the case of a breaking error. The filter must determine if it fails the pipeline or propagates the exception"; este sample elige: **datos malos se rechazan, bugs tumban el run**.

### 4.2 Los filtros no guardan estado entre ejecuciones
Microsoft: filtros "typically stateless" y, para los que no lo son, "filters in a pipeline should be designed to be idempotent". `DeduplicateFilter` necesita memoria (los ids ya vistos), pero la crea **dentro de `Run`**, no en un campo. Así la misma instancia sirve para cualquier cantidad de ejecuciones. Hay dos redes:
- Test de comportamiento: `Deduplicate_RemembersNothingBetweenRuns` y `RunningTheSamePipelineTwice_GivesTheSameResult`.
- Test de arquitectura: ningún filtro tiene campos mutables ni colecciones mutables (`FiltersHoldNoMutableState_...`).

### 4.3 El orden lo decide la raíz de composición
`Sales.Host/SalesPipelines.cs` es el único lugar con `.Then(...)`. Hay dos pipelines (`Standard` y `Quick`) que **reutilizan los mismos filtros** en distinto orden y cantidad. Cambiar un pipeline es tocar ese archivo y nada más. Un test (`OnlyTheCompositionRootChainsFilters`) vigila que ni el núcleo ni los filtros encadenen.

### 4.4 La tubería con buffer
`Buffer(n)` pone una tubería con capacidad `n` (un `Channel` acotado): los filtros de arriba corren en su propia tarea, hasta `n` elementos adelantados. Si el buffer se llena, el productor espera. Wikipedia: "Usually some amount of buffering is provided between consecutive elements".

Qué se probó:
- Se adelanta, pero solo hasta su capacidad (`ABufferedPipe_LetsTheProducerRunAheadButOnlyByItsCapacity`).
- Si el consumidor abandona, el productor para y suelta la fuente (`..._StopsTheProducerWhenTheConsumerWalksAway`).
- Conserva todos los elementos y su orden, y un fallo de arriba llega al consumidor.

**Efecto observado al correrlo**: con buffer, el *orden* en que aparecen los rechazos deja de ser el de las líneas (dos etapas corren a la vez). El test `ABufferedPipeline_Produces...ThoughNotNecessarilyInTheSameOrder` compara los rechazos ordenados, no en secuencia. Es el precio del paralelismo.

## 5. Reglas y cómo se verifican

| Regla | Cómo se verifica |
|---|---|
| El núcleo solo referencia el framework | `TheCoreReferencesOnlyTheFramework` (lee las referencias del ensamblado con `PEReader`) |
| Los filtros referencian el núcleo, no la composición | `TheFiltersReferenceTheCore_NeverTheComposition` |
| Lo mismo en los `.csproj` | `TheProjectFilesSayTheSame` |
| El núcleo no conoce el dominio | `TheCoreKnowsNothingAboutSales` (escaneo de fuente) |
| Cada filtro es público y sellado, y son exactamente cinco | `ThereAreFiveFilters_EachPublicAndSealed` |
| Ningún filtro nombra a otro | `NoFilterMentionsAnotherFilter` |
| Los filtros no tienen estado mutable | `FiltersHoldNoMutableState_...` |
| Los nombres de etapa son únicos y en kebab-case | `FilterNamesAreUniqueKebabCase` |
| Solo la raíz encadena | `OnlyTheCompositionRootChainsFilters` |
| `Then` exige que los esquemas encajen | `ThenOnlyAcceptsAFilterWhoseInputIsThePipelinesOutput` |

## 6. Paso a paso para crear el tuyo

1. **Escribí los esquemas.** ¿Qué tipo entra y cuál sale en cada paso? Un `record` inmutable por esquema (`Messages.cs`).
2. **Definí el contrato del filtro**: `IFilter<TIn, TOut>` con `Name` y `Run(input, rejects, ct)`. Es lo único que el núcleo conoce.
3. **Escribí cada filtro solo**, con tests que lo corren sin ningún otro (helper `RunAlone`). Si para probar un filtro necesitás otro, están acoplados.
4. **Decidí qué es rechazo y qué es excepción.** Dato inválido: `Reject`. Bug o recurso caído: dejá que lance.
5. **Armá el pipeline en la raíz de composición**, no en los filtros.
6. **Agregá las reglas de arquitectura como tests** (sección 5) antes de que el proyecto crezca.
7. **Probá lo que no se ve**: pereza, tamaño de memoria, cancelación, liberación de la fuente.

```bash
cd sample
dotnet build
dotnet run --project src/Sales.Host -- data/sales.csv
dotnet run --project src/Sales.Host -- data/sales.csv --quick --buffer 4
dotnet test
```

Salida real del primero:

```
Pipeline: parse-csv -> validate-sale -> deduplicate -> blocked-skus -> apply-vat
  line  2  1001  ABC-100  x2   AR  net    20.00  vat    4.20  total    24.20
  line  3  1002  ABC-200  x1   ES  net    99.90  vat   20.98  total   120.88
  line  6  1005  XYZ-300  x3   UY  net     1.50  vat    0.33  total     1.83
  line 12  1009  ABC-400  x10  CL  net    11.50  vat    2.19  total    13.69
Accepted: 4  Rejected: 6
...
Totals: net 132.90  vat 27.70  total 160.60
```

Códigos de salida: `0` todo aceptado, `1` hubo rechazos, `2` uso incorrecto, `3` un filtro falló.

## 7. Receta: agregar un filtro y reordenar

**Un filtro nuevo** (por ejemplo, descuento por cantidad `Sale → Sale`):
1. Creá `QuantityDiscountFilter : IFilter<Sale, Sale>` en `Sales.Filters`, sin mencionar otros filtros.
2. Sus tests con `RunAlone`.
3. En `SalesPipelines.cs`, un `.Then(new QuantityDiscountFilter())` donde corresponda.

Los tests de arquitectura te dicen si tocaste algo que no debías (`ThereAreFiveFilters_...` fallará: actualizá esa lista a propósito, es tu aviso de que el catálogo cambió).

**Reordenar**: `DeduplicateFilter` y `BlockedSkusFilter` son ambos `Sale → Sale`, así que se pueden intercambiar. El test `ReorderingTwoFiltersThatShareASchema_GivesTheSameResult` lo prueba con el archivo real: mismas ventas, mismos rechazos. Ojo, que dos filtros encajen en tipos **no garantiza** que el resultado sea el mismo: depende de qué hacen. Por ejemplo, `ParseCsvFilter` no puede ir después de `ValidateSaleFilter` (los tipos no encajan), pero un filtro `Sale → Sale` que *suma 1 a la cantidad* y otro que *descarta cantidades mayores a 5* sí encajan y **no conmutan**. El compilador cuida los tipos; el significado lo cuidás vos.

## 8. Pruebas de mutación (que las reglas muerden)

Cada una se aplicó en una copia temporal del sample y se corrió la suite completa. Las 12 se detectaron.

| # | Mutación | Cayó |
|---|---|---|
| M1 | Un filtro referencia a otro filtro | `NoFilterMentionsAnotherFilter` |
| M2 | El núcleo trata especial a un filtro del dominio | `TheCoreKnowsNothingAboutSales` |
| M3 | El núcleo materializa todo el flujo antes de emitir | 11 tests (pereza, 1 en vuelo, cancelación, errores) |
| M4 | Los fallos ya no se atribuyen a un filtro | 4 tests (`PipelineException`, CLI con código 3) |
| M5 | `Deduplicate` guarda su memoria en un campo | 3 tests (campo mutable, dos ejecuciones) |
| M6 | El buffer es ilimitado | `ABufferedPipe_LetsTheProducerRunAheadButOnlyByItsCapacity` |
| M7 | El buffer nunca detiene a su productor | `..._StopsTheProducerWhenTheConsumerWalksAway` |
| M8 | `Validate` descarta una cantidad mala sin avisar | 7 tests |
| M9 | Los runs comparten un solo registro de rechazos | 22 tests |
| M10 | El IVA redondea half-to-even | `Vat_RoundsHalvesAwayFromZero` + 2 de integración |
| M11 | El encabezado se salta en cualquier línea | `ParseCsv_OnlyTheFirstLineCanBeTheHeader` |
| M12 | El CLI siempre sale con 0 | `Cli_OnTheSampleFile_...ExitsWithOne` |

Los tests de buffer dependen de tiempos (esperan 300 ms para ver hasta dónde se adelanta el productor). Se corrió la suite completa seis veces seguidas sin fallos, pero es la zona donde un test podría volverse intermitente en una máquina muy cargada.

## 9. Verificación del binario con REA

REA inspecciona los `.dll` ya compilados sin ejecutarlos. Es evidencia **estática**.

| Pregunta | Evidencia |
|---|---|
| ¿Qué referencia el núcleo? | `Sales.Pipeline.dll` (`ev_228c78f1c104f829a25cbe6c8e5e14d3d8cd686ff5a41f20f36c28d0d80b047b`): `System.Runtime`, `System.Collections`, `System.Threading.Channels`. Ninguna `Sales.*` |
| ¿Qué referencian los filtros? | `Sales.Filters.dll` (`ev_ea5f2aa82b43e1aa4e31d93a92685bc40fe9c3d573f6ddfc343fd0836ea8f587`): `System.Runtime`, **`Sales.Pipeline`**, `System.Collections`, `System.Linq`, `System.Linq.AsyncEnumerable`. No `Sales.Host` |
| ¿Se llaman entre sí? | Miembros de `Sales.Filters.dll` (`ev_f984d7d5fa48a0609f3f73addc853ed870798b56d49f2b29ca76bd82ace1d67a`): 714 aristas de llamada; **ninguna** apunta a un tipo `*Filter` (los únicos tipos de `Sales.Filters` que reciben llamadas son los mensajes `SourceLine`, `RawSale`, `Sale`, `PricedSale`) |
| ¿Qué usan del núcleo? | El único miembro de `Sales.Pipeline` al que llaman los filtros es `IRejectSink.Reject` |
| ¿Hay estado de instancia? | Los únicos campos de instancia de los filtros son `ApplyVatFilter._rateByCountry` y `BlockedSkusFilter._blocked`, ambos de solo lectura (flags `initonly`); `Columns` es una constante |

Límite: los nombres de los métodos de las máquinas de estado generadas por el compilador (`<Run>d__2`...) aparecen sin el tipo contenedor en los datos de REA, así que el "ninguna arista entre filtros" se apoya en que **ningún destino** es un tipo `*Filter`, no en identificar al llamador.

## 10. Cuándo sí y cuándo no

Microsoft recomienda el patrón cuando:
- "The processing required by an application can easily be broken down into a set of independent steps."
- "The processing steps performed by an application have different scalability requirements."
- "You require the flexibility to allow reordering of the processing steps the application performs, or to allow the capability to add and remove steps."

Y avisa que puede no servir cuando:
- "The application follows a request-response pattern."
- "The processing steps performed by an application aren't independent, or they have to be performed together as part of a single transaction."
- "The amount of context or state information a step requires makes this approach inefficient."

Traducido: imports de archivos, ETL, procesamiento de imágenes, ingesta de eventos, compiladores. **No** para una API que responde en el mismo request ni para pasos que dependen mucho unos de otros.

## 11. Errores comunes

- **Filtros que se hablan.** Si `Validate` llama a `Deduplicate`, ya no son piezas independientes.
- **Un filtro que lo hace todo.** "Composite tasks should use multiple filters rather than one."
- **Cargar todo en una lista** entre filtros "para simplificar": perdés la memoria constante y la cancelación temprana.
- **Estado en el filtro.** Funciona en el primer run y falla en el segundo.
- **Tragarse los errores.** Un `catch` que ignora una excepción oculta un bug como si fuera un dato malo.
- **Rechazar sin motivo.** Un rechazo sin razón y sin referencia al dato original es imposible de depurar.
- **Pensar que reordenar es gratis.** Los tipos encajan, el significado puede no hacerlo.

## 12. Límites de este sample (honestidad)

- **Un solo proceso.** No hay colas ni tuberías durables. Microsoft implementa el ejemplo con colas de mensajes y advierte que entonces importan la fiabilidad ("Use an infrastructure that ensures that data flowing between filters in a pipe aren't lost"), los mensajes repetidos y la idempotencia. **Nada de eso está acá.** Si el proceso muere, se pierde el run.
- **Sin garantía de entrega.** No hay reintentos, ni "al menos una vez", ni "exactamente una vez".
- **`Deduplicate` recuerda todos los ids** de un run: su memoria crece con la cantidad de ids distintos. Con mil millones de filas no sirve tal cual.
- **Los rechazos se guardan en memoria** durante el run. Con un archivo con millones de líneas malas, crecen sin límite.
- **Paralelismo limitado.** Solo `Buffer` pone etapas en tareas distintas; no hay varias instancias de un filtro lento ni escalado independiente (lo que Microsoft menciona como una de las ventajas del patrón distribuido).
- **Un fallo de la fuente se atribuye al primer filtro** que la lee (test `AFailingSource_IsBlamedOnTheFirstFilterThatPullsFromIt`). Es un comportamiento deliberado y conocido, no un acierto.
- **Una excepción tumba todo el run.** Microsoft lo llama el carácter "monolithic" del patrón: "for any change, the entire filter chain should be tested end to end". Los tests de integración de este sample hacen justo eso.
- **El paso a paso no es transaccional.** Si el run falla en la fila 500, las 499 anteriores ya salieron al consumidor.

## 13. Checklist

- [ ] Cada filtro tiene un tipo de entrada, un tipo de salida y una sola tarea.
- [ ] Ningún filtro menciona a otro.
- [ ] Solo la raíz de composición encadena.
- [ ] Datos malos: `Reject` con motivo. Bugs: excepción.
- [ ] Ningún filtro guarda estado entre ejecuciones.
- [ ] Hay un test que prueba que el flujo es perezoso y de memoria constante.
- [ ] Hay un test por cada regla de arquitectura, y alguna mutación que lo hace fallar.
- [ ] Sabés qué pasa si el proceso muere a la mitad.
