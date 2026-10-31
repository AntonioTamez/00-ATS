# Arquitectura de frontend vs otras arquitecturas

Las tres ideas de este kit (Atomic Design, Container/Presentational, Micro-frontends) no compiten con Clean, Hexagonal o Microservicios: **trabajan en otro eje**. Esta comparación muestra dónde se parecen, dónde chocan y cómo se combinan.

> Fuentes: [Atomic Design](https://atomicdesign.bradfrost.com/chapter-2/), [patterns.dev – Container/Presentational](https://www.patterns.dev/react/presentational-container-pattern/), [Martin Fowler – Micro Frontends](https://martinfowler.com/articles/micro-frontends.html), [micro-frontends.org](https://micro-frontends.org/). Ejemplos del repo: [`FRONTEND/sample`](../sample), [`MVC-MVVM/`](../../MVC-MVVM), [`SCREAMING/`](../../SCREAMING), [`MICROSERVICIOS/`](../../MICROSERVICIOS), [`MODULAR-MONOLITH/`](../../MODULAR-MONOLITH), [`HEXAGONAL/`](../../HEXAGONAL).

---

## 1. Qué organiza cada una

| Idea | Qué organiza | Unidad |
|---|---|---|
| Atomic Design | La **composición visual**: de lo mínimo a la pantalla | Átomo, molécula, organismo, template, página |
| Container / Presentational | La **responsabilidad**: datos y efectos vs. apariencia | Contenedor / presentacional |
| Micro-frontends | La **entrega**: quién construye y despliega cada parte | Micro-frontend |
| Layered / Clean / Hexagonal | La dirección de las dependencias del código | Capa / anillo / puerto |
| Microservicios | El sistema de servidor partido en servicios | Servicio |

Ninguna responde la pregunta de las demás, por eso se combinan sin pelearse.

## 2. Atomic Design vs Screaming Architecture (el choque más interesante)

| | Atomic Design | Screaming Architecture |
|---|---|---|
| Carpeta de primer nivel | **Tipo de pieza**: `atoms/`, `molecules/`... | **Funcionalidad**: `catalog/`, `cart/` |
| Pregunta que contesta la estructura | "¿Qué tan complejo es este componente?" | "¿De qué trata esta aplicación?" |
| Riesgo | Una carpeta `organisms/` gigante con todo el negocio mezclado | Duplicar piezas visuales en cada funcionalidad |

El sample **usa las dos a la vez**: el primer nivel *grita el negocio* (`catalog`, `cart`) y, dentro de cada uno, las carpetas siguen los niveles atómicos. Lo genérico (átomos y moléculas) sube a un design system compartido. Así cada eje se usa para lo que es bueno.

## 3. Container / Presentational vs MVC / MVVM

| | MVC / MVVM | Container / Presentational |
|---|---|---|
| Piezas | Modelo, vista, controlador o view-model | Contenedor y presentacional |
| Qué se separa | Datos y reglas, presentación, coordinación | El "qué" se muestra y el "cómo" se ve |
| Dónde viven las reglas de negocio | En el modelo | **No están en ninguna de las dos**: en este sample, en `domain/` (funciones puras) |
| Enlace de datos | Típico en MVVM | Manual, por props y manejadores |

Se parecen: el contenedor hace de controlador/view-model y el presentacional de vista. La diferencia práctica es que acá la "vista" es una **función pura** y el estado se mueve a un solo lugar.

## 4. Micro-frontends vs Microservicios

| | Microservicios | Micro-frontends |
|---|---|---|
| Frontera | Red | El navegador: módulos, iframes o Web Components |
| Qué entrega cada equipo | Una capacidad de negocio en el servidor | Una porción de la pantalla, de punta a punta |
| Aislamiento de fallos | Proceso y red | Débil si comparten la página; hay que construirlo (en el sample, un mensaje de reemplazo) |
| Costo operativo | Alto | Alto de otra forma: "duplication of dependencies" y más pipelines |

Combinan bien: cada micro-frontend puede tener su propio servicio, para que un equipo sea dueño de ambos extremos.

## 5. Micro-frontends vs Modular Monolith

| | Modular Monolith | Micro-frontends |
|---|---|---|
| Despliegue | Uno solo | Uno por micro-frontend |
| Módulos | Conocidos en compilación | Cargados en tiempo de ejecución desde un manifest |
| Costo | Bajo | Contrato de integración, versiones compartidas, errores en ejecución |

Si todos los módulos los hace un mismo equipo y se despliegan juntos, un monolito modular de frontend (carpetas por funcionalidad con fronteras claras) cuesta mucho menos.

## 6. Micro-frontends vs Microkernel

El shell del sample **es** un pequeño microkernel: descubre piezas desde una lista, valida el contrato, las monta y se protege de sus fallos. Las diferencias son de alcance: acá las piezas son porciones de pantalla y comparten un bus de eventos, en lugar de extender un comportamiento de negocio. Todo lo que dice [MICROKERNEL](../../MICROKERNEL/docs/GUIA-MICROKERNEL.md) sobre contratos estables y versionado aplica igual.

## 7. Qué obtenés y qué pagás

| Ganás | Pagás |
|---|---|
| Vocabulario común y componentes reutilizables | Debates sobre "¿es molécula u organismo?" si se toma como dogma |
| Componentes presentacionales fáciles de probar | Más archivos y más indirección para pantallas simples |
| Equipos que entregan por separado | Contrato de integración y duplicación de dependencias |
| Fallos aislados por porción de pantalla | Experiencia de usuario fragmentada si los equipos divergen |

## 8. Cuándo elegir cada una

| Situación | Elección |
|---|---|
| Interfaz con muchas pantallas y piezas repetidas | Atomic Design (como modelo mental) |
| Lógica de datos mezclada con la apariencia | Container / Presentational (o hooks, en React) |
| Varios equipos que necesitan entregar por separado | Micro-frontends |
| Un solo equipo, un solo despliegue | Monolito modular de frontend, sin micro-frontends |
| Reglas de negocio ricas en el cliente | Además, un núcleo de dominio aislado (como `domain/`) |

## 9. Errores al comparar

- "Micro-frontends = microservicios en el navegador": el costo y las razones son distintos.
- "Atomic Design = carpetas atoms/molecules/organisms": es sobre todo una forma de pensar la composición; la carpeta es una consecuencia.
- "Container/Presentational ya no existe porque hay hooks": cambió la forma, no el principio de separar el "qué" del "cómo".
- "Un design system compartido hace micro-frontends": sin contrato ni versionado, lo único que hace es acoplarlos por detrás.
