# Screaming Architecture vs Layered / Clean / Vertical Slice / Modular Monolith / DDD

Screaming Architecture no compite con las demás: es una **pregunta que se le hace a cualquiera de ellas**: *"What does the architecture of your application scream?"* Una arquitectura puede ser perfecta en dependencias y aun así gritar "framework" en lugar de gritar el negocio.

> Fuente: [Robert C. Martin – Screaming Architecture](https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html). Ejemplos: [`SCREAMING/sample`](../sample) y los demás samples del catálogo.

---

## 1. Qué responde cada cosa

| Idea | Responde a… |
|---|---|
| **Screaming Architecture** | ¿Qué comunica la estructura a quien la abre? |
| **Layered / Clean / Hexagonal / Onion** | ¿Hacia dónde apuntan las dependencias? |
| **Vertical Slice** | ¿Cómo agrupo el código para que un cambio sea local? |
| **Modular Monolith** | ¿Cómo divido el negocio con fronteras impuestas? |
| **DDD** | ¿Cómo nombro y modelo el negocio? |

## 2. El mismo dominio, tres formas de organizarlo

| | Por rol técnico (Layered clásico) | Por capacidad (Screaming) | Por capacidad con fronteras impuestas (Modular Monolith) |
|---|---|---|---|
| Carpetas de la raíz | `Controllers`, `Services`, `Repositories`, `Models` | `Patients`, `Appointments`, `Billing` | Proyectos `Catalog`, `Ordering`, `Notifications` |
| ¿Qué grita? | "Aplicación web con controladores" | "Una clínica" | "Una tienda con catálogo y pedidos" |
| Tocar "cancelar cita" | 4 carpetas | 1 carpeta | 1 módulo |
| Frontera entre áreas | Ninguna | Por convención + test de texto | Por compilador (`internal`) + tests |
| Coste | Mínimo | Mínimo | Más proyectos y más disciplina |

Screaming es **el primer paso**; Modular Monolith es ese mismo corte con fronteras que el compilador defiende.

## 3. Frente a cada una

### Layered
Layered corta por **capa técnica** (presentación, negocio, datos); eso es lo que, por defecto, produce carpetas `Controllers/Services/Repositories`. Screaming propone que el **primer nivel** de la estructura sea el negocio, y que las capas, si las hay, vivan *dentro* de cada capacidad. No contradice a Layered: cambia qué va en primer plano.

### Clean / Hexagonal / Onion
Las tres definen **dependencias**, no nombres de carpeta. Clean, en particular, comparte la idea de fondo (los casos de uso y el dominio en el centro, el framework fuera). Se pueden construir con estructura "gritona" (carpetas por capacidad con sus propias capas) o con estructura "técnica" (un proyecto `Domain`, otro `Application`...). Ambas son válidas en dependencias; solo la primera grita el negocio.

### Vertical Slice
Es el pariente más cercano: agrupar por funcionalidad produce, de forma natural, una estructura que grita el negocio. La diferencia es de nivel: **Vertical Slice es un mecanismo de organización** (cada petición con todo lo suyo); **Screaming es el criterio de comunicación** (que el árbol hable del negocio). Un proyecto puede cumplir uno y no el otro.

### Modular Monolith
Los módulos nombrados por capacidad son Screaming con fronteras reforzadas. Si la estructura de carpetas ya refleja capacidades, convertirlas en módulos es el paso siguiente.

### DDD
Los nombres de las capacidades deberían salir del **lenguaje ubicuo** del negocio. Una carpeta `Appointments` sale de hablar con quien gestiona la clínica; una carpeta `Managers` no sale de ninguna conversación.

## 4. Autocrítica: lo que gritan los samples de este catálogo

Los ejemplos de este repositorio se diseñaron para enseñar **arquitecturas**, y eso se nota en lo que gritan sus proyectos (nombres reales de las carpetas `sample/src`):

| Kit | Proyectos de `src/` | ¿Qué grita? |
|---|---|---|
| CLEAN | `TodoApp.Domain`, `.Application`, `.Infrastructure`, `.Api` | La arquitectura (capas de Clean) |
| HEXAGONAL | `Library.Core`, `Library.Adapters.Rest/.Cli/.Outbound` | Hexagonal (núcleo y adaptadores) |
| ONION | `Shop.Domain.Model`, `Shop.Domain.Services`, `Shop.Application`... | Onion (anillos) |
| DDD | `Hotel.Domain`, `.Application`, `.Infrastructure`, `.Api` | Capas con DDD |
| CQRS | `Bank.Domain`, `.Commands`, `.Queries`, `.Infrastructure` | CQRS (lado de escritura y de lectura) |
| LAYERED | `Lending.Data`, `.Business`, `.Web` | Capas |
| VERTICAL-SLICE | `Helpdesk.Api` con `Features/OpenTicket`, `AssignTicket`, `CloseTicket`... | **El negocio** (un helpdesk con tickets) |
| MODULAR-MONOLITH | `Modules/Catalog`, `Ordering`, `Notifications` | **El negocio** (tienda) |
| EVENT-DRIVEN | `Eda.Orders`, `.Inventory`, `.Notifications`, `.Messaging` | Mezcla: participantes del negocio + plumbing |
| SCREAMING | `Clinic/Patients`, `Appointments`, `Billing` | **El negocio** (clínica) |

Es una consecuencia razonable de lo que cada kit enseña: en un kit de Clean quieres que se vean las capas de Clean. Pero conviene saberlo: **un proyecto de producción rara vez debería copiar esa estructura tal cual**; lo habitual es combinar: carpetas por capacidad en primer nivel, y las capas de la arquitectura elegida *dentro* de cada capacidad.

## 5. Cuándo aplicarlo

### Siempre (casi)
Es gratis: solo cambia cómo nombras y agrupas. Aplícalo desde el primer commit.

### Con cuidado cuando
- Hay muchísimo código compartido real entre áreas: podrías necesitar un núcleo común, pero mantenlo mínimo.
- El equipo ya tiene una estructura técnica grande: migrar de golpe es costoso; hazlo capacidad a capacidad.
- Las "capacidades" no están claras: primero entiende el negocio (DDD estratégico).

## 6. Cómo se combina

```
Raíz del proyecto:     capacidades del negocio       ← Screaming
  dentro de cada una:  casos de uso (Vertical Slice) ← o capas, o la arquitectura que prefieras
  entre ellas:         una dirección + interfaces del consumidor
  fronteras fuertes:   convertirlas en módulos      ← Modular Monolith
  reglas internas:     DDD táctico                  ← si el dominio lo merece
```

## 7. Errores al comparar

- "Screaming Architecture es una arquitectura nueva": es un principio sobre la estructura.
- "Si uso Clean ya grito el negocio": Clean define dependencias; el árbol puede seguir diciendo "Domain, Application, Infrastructure".
- "Es solo renombrar carpetas": si las clases siguen acopladas al framework, solo cambió la fachada. El criterio del artículo incluye poder probar los casos de uso sin servidor web.
- "Una lista de palabras prohibidas garantiza que se grita el negocio": solo detecta el error común; nombrar bien sigue siendo juicio humano.
- "No puede haber capas": pueden existir *dentro* de una capacidad.
