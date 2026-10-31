# Guía: Arquitectura Microkernel / Plugins

Para un dev junior. La idea: un **núcleo pequeño** que sabe hacer lo mínimo y un montón de **plugins** que se enchufan en tiempo de ejecución, sin recompilar el núcleo. El sample es un cotizador de envíos (`Shipping`): el núcleo compara precios y cada transportista es un plugin que se instala copiando una carpeta. Tiene 34 tests, 10 mutaciones y sus binarios analizados con REA.

> **Fuentes y qué se verificó.** Citas copiadas de:
> - [Microsoft Learn – Create a .NET Core application with plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) (carga con `AssemblyLoadContext`, `AssemblyDependencyResolver`, `Private=false`).
> - [Microsoft Learn – assembly unloadability](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability) (descarga cooperativa).
> - Wikipedia: [Microkernel](https://en.wikipedia.org/wiki/Microkernel) y [Plug-in (computing)](https://en.wikipedia.org/wiki/Plug-in_(computing)).
>
> El nombre "arquitectura microkernel" para aplicaciones (núcleo + plug-ins) se usa en libros de patrones como el de Mark Richards (O'Reilly); **no pude abrir esa fuente (HTTP 403), así que no cito texto suyo**. Lo que sigue sobre su estructura sale de las fuentes de arriba y de este sample. Ojo: el *microkernel de sistemas operativos* es el origen de la analogía, pero no es lo mismo que una app con plugins.

---

## 1. La idea en una frase

> Un plug-in es un "software component that extends the functionality of an existing software system without requiring the system to be re-built" (Wikipedia, *Plug-in*).

Y el núcleo, en la analogía con los sistemas operativos, es "the near-minimum amount of software that can provide the mechanisms needed" (Wikipedia, *Microkernel*). Principio de minimalidad (Liedtke): un concepto se tolera dentro del núcleo solo si sacarlo impediría implementar la funcionalidad requerida.

Dos propiedades que definen el estilo:

| Propiedad | En este sample |
|---|---|
| El anfitrión funciona sin plugins ("the host application operates independently of the plug-ins") | Sin plugins, el núcleo corre y simplemente no cotiza nada (test `WithoutPlugins_TheKernelRunsAndQuotesNothing`) |
| Los plugins dependen del anfitrión y no funcionan solos | Un plugin solo implementa `IShippingCarrier`; sin núcleo no hace nada |

## 2. Las cuatro piezas

```
Shipping.Contracts   El CONTRATO. Lo único que conocen ambos lados. Diminuto y estable.
Shipping.Kernel      El NÚCLEO. Descubre plugins, los carga aislados, los compara. No conoce ninguno.
Shipping.Host        La aplicación (consola). Referencia núcleo + contrato, jamás un plugin.
plugins/             Carriers.Standard, Carriers.Express... Cada uno referencia SOLO el contrato.
```

```
              Host ───► Kernel ───► Contracts ◄─── Carriers.Standard
                                        ▲
                                        └────────── Carriers.Express
   (Kernel y Plugins no se conocen: se encuentran en el contrato y en una carpeta, en tiempo de ejecución)
```

La dependencia apunta **hacia el contrato** desde los dos lados. Es la inversión de dependencias, pero con un giro: el núcleo ni siquiera sabe *cuántos* implementadores hay.

## 3. Cómo se instala un plugin

Una carpeta por plugin, y el nombre de la carpeta es el nombre del ensamblado:

```
plugins/
  Carriers.Standard/Carriers.Standard.dll   + Carriers.Standard.deps.json
  Carriers.Express/Carriers.Express.dll     + Carriers.Express.deps.json
```

"Instalar" = copiar la carpeta. "Desinstalar" = borrarla. El núcleo no cambia.

## 4. Las tres cosas que hay que hacer bien

### 4.1 Un contexto de carga por plugin (aislamiento)
Cada plugin se carga en su propio `AssemblyLoadContext`. Microsoft lo explica así: es "a special type in the runtime that allows developers to isolate loaded assemblies into different groups to ensure that assembly versions don't conflict". Dos plugins pueden depender de versiones distintas de una misma librería.

El contexto usa `AssemblyDependencyResolver` para encontrar las dependencias del plugin leyendo su `.deps.json`. Por eso el proyecto del plugin lleva `<EnableDynamicLoading>true</EnableDynamicLoading>`.

### 4.2 El contrato se comparte, no se copia (identidad de tipos)
Este es el error número uno. Si el plugin trae **su propia copia** de `Shipping.Contracts.dll`, el `IShippingCarrier` del plugin y el del núcleo son **dos tipos distintos** para el runtime, y el núcleo no encuentra nada. Microsoft: "`<Private>false</Private>` ... is required for the reference to the assembly containing the plugin interfaces".

Este sample se defiende dos veces:
1. Cada plugin usa `<Private>false</Private>` (un test lo verifica en las carpetas ya construidas).
2. El núcleo, además, **ignora cualquier copia** del contrato dentro de un plugin y devuelve la suya:
```csharp
protected override Assembly? Load(AssemblyName name)
{
    if (name.Name == ContractsAssembly) return null;   // null => el contexto por defecto => el contrato del anfitrión
    var path = _resolver.ResolveAssemblyToPath(name);
    return path is null ? null : LoadFromAssemblyPath(path);
}
```
El fixture `Fixtures.BundledContracts` comete a propósito el error y el test demuestra que igual funciona; la mutación M3 (quitar esa línea) lo rompe.

### 4.3 El núcleo se protege de los plugins
Un plugin es código de otro. El núcleo:
- **Valida al cargar**: ¿tiene `[Carrier]`? ¿su versión de contrato es la soportada? ¿el id está repetido? Si no, lo rechaza **con motivo** y sigue con los demás.
- **Aísla al ejecutar**: si un plugin lanza una excepción, o responde con un precio inválido, o firma la cotización con el id de otro, queda como `Failure` y las demás cotizaciones siguen valiendo.

> Límite honesto: esto es aislamiento de **errores honestos**, no de seguridad. Microsoft lo dice: "Untrusted code cannot be safely loaded into a trusted .NET process. To provide a security or reliability boundary, consider a technology provided by your OS or virtualization platform." Un plugin que entra en un bucle infinito o consume toda la memoria sigue derribando al anfitrión.

## 5. Reglas y cómo se verifican

| Regla | Cómo |
|---|---|
| El contrato solo referencia el framework | Test (PE) |
| El contrato es diminuto: 5 tipos públicos, una interfaz con un método | Test |
| El núcleo conoce el contrato y ningún plugin | Test (PE + csproj) + REA |
| Ningún archivo del núcleo ni del host menciona un transportista concreto | Test de texto |
| El host referencia solo núcleo y contrato | Test (csproj) |
| Cada plugin referencia el contrato y no el núcleo ni otro plugin | Test (PE) |
| La carpeta de cada plugin contiene solo su dll (no copia el contrato) | Test |
| Los proyectos de plugin tienen `Private=false` y `EnableDynamicLoading` | Test |
| Plugins con versión de contrato desconocida, sin atributo, duplicados, archivos corruptos: rechazados con motivo | Tests |
| Un plugin que falla no afecta a los demás | Tests |
| Descargar un plugin libera su contexto | Test (`WeakReference` + GC) |

## 6. Paso a paso para crear el tuyo

Requisito: .NET 10 SDK.

1. **Define el contrato** (`classlib` sin referencias): la interfaz de extensión, los datos que viajan y un atributo que marca y versiona.
2. **Núcleo** (`classlib`): referencia el contrato. Descubrimiento por carpeta, contexto de carga por plugin, validación, y la capacidad central que usa los plugins.
3. **Plugin** (`classlib`): 
```xml
<PropertyGroup><EnableDynamicLoading>true</EnableDynamicLoading></PropertyGroup>
<ItemGroup>
  <ProjectReference Include="..\Shipping.Contracts\Shipping.Contracts.csproj"><Private>false</Private></ProjectReference>
</ItemGroup>
```
```csharp
[Carrier("standard")]
public sealed class StandardCarrier : IShippingCarrier
{
    public Quote? GetQuote(Parcel p) => p.WeightKg > 30m ? null : new Quote("standard", ..., ...);
}
```
4. **Host**: referencia núcleo y contrato. `PluginCatalog.Load(carpeta)` y usar.
5. **Despliegue**: construir cada plugin a una carpeta con su nombre (aquí lo hace `Directory.Build.props`: copia la salida a `artifacts/plugins/<Nombre>/`).
6. **Tests de arquitectura** desde el principio, y **fixtures** que representan plugins malos (el sample tiene seis).

Probarlo a mano:
```bash
cd sample && dotnet build
dotnet run --project src/Shipping.Host -- artifacts/plugins 2 100
# Plugins loaded: express, standard
#   standard   7.00  3 day(s)
#   express   17.40  1 day(s)
# Cheapest: standard
```

## 7. Receta: añadir un transportista sin tocar el núcleo

1. Crear `Carriers.Pickup` (copiar `Carriers.Express.csproj`, cambiar nombres).
2. Una clase con `[Carrier("pickup")]` que implemente `IShippingCarrier`.
3. Construir y copiar la carpeta a `plugins/`.

Núcleo, host y contrato: **cero cambios**. Ese es el punto del estilo.

## 8. Los plugins de prueba (fixtures) y qué demuestran

| Fixture | Mal comportamiento | Resultado esperado |
|---|---|---|
| `Faulty` | Lanza excepción al cotizar | Fallo registrado; los demás cotizan |
| `Impostor` | Devuelve una cotización firmada por otro | Rechazada por el núcleo |
| `FutureContract` | Declara contrato versión 99 | Plugin rechazado al cargar |
| `StandardClone` | Reclama el id `standard` | Gana la primera carpeta; la otra rechazada |
| `NotAPlugin` | Implementa la interfaz sin `[Carrier]` | Rechazado |
| `BundledContracts` | Trae su propia copia del contrato | Funciona igual (el núcleo comparte el suyo) |

## 9. Pruebas de mutación (que las reglas muerden)

Diez, en copias temporales, todas atrapadas:

| # | Mutación | Qué falló |
|---|---|---|
| M1 | El núcleo referencia un plugin concreto | 2 tests de arquitectura |
| M2 | Un plugin referencia el núcleo | 3 tests de arquitectura |
| M3 | El núcleo deja de compartir el contrato del anfitrión | test del plugin con contrato copiado |
| M4 | El núcleo deja de aislar excepciones de plugins | test del plugin que lanza |
| M5 | El núcleo deja de revisar la versión del contrato | test de versión desconocida |
| M6 | El núcleo deja de detectar ids duplicados | test de duplicados |
| M7 | Un plugin copia el contrato a su carpeta | 2 tests de arquitectura |
| M8 | Los contextos dejan de ser descargables | todos los tests que cargan plugins (el catálogo falla al hacer `Unload`) y el de descarga |
| M9 | El núcleo acepta cotizaciones firmadas por otro | test del impostor |
| M10 | El núcleo menciona un transportista concreto | test de texto |

**Lo que encontró el uso real y no los tests:** al correr el host con una ruta relativa (`artifacts/plugins`), todos los plugins se rechazaban: `LoadFromAssemblyPath` solo acepta rutas absolutas. Se corrigió con `Path.GetFullPath` y se agregó un test (`ARelativeDirectoryWorksToo`). Moraleja: los tests que escribís reflejan lo que imaginaste; correr la app refleja lo que hace.

## 10. Verificación del binario con REA

| Hecho | Evidencia |
|---|---|
| `Shipping.Kernel.dll` referencia `Shipping.Contracts`, `System.Runtime.Loader` y el framework; **ningún** `Carriers.*` ni `Fixtures.*` | `inspect_managed_artifact`, `ev_b36ece73…caa0` |
| `Carriers.Standard.dll` referencia solo `System.Runtime` y `Shipping.Contracts`; no el núcleo | `inspect_managed_artifact`, `ev_498e3936…23db` |
| En el IL del núcleo, `AssemblyLoadContext` es llamado desde `PluginCatalog.LoadFolder`/`Dispose` (carga y `Unload`) y `PluginLoadContext`; `AssemblyDependencyResolver` desde `PluginLoadContext`; `Activator.CreateInstance` solo desde `PluginCatalog.TryAccept`; 0 llamadas cuyo tipo declarante empiece por `Carriers.` o `Fixtures.`; ningún literal "standard"/"express" | `inspect_managed_members` (`call_edges`, `user_strings`), `ev_b354c8f7…b6dd` |

Es la imagen binaria de la regla: **el núcleo carga por reflexión lo que no conoce**. REA es análisis estático: no prueba el comportamiento en ejecución (para eso están los tests).

## 11. Cuándo sí y cuándo no

| Tu situación | Recomendación |
|---|---|
| Varias variantes del mismo comportamiento (transportistas, formatos, pasarelas de pago, reglas de precios) | **Microkernel / plugins** |
| Terceros o equipos distintos deben poder extender sin tocar tu código | **Sí** |
| IDEs, editores, navegadores, herramientas con ecosistema | **Sí** (es el caso clásico) |
| Una sola implementación que nunca cambia | **No**: es un `if` o una interfaz con una clase |
| Necesitás aislamiento de seguridad o de recursos | **No como único mecanismo**: procesos separados o contenedores |
| Los "plugins" necesitan transacciones entre sí | Cuidado: el núcleo no puede coordinar lo que no conoce |

## 12. Errores comunes

| Error | Por qué duele | Corrección |
|---|---|---|
| El plugin trae su copia del contrato | Dos tipos distintos con el mismo nombre; "no encuentro ningún plugin" | `Private=false` y que el núcleo comparta su contrato |
| El núcleo "conoce" un plugin "solo uno" | Deja de ser núcleo | Un test que prohíba nombres concretos |
| Contrato grande y cambiante | Rompe a todos los plugins | Mantenerlo mínimo y versionado |
| Plugin que `throw` tumba la app | Un error ajeno derriba todo | Aislar cada llamada y reportar el fallo |
| Cargar todo en el contexto por defecto | Conflictos de versiones entre plugins | Un `AssemblyLoadContext` por plugin |
| Plugin apuntando a `netstandard` | El `.deps.json` puede elegir implementaciones equivocadas | Apuntar a un runtime concreto (Microsoft lo recomienda) |
| Suponer que descargar es inmediato | Es cooperativo: depende del GC y de que nada retenga referencias | Tests con `WeakReference`; no mantener referencias a tipos del plugin |
| Tratarlo como sandbox de seguridad | Corre en el mismo proceso | Procesos aislados para código no confiable |

## 13. Límites de este sample (honestidad)

- **No es un sandbox.** Un plugin malicioso o con bucle infinito derriba el proceso. Los tests solo cubren errores honestos.
- **Un solo tipo de extensión** (`IShippingCarrier`). Los sistemas reales suelen tener varios puntos de extensión y servicios que el núcleo ofrece a los plugins (aquí no hay ninguno: el núcleo no inyecta servicios en los plugins).
- **Sin descubrimiento en caliente**: se carga una vez al arrancar; no vigila la carpeta.
- **Descarga probada, no usada**: el test demuestra que los contextos pueden recolectarse tras `Dispose`, pero ninguna parte del host recarga plugins en caliente. La prueba depende del GC y se hace con hasta 10 pasadas, tal como indica Microsoft; es una técnica estándar pero no una garantía determinista.
- **Orden de duplicados**: gana la carpeta que va primero en orden ordinal; es una convención de este sample.
- **Sin firma ni verificación de procedencia** de los ensamblados.
- **Los plugins se construyen con la solución** (el proyecto de tests los referencia solo para construirlos). En producción se construirían y publicarían por separado.
- **Un único `TargetFramework`** (net10.0) entre núcleo y plugins; plugins con otro framework no se cubren. Microsoft señala además que un plugin no puede introducir frameworks nuevos (p. ej. `Microsoft.AspNetCore.App`) en un anfitrión que no los declare.

## 14. Checklist

- [ ] El contrato es una `classlib` sin dependencias y casi no cambia
- [ ] El núcleo no referencia ningún plugin (test que lo verifica)
- [ ] El núcleo corre sin plugins
- [ ] Cada plugin referencia solo el contrato, con `Private=false`
- [ ] Un `AssemblyLoadContext` por plugin y el contrato siempre del anfitrión
- [ ] Un plugin roto se rechaza o se reporta; nunca tumba a los demás
- [ ] El contrato tiene versión y el núcleo la comprueba
- [ ] Hay fixtures de plugins malos y tests que los usan
- [ ] Sé que esto no es un sandbox de seguridad
- [ ] Probé al menos una mutación y corrí la app a mano
