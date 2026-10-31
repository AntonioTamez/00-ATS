# MVC / MVVM vs las arquitecturas de aplicación

La confusión más frecuente del catálogo: **MVC y MVVM no compiten con Clean, Hexagonal, Layered o Microservicios**. Responden a otra pregunta. Este documento aclara qué es cada cosa y dónde vive cada patrón dentro de las demás.

> Fuentes: [Fowler – GUI Architectures](https://martinfowler.com/eaaDev/uiArchs.html), [Microsoft – ASP.NET Core MVC](https://learn.microsoft.com/en-us/aspnet/core/mvc/overview), [Microsoft – MVVM](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm), [Microsoft – N-tier](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/n-tier). Ejemplos: [`MVC-MVVM/sample`](../sample), [`LAYERED/sample`](../../LAYERED/sample), [`CLEAN/sample`](../../CLEAN/sample), [`HEXAGONAL/sample`](../../HEXAGONAL/sample).

---

## 1. Dos preguntas distintas

| Pregunta | Familia | Ejemplos |
|---|---|---|
| ¿Cómo organizo **toda la aplicación** (dependencias, capas, despliegue)? | Arquitecturas de aplicación | Layered, Clean, Hexagonal, Onion, Modular Monolith, Microservicios |
| ¿Cómo organizo **el código de la pantalla** y su relación con el usuario? | Patrones de presentación | MVC, MVP, MVVM |

Fowler lo enmarca como patrones para *"rich-client development"* y como *"the separation of presentation from domain logic"*: tratan de una frontera, la que separa la **presentación** del **dominio**.

## 2. Dónde vive cada patrón dentro de una arquitectura

| Arquitectura | Dónde encajan MVC / MVVM |
|---|---|
| **Layered** | Dentro de la capa de **presentación**. La capa de negocio es "el Model" |
| **Clean** | En la capa de **interface adapters** (controladores, presenters, view models), por fuera de los casos de uso |
| **Hexagonal** | En el **adaptador de entrada** (driving adapter): el controlador MVC traduce HTTP a una llamada al puerto |
| **Onion** | En el anillo exterior (presentación), llamando a los servicios de aplicación |
| **Vertical Slice** | Cada slice puede tener su endpoint + su "vista" (aquí el patrón se diluye: un endpoint minimal API no usa controladores ni vistas) |
| **Modular Monolith / Microservicios** | Cada módulo o servicio con interfaz propia puede usar su patrón; muchas veces solo expone una API |

En el sample, el "Model" es `Shop.Model`: las reglas compartidas. En una arquitectura mayor, ese Model *sería la capa de negocio/dominio entera* y el patrón de presentación se quedaría con el trozo de arriba.

## 3. MVC, MVP y MVVM

| | MVC | MVP | MVVM |
|---|---|---|---|
| Intermediario | Controller | Presenter | ViewModel |
| Quién actualiza la vista | El controlador elige una vista y le da datos (web) / la vista observa el modelo (clásico) | El presenter la actualiza **activamente** | La vista **se enlaza** (binding) al ViewModel |
| La vista conoce | Al modelo (clásico) o recibe datos | Casi nada: el presenter la maneja | Al ViewModel |
| Habitual en | Web servidor (ASP.NET Core MVC) | Aplicaciones de interfaz con estado, en su origen (IBM, Taligent, según Fowler) | XAML: .NET MAUI (Microsoft lo documenta para MAUI) |

Fowler sobre las variantes: *"The active reaction to user acts lives in a separate presenter object"* (MVP) y *"I call this a Presentation Model because it's a model that is really designed for and thus part of the presentation layer. It's also known as MVVM"*. Sobre MVC clásico: *"When the model changes, the views react"*.

El kit implementa MVC (web) y MVVM; MVP queda como contraste conceptual.

## 4. Lo que cada patrón resuelve y lo que cuesta

| | Ventaja | Coste |
|---|---|---|
| **MVC** | Separa petición, reglas y HTML; controladores muy probables sin servidor (Microsoft: *"highly testable"*) | Cada acción pasa por un ciclo completo de petición y respuesta; el estado de la pantalla lo recompone el servidor |
| **MVVM** | La lógica de pantalla se prueba **sin la vista**; la vista cambia sin tocar el ViewModel (Microsoft) | Infraestructura de notificaciones y comandos; Fowler avisa: *"Observer behavior is hard to understand and debug because it's implicit behavior"* |

## 5. Cuándo usar cada uno

| Tu situación | Patrón |
|---|---|
| Sitio web renderizado en el servidor | **MVC** (o Razor Pages, que simplifica el patrón por página) |
| API que consume una SPA | No hay vista en el servidor: controladores/endpoints devuelven datos; el cliente tiene sus propios patrones |
| Aplicación de escritorio o móvil con XAML | **MVVM** |
| Pantalla con mucho estado y comandos que se habilitan/deshabilitan | **MVVM** |
| Pantallas simples sin estado en el cliente | MVC / endpoints, no hace falta más |

### No lo uses (o simplifica) cuando
- La "vista" es solo JSON: un controlador/endpoint fino basta.
- La pantalla es trivial: un ViewModel con notificaciones es burocracia.
- Todavía no tienes el Model separado: lo primero es sacar las reglas de la UI.

## 6. Cómo se combina

```
Arquitectura de aplicación        → Layered / Clean / Hexagonal / Modular Monolith ...
  └ presentación                  → MVC (web) o MVVM (XAML)
       └ llama a                  → servicios de aplicación / casos de uso / la capa de negocio
            └ que usa             → el Model (dominio), compartido por todas las presentaciones
```

Lo valioso del sample: **un solo Model, dos presentaciones**. Las reglas (máximo 10 por línea, descuento desde 100 €) están escritas una vez y las respetan la web MVC y el ViewModel de MVVM.

## 7. Qué muestra cada sample

| Aspecto | LAYERED | CLEAN | HEXAGONAL | **MVC-MVVM** |
|---|---|---|---|---|
| Foco | Dependencias entre capas | Dependencias hacia el dominio | Puertos y adaptadores | **Cómo se organiza la presentación** |
| Qué hay "arriba" | Endpoints minimal API | API | Adaptadores REST/CLI | **Controladores + vistas Razor; ViewModel + vista** |
| Dominio compartido entre interfaces | No | No | Sí (REST y CLI) | **Sí (web y vista de consola)** |
| Reglas verificadas | Capas cerradas | Dependencias | Puertos/adaptadores | **Quién conoce a quién en MVC y MVVM; ViewModels pasivos vs activos** |

## 8. Errores al comparar

- "MVC es una arquitectura": es un patrón de presentación.
- "MVVM es MVC con otro nombre": cambia el mecanismo de sincronización (binding y comandos frente a un controlador que elige una vista) y la vida del objeto intermedio.
- "El ViewModel es siempre un DTO": en MVC sí (pasivo); en MVVM es activo.
- "Con Clean no necesito MVC/MVVM": Clean no dice cómo es la pantalla; los controllers/presenters/view models de Clean *son* el patrón de presentación.
- "El controlador es el sitio de la lógica": Microsoft recomienda lo contrario (*"push business logic out of the controller and into the domain model"*).
- "MVVM necesita un framework": los frameworks ayudan (Microsoft cita el Community Toolkit, ReactiveUI y Prism), pero el patrón son dos interfaces de .NET (`INotifyPropertyChanged`, `ICommand`) y disciplina.
