# Guía: Arquitectura de Frontend – Atomic Design + Container/Presentational + Micro-frontends

Para un dev junior. Son **tres ideas distintas** que se combinan en un mismo código y responden tres preguntas:

| Idea | Pregunta que responde | Se ve en el sample como |
|---|---|---|
| **Atomic Design** | ¿De qué piezas se compone una interfaz? | `atoms/`, `molecules/`, `organisms/`, `templates/`, `pages/` |
| **Container / Presentational** | ¿Quién sabe *qué* datos se muestran y quién sabe *cómo* se ven? | `containers/` (estado y efectos) y todo lo demás (puro) |
| **Micro-frontends** | ¿Cómo se reparte la aplicación entre equipos que entregan por separado? | `catalog` y `cart` cargados por un `shell` en tiempo de ejecución |

El sample es una tienda: un **catálogo** y un **carrito** que no se conocen entre sí. Tiene 116 tests, 16 mutaciones, y **no usa ninguna dependencia ni paso de compilación** (JavaScript puro y `node --test`).

> **Fuentes y qué se verificó.** Citas copiadas de:
> - Brad Frost, [Atomic Design, cap. 2](https://atomicdesign.bradfrost.com/chapter-2/) (las cinco etapas).
> - [patterns.dev – Container/Presentational](https://www.patterns.dev/react/presentational-container-pattern/).
> - Martin Fowler (Cam Jackson), [Micro Frontends](https://martinfowler.com/articles/micro-frontends.html) y [micro-frontends.org](https://micro-frontends.org/).
>
> **No pude abrir** el artículo original de Dan Abramov sobre el patrón (HTTP 403), así que **no cito texto suyo**: uso patterns.dev, que lo describe. Y atención a lo que patterns.dev aclara: "Modern React strongly favors Hooks over container components for separating logic from views." Este sample no usa React; el *principio* (separar el "qué" del "cómo") sigue vigente, el *envase* (clases contenedoras) es lo que cambió.

---

## 1. La idea en una frase cada una

**Atomic Design.** "Atoms are UI elements that can’t be broken down any further and serve as the elemental building blocks of an interface." Luego: "Molecules are collections of atoms that form relatively simple UI components", "Organisms are relatively complex components that form discrete sections of an interface", "Templates place components within a layout and demonstrate the design’s underlying content structure" y "Pages apply real content to templates and articulate variations."

Frost aclara que no es un proceso rígido: "Atomic design is not a linear process", sino "a mental model to help us think of our user interfaces as both a cohesive whole and a collection of parts at the same time."

**Container / Presentational.** Los presentacionales son "Components that care about how data is shown to the user"; los contenedores, "Components that care about what data is shown to the user". Beneficio: "Presentational components are easily made reusable, as they simply display data without altering this data" y "Testing presentational components is easy, as they are usually pure functions."

**Micro-frontends.** Fowler: "An architectural style where independently deliverable frontend applications are composed into a greater whole". Cada una "should have its own continuous delivery pipeline" y es propiedad de un equipo de punta a punta.

## 2. El mapa del sample

```
packages/
  ui-runtime/      h(), renderToString, store, adaptador al DOM        (infraestructura, sin dominio)
  contracts/       eventos, forma de un micro-frontend, versiones      (lo único que todos acuerdan)
  design-system/   foundations → atoms → molecules                     (sin dominio de negocio)
  catalog/         domain · api · organisms · templates · pages · containers · index.js   (micro-frontend)
  cart/            domain · organisms · templates · pages · containers · index.js         (micro-frontend)
  shell/           manifest, loader, event-bus, main.js                (compone, no conoce a nadie por nombre en el código)
```

```
              shell ──(import dinámico desde la URL del manifest)──► catalog ─┐
                │                                                              ├─► design-system ─► ui-runtime
                └────────────────────────────────────────────────────► cart ──┘
   catalog ──"cart/add-item"──► (bus de eventos) ──► cart ──"cart/changed"──► header del shell
```

## 3. Atomic Design en el código

| Nivel | Dónde vive | Qué es en el sample | Puede importar |
|---|---|---|---|
| Foundations | `design-system/foundations` | `formatMoney` (centavos → `$12.50`) | nada |
| **Atoms** | `design-system/atoms` | `Button`, `Badge`, `Heading`, `Price`, `TextInput` | foundations |
| **Molecules** | `design-system/molecules` | `SearchField`, `QuantityStepper`, `Card` | atoms |
| **Organisms** | cada micro-frontend | `ProductCard`, `ProductGrid`, `CartTable`, `CartSummary` | molecules y atoms (vía el índice del design system), otros organisms |
| **Templates** | cada micro-frontend | `CatalogTemplate`, `CartTemplate`: dónde va cada cosa, sin contenido | lo de abajo |
| **Pages** | cada micro-frontend | `CatalogPage`, `CartPage`: el template con contenido real para un estado | templates, organisms |

Decisiones de este sample (no son leyes del libro):
- **Átomos y moléculas no saben de negocio**; viven en el design system compartido. Todo lo que menciona productos o carritos es un organismo y vive en su micro-frontend. Fowler lo recomienda para las librerías compartidas: deben contener "only UI logic, and no business or domain logic".
- **Un átomo no contiene átomos ni una molécula contiene moléculas.** Un organismo sí puede contener organismos (`ProductGrid` usa `ProductCard`).
- **Una dirección única**: cada capa solo importa de las de abajo.

Un matiz honesto: Fowler también advierte contra crear la librería compartida demasiado pronto: dejá que los componentes "emerge naturally". Acá el design system existe desde el principio **para poder enseñar el patrón**; en un proyecto real conviene esperar a ver qué se repite.

## 4. Container / Presentational en el código

**Presentacional** = todo lo que no es `containers/`: funciones de props a elementos.
- No hacen `fetch`, no leen el reloj ni el azar, no tocan `document`, no guardan estado, no publican eventos. Un test lo vigila con un escaneo del código fuente.
- No modifican sus props: los tests les pasan datos **congelados** (`deepFreeze`) y fallarían si los tocaran.
- Dan el mismo resultado cada vez (tests de pureza).

**Contenedor** = `createCatalogContainer`, `createCartContainer`.
- Es el único que tiene un `store`, llama a la API y publica o escucha eventos.
- **No escribe markup**: `view()` solo le pasa estado y manejadores a la página. Un test prohíbe `h(...)` y etiquetas en los contenedores.
- Recibe sus dependencias (`productApi`, `bus`) por parámetro, así que se prueba con una API falsa.

```js
// containers/CatalogContainer.js (resumido)
const view = () => CatalogPage({ state: store.getState(), onSearch: actions.search, onAdd: actions.add, onRetry: actions.load });
```

Una carrera real que los tests atrapan: si el usuario pide cargar dos veces y la respuesta vieja llega *después* de la nueva, no debe pisarla. El contenedor numera cada carga y descarta las viejas (`keeps the answer of the LATEST load...`).

**Cómo se traduce a React** (el sample no lo usa): un componente es una función de props a elementos; un contenedor equivale a un *hook* propio más una página que lo usa. Patterns.dev lo dice: "Custom Hooks can replace class-based containers entirely". La separación se mantiene aunque cambie la forma.

## 5. Micro-frontends en el código

**Cómo se integran.** Fowler describe varias opciones; una es la ejecución en tiempo de ejecución con JavaScript, donde cada micro-frontend "exposes a global function as its entry-point" y el contenedor la invoca. Este sample hace una variante: cada micro-frontend **exporta un objeto `mfe` desde un módulo** y el shell lo carga con `import(url)`; la URL sale de una lista (`manifest.js`). El shell **no importa** ningún micro-frontend en el código.

```js
export const mfe = { name: 'catalog', version: '1.0.0', contract: 1, mount(root, { bus, services }) { /* ... */ return unmount; } };
```

**El contrato** (`packages/contracts`) es lo único que ambos lados conocen: la forma de `mfe`, los nombres y payloads de los eventos y cómo se comparan versiones.

**Cómo hablan sin conocerse.** Con eventos del navegador (`EventTarget` + `CustomEvent`), siguiendo el consejo de micro-frontends.org: "Use Browser Events for communication instead of building a global PubSub system." Fowler advierte el costo: los eventos permiten que se comuniquen de forma indirecta, pero el contrato es más difícil de hacer cumplir; por eso el bus **valida el payload contra el contrato al publicar**. Y recomienda que se comuniquen "as little as possible": acá son solo dos eventos (`cart/add-item`, `cart/changed`).

**Resiliencia.** Un micro-frontend que no carga, no cumple el contrato, necesita otra versión mayor del design system o lanza al montarse es reemplazado por un mensaje en su espacio; el otro sigue funcionando. Un handler de eventos que lanza no frena a los demás.

**Estilos.** micro-frontends.org: "Establish Team Prefixes — Agree on naming conventions where isolation is not possible yet." Cada paquete usa su prefijo (`ds-`, `catalog-`, `cart-`, `shell-`) y un test revisa tanto el código como `styles.css`.

**Versiones del design system.** El manifest declara `requires: { 'design-system': 1 }`; si el shell trae otra versión mayor, no monta ese micro-frontend y lo dice.

## 6. Reglas y cómo se verifican

| Regla | Test (en `tests/architecture.test.js`) |
|---|---|
| Cada capa solo importa de las de abajo (los organismos pueden anidarse) | `a layer only imports from layers BELOW it...` |
| Átomos/moléculas viven en el design system; organismos, templates, páginas y contenedores en un micro-frontend | `atoms and molecules live in the design system...` |
| El design system expone exactamente su API | `the design system exposes exactly...` |
| Los presentacionales importan solo capas inferiores, dominio, el design system y `h` | `they import only lower layers...` |
| Sin red, almacenamiento, navegador, tiempo, azar, estado ni eventos en los presentacionales | `they never touch the network...` |
| Sin clases ni estado de módulo | `they are plain exported functions...` |
| Los contenedores importan solo páginas, dominio, `createStore` y contratos | `they import only pages, domain rules...` |
| Los contenedores no construyen elementos | `they never build elements...` |
| El dominio es puro; la API no importa nada | `domain files are pure...`, `only api files...` |
| Dependencias permitidas entre paquetes | `packages only depend on the packages they are allowed to` |
| Ningún micro-frontend importa a otro | `no micro-frontend imports another...` |
| Solo se usa el `index.js` público de cada paquete | `other packages are used only through their public index.js...` |
| El shell no importa micro-frontends | `the shell never imports a micro-frontend...` |
| El manifest apunta a módulos reales que cumplen el contrato | `the manifest points at existing micro-frontends...` |
| Sin ciclos de importación | `there are no import cycles` |
| Prefijos de clases CSS | `every class a package renders carries that package prefix`, `every class selector in styles.css...` |

## 7. Paso a paso para crear el tuyo

1. **Dibujá primero los espacios**, no los componentes: ¿qué partes de la pantalla pertenecen a equipos distintos?
2. **Definí el contrato** entre ellos: eventos y su payload, forma del módulo, versionado.
3. **Armá el design system pequeño** con lo que de verdad se repite: átomos y moléculas sin negocio.
4. **En cada micro-frontend**, de abajo hacia arriba: dominio puro → organismos → template → página → contenedor.
5. **Escribí cada presentacional como una función** y probala llamándola con props congeladas.
6. **Escribí el contenedor** con dependencias inyectadas y probalo con una API falsa.
7. **Escribí el shell**: manifest, carga dinámica, verificación y mensaje de reemplazo.
8. **Agregá las reglas de arquitectura como tests**, y un test que pruebe al propio escáner de imports.

```bash
cd sample
npm test          # o: node --test
npm start         # o: node tools/serve.mjs  → http://localhost:5173
```

El navegador no carga módulos desde `file://`, por eso `npm start` levanta un servidor estático mínimo.

## 8. Recetas

**Agregar un átomo** (por ejemplo, `Avatar`): archivo en `design-system/atoms/`, exportarlo en `design-system/index.js`, tests de render y pureza, y actualizar la lista de exportaciones del test de arquitectura (es el aviso de que cambió la API pública).

**Agregar un micro-frontend** (por ejemplo, `recommendations`): carpeta nueva con las mismas capas, un `index.js` que exporte `mfe`, una entrada en `manifest.js` y una ranura en `index.html`. `shell/loader.js` no cambia. Los tests de arquitectura exigen que no importe a `catalog` ni `cart`; la prueba del manifest exige que cumpla el contrato.

## 9. Pruebas de mutación (que las reglas muerden)

Cada una se aplicó en una copia temporal y se corrió la suite completa (116 tests, todos verdes antes). Las 16 se detectaron.

| # | Mutación | Cayó |
|---|---|---|
| M1 | Un átomo importa una molécula | regla de capas |
| M2 | Un organismo presentacional llama a `fetch` | regla de pureza |
| M3 | Un contenedor construye markup | 2 tests |
| M4 | El catálogo importa al carrito | 2 tests |
| M5 | El shell importa al catálogo estáticamente | 2 tests |
| M6 | Un micro-frontend importa por una ruta interna del design system | 2 tests |
| M7 | Una clase CSS sin el prefijo del paquete | 2 tests |
| M8 | El shell deja que un micro-frontend roto rompa a los demás | 5 tests |
| M9 | Se elimina la verificación de versión del design system | `refuses a micro-frontend built for another major version...` |
| M10 | Un handler de eventos que lanza frena a los demás | `a handler that throws is reported...` |
| M11 | `addItem` muta el carrito recibido | 2 tests |
| M12 | El catálogo se queda con la respuesta de una carga vieja | `keeps the answer of the LATEST load...` |
| M13 | El renderizador deja de escapar texto | `escapes text...` |
| M14 | El carrito nunca deja de escuchar el bus | 2 tests |
| M15 | `stop()` del shell no desmonta nada | 3 tests |
| M16 | Un átomo lee el reloj | 4 tests |

## 10. Verificación con REA

REA analizó `packages/` como una aplicación JavaScript (evidencia estática, sin ejecutar nada): `ev_6ea3865845671644801c86d78dc70aab0ea9de927d1e80e9260454995108174a`, derivada de `ev_8d8033141a5f823a44af4521b16926dda0c0ae3edd5be33d7cd8e7cba8a29340`.

| Qué mostró | Detalle |
|---|---|
| Alcance | 36 archivos JavaScript analizados, 0 fallos de parseo |
| Red, temporizadores, procesos hijo, configuración, recursos | 0 relaciones en las familias `request`, `timer`, `child-process`, `configuration` y `resource-lifecycle`: el código de producción no hace `fetch`, ni `setTimeout`, ni lee `process.env` |
| Eventos | 0 relaciones en la familia `event`: REA solo cubre registros al estilo `EventEmitter` con nombres literales; el bus de este sample usa `EventTarget`, así que **esto no prueba nada sobre sus eventos** |
| Cobertura | La cobertura semántica queda como parcial (863 incógnitas): son relaciones que REA no pudo resolver, no defectos |

**Un hallazgo útil.** Comparé la cantidad de relaciones de importación que REA vio por módulo con las que contaba mi propio escáner de los tests. REA cuenta *enlaces importados* (por ejemplo, `CartContainer.js` tiene 9) y yo contaba *sentencias* (4); esa diferencia es normal y la expliqué módulo por módulo. Pero dos casos eran **errores reales de mi escáner**: tomaba la frase `does not export 'mfe'` dentro de un texto como si fuera un `export`, y fusionaba `export const VERSION = ...; export { formatMoney } from ...` en una sola sentencia con nombres equivocados. Los corregí (el patrón ahora lee una sentencia por vez desde el inicio de línea) y agregué tests que prueban al escáner. Una regla de arquitectura vale lo que vale la herramienta que la lee.

Límite: la vista de referencias entre módulos de REA devolvió vacío para este árbol, así que **no tengo el grafo de importaciones de REA**, solo conteos. El grafo lo calculan los tests.

## 11. Cuándo sí y cuándo no

### Atomic Design y Container/Presentational
Sirven casi siempre que haya más de un puñado de pantallas: dan un vocabulario común y componentes fáciles de probar. Cuidado con convertirlos en burocracia: Frost dice que es un modelo mental, no un proceso lineal; clasificar un componente en "molécula u organismo" no debería frenar a nadie.

### Micro-frontends
Fowler enumera los beneficios (bases de código "smaller, more cohesive and maintainable", mejoras incrementales, despliegue independiente, equipos autónomos) y **los costos**:
- "Duplication of dependencies": los usuarios pueden descargar el mismo código varias veces.
- "Fragmentation in the way your teams work".
- Diferencias entre desarrollo y producción, sobre todo con los estilos globales.
- Más repositorios, pipelines y despliegues: complejidad operativa.

Regla práctica: si un solo equipo hace todo el frontend, un monolito modular con carpetas por funcionalidad rinde más. Micro-frontends se justifican cuando hay **varios equipos que necesitan entregar por separado**.

## 12. Errores comunes

- **Lógica de negocio en el design system.** Un `ProductCard` en el design system lo ata al negocio.
- **Un contenedor que "también" pinta.** Mezcla el "qué" y el "cómo" y obliga a probar todo junto.
- **Presentacionales que llaman a la API** "solo para esto".
- **Micro-frontends que se importan entre sí.** Es un monolito con pasos extra.
- **Compartir estado por una variable global** en lugar de por eventos con contrato.
- **Estilos globales sin prefijo.** Un cambio en un equipo rompe a otro.
- **Crear el design system antes de ver qué se repite.**
- **No probar el fallo**: ¿qué ve el usuario si el carrito no carga?

## 13. Límites de este sample (honestidad)

- **No usa un framework.** `h()` y el adaptador al DOM son de juguete. La arquitectura es independiente de eso, pero no probé este código con React ni Vue.
- **Repintado ingenuo.** `mount` reemplaza todo el contenido en cada cambio de estado (sin *diffing*): se pierde el foco, el texto a medio escribir y el scroll de lo repintado. Los frameworks reales hacen mucho más.
- **Probado en un solo navegador.** Los tests usan un DOM falso (`FakeDocument`), así que además corrí la página real en Chrome 155 sin ventana (con `capture_browser_scenario` de REA, evidencia `ev_746a2a360fb8e1253b702a02786b894bff4b146cb665f567eaac1472561012cc`): cargó los dos micro-frontends (el shell informó `mounted: 2, failed: 0`), agregué dos libros y uno más desde el catálogo, busqué "design" con Enter, y el DOM final mostró 2 tarjetas filtradas, el carrito con las líneas `p1` (×2) y `p3`, el resumen "3 items · $99.97" y 3 en el encabezado. La consola solo mostró un 404 del favicon (ya corregido con un ícono vacío). **No probé Firefox ni Safari**, ni lectores de pantalla, ni vi la captura de pantalla a ojo: lo verificado es el DOM.
- **No hay aislamiento real de ejecución.** micro-frontends.org recomienda "Isolate Team Code — Don’t share a runtime, even if all teams use the same framework." Acá los micro-frontends comparten la página, el runtime y la misma instancia del design system; el aislamiento es por convenciones, contrato y prefijos, no por iframes ni Shadow DOM.
- **Todo en un repositorio y un servidor.** No hay pipelines ni despliegues independientes, que es el principal beneficio de los micro-frontends.
- **Sin ruteo, sin renderizado en servidor, sin carga diferida de dependencias, sin accesibilidad auditada.**
- **Sin tipos.** Los contratos (`props`, eventos) se verifican por tests y validación en ejecución, no por TypeScript.
- **Eventos síncronos.** Una promesa rechazada dentro de un handler no la ve el bus.
- **Datos de juguete.** La API devuelve una lista fija en memoria.

## 14. Checklist

- [ ] Cada nivel atómico importa solo de los inferiores.
- [ ] Átomos y moléculas no saben nada de negocio.
- [ ] Los presentacionales son funciones puras de sus props y no modifican lo que reciben.
- [ ] Los contenedores no escriben markup y reciben sus dependencias por parámetro.
- [ ] Los micro-frontends no se importan; se hablan por eventos con contrato validado.
- [ ] El shell carga por manifest y reemplaza lo que falla en lugar de romperse.
- [ ] Hay un test de arquitectura por regla y alguna mutación que lo hace fallar.
- [ ] Alguien probó la página en un navegador de verdad (acá: solo Chrome).
