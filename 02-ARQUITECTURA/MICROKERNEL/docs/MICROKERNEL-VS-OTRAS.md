# Microkernel / Plugins vs otras arquitecturas

El microkernel responde a una pregunta distinta de las de Clean o Hexagonal: no "¿hacia dónde apuntan las dependencias?" sino **"¿quién decide, y cuándo, qué piezas forman el sistema?"**. En Clean, lo decide el compilador; en Microkernel, lo decide la carpeta de plugins en tiempo de ejecución.

> Fuentes: [Microsoft Learn – plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support), [Wikipedia – Microkernel](https://en.wikipedia.org/wiki/Microkernel), [Wikipedia – Plug-in](https://en.wikipedia.org/wiki/Plug-in_(computing)). Ejemplos del repo: [`MICROKERNEL/sample`](../sample), [`HEXAGONAL/`](../../HEXAGONAL), [`MODULAR-MONOLITH/`](../../MODULAR-MONOLITH), [`CLEAN-SCREAMING/`](../../CLEAN-SCREAMING).

---

## 1. Quién compone el sistema y cuándo

| Arquitectura | ¿Cuándo se decide qué piezas hay? | ¿Quién? |
|---|---|---|
| Clean / Hexagonal | **Compilación** (y composition root) | El equipo, en el código |
| Modular Monolith | Compilación (módulos) | El equipo |
| Microservicios | Despliegue | Operaciones / equipos |
| **Microkernel / Plugins** | **Ejecución** (al arrancar o en caliente) | Quien instala los plugins |

## 2. Microkernel vs Hexagonal (puertos y adaptadores)

Se parecen mucho en el esqueleto: ambos tienen un núcleo que define una interfaz y piezas externas que la implementan.

| | Hexagonal | Microkernel |
|---|---|---|
| Interfaz | Puerto, definido por el núcleo | Contrato de extensión, definido por el núcleo |
| Implementaciones | Una por puerto (normalmente) | **Cero, una o muchas**, descubiertas |
| ¿El núcleo sabe cuántas hay? | Sí (se enchufan en el composition root) | **No**: las descubre |
| ¿Se pueden añadir sin recompilar? | No | **Sí** |
| Qué se optimiza | Aislar el dominio de la tecnología | Extensibilidad abierta |
| Qué cuesta | Casi nada | Carga dinámica, versionado del contrato, errores en ejecución |

Pueden **combinarse**: el núcleo hexagonal puede tener un puerto cuyo adaptador sea "el conjunto de plugins descubiertos".

## 3. Microkernel vs Modular Monolith

| | Modular Monolith | Microkernel |
|---|---|---|
| Módulos | Conocidos en compilación, con contrato público | Desconocidos en compilación |
| Comunicación | Contratos y eventos entre módulos | El núcleo llama a plugins; los plugins **no se hablan** |
| Propósito | Dividir un dominio grande | Permitir variantes y extensión |
| Ejemplo | Pedidos, Inventario, Facturación | Transportistas, formatos de exportación |

Un módulo del monolito puede ser un microkernel por dentro, y un plugin puede ser un módulo.

## 4. Microkernel vs Microservicios

| | Microservicios | Microkernel |
|---|---|---|
| Frontera | Red | Proceso (carga dinámica) |
| Aislamiento de fallos | Fuerte (proceso, red) | Débil: **mismo proceso** |
| Despliegue independiente | Sí, por servicio | Sí, por carpeta, pero reinicio o recarga del anfitrión |
| Coste operativo | Alto | Bajo |
| Seguridad frente a código ajeno | Mejor (procesos/contenedores) | **Ninguna por sí solo** |

Regla práctica: si no confiás en el autor del plugin, no es un microkernel en proceso, es un servicio.

## 5. Microkernel vs Pipes & Filters y vs Strategy

- **Strategy** (patrón de diseño): varias implementaciones de una interfaz elegidas en el código. Microkernel es Strategy con **descubrimiento y carga dinámica** y con ciclo de vida.
- **Pipes & Filters** (próximo en el catálogo): etapas encadenadas donde la *salida de una es la entrada de la siguiente*. En un microkernel, los plugins son *alternativas o extensiones* que el núcleo consulta; no se encadenan por sí solos. Se combinan bien: cada filtro puede ser un plugin.

## 6. Qué obtenés y qué pagás

| Ganás | Pagás |
|---|---|
| Añadir variantes sin tocar el núcleo | Un contrato que debe ser estable y versionado |
| Equipos o terceros pueden extender | Errores que aparecen en ejecución, no al compilar |
| Núcleo pequeño y fácil de razonar | Descubrimiento, carga, aislamiento y validación son parte del núcleo |
| Variantes opcionales por cliente | Pruebas de combinaciones de plugins |
| Despliegue de una variante sin recompilar el resto | No es un sandbox: un plugin malo puede tumbar el proceso |

## 7. Cuándo elegir cada una

| Situación | Elección |
|---|---|
| Dominio con reglas ricas y tecnología cambiante | Hexagonal / Clean |
| Dominio grande dividido en áreas | Modular Monolith |
| Escalas y equipos independientes | Microservicios |
| **Muchas variantes de un mismo comportamiento y se agregan seguido** | **Microkernel / plugins** |
| **Ecosistema abierto de extensiones** | **Microkernel / plugins** |
| Un procesamiento por etapas | Pipes & Filters (cuando esté en el catálogo) |
| Una sola implementación estable | Nada de esto: una interfaz y una clase |

## 8. Errores al comparar

- "Microkernel = microservicios": los dos tienen "micro" en el nombre y nada más. El microkernel vive en un solo proceso.
- "Si uso inyección de dependencias ya tengo plugins": la DI compone en compilación o arranque con tipos conocidos; el microkernel carga código **que no existía** cuando compilaste.
- "Los plugins aíslan la seguridad": no, aíslan el *versionado y los errores honestos*.
- "El núcleo debe tener las reglas importantes": las reglas importantes del sistema viven donde convenga; el núcleo solo debe tener lo mínimo para que los plugins funcionen.
- "Un plugin por clase": un plugin es una unidad desplegable (ensamblado, carpeta), no una clase.
