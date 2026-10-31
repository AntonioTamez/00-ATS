# Layered / N-Tier vs Clean / Hexagonal / Onion / Vertical Slice / DDD / Modular Monolith

La arquitectura en capas es el **punto de partida** del catálogo: responde a "¿cómo separo presentación, reglas y datos?" con una dirección de dependencias hacia abajo. Casi todas las demás nacen para resolver algo que las capas dejan abierto.

> Fuentes: [Microsoft – N-tier](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/n-tier), [Microsoft – Architecture styles](https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/), [Fowler – PresentationDomainDataLayering](https://martinfowler.com/bliki/PresentationDomainDataLayering.html). Ejemplos: [`LAYERED/sample`](../sample), [`CLEAN/sample`](../../CLEAN/sample), [`HEXAGONAL/sample`](../../HEXAGONAL/sample), [`ONION/sample`](../../ONION/sample), [`VERTICAL-SLICE/sample`](../../VERTICAL-SLICE/sample), [`DDD/sample`](../../DDD/sample).

---

## 1. La diferencia de fondo: hacia dónde apunta la flecha

```
LAYERED                 CLEAN / ONION / HEXAGONAL
Web  → Business → Data   Web ─┐
                         Data ┴─► Business/Dominio     (el negocio no depende de nadie)
```

| | Layered | Clean / Onion / Hexagonal |
|---|---|---|
| El negocio depende de… | **La capa de datos** (clases concretas) | **Nada**: define interfaces que los detalles implementan |
| Probar el negocio sin base real | Difícil: no hay seam | Natural: se pasa un fake |
| Cambiar la base de datos | Toca el negocio | Toca solo un adaptador |
| Coste | Menos proyectos y menos conceptos | Interfaces, adaptadores, composition root |
| Curva | Baja (*"less learning curve for most developers"*, Microsoft) | Media |

Fowler lo describe como una variación de las capas: *"arrange things so that the domain does not depend on its data sources"*. En el sample, el test `Characterization_BusinessServicesDependOnConcreteDataClasses` lo hace visible: los servicios de negocio reciben `BookRepository`, una clase concreta.

## 2. Frente a cada una

### Clean / Onion / Hexagonal
Son las capas **con la dependencia invertida**. Si tu problema es "no puedo probar el negocio sin la base" o "cambiar de almacenamiento me obliga a tocar reglas", esta es la evolución natural. Si ese problema no existe, la inversión añade conceptos que no pagan.

### Vertical Slice
Cambia el **eje de corte**. Layered corta por capa técnica (todo lo de "préstamos" está repartido en tres proyectos); Vertical Slice corta por funcionalidad (todo "préstamos" junto). Microsoft señala la limitación del corte horizontal: *"can make it difficult to introduce changes without affecting multiple parts of the application"*. En el [sample de Vertical Slice](../../VERTICAL-SLICE/sample) añadir una funcionalidad es crear una carpeta; en este, es tocar cada capa.

Dato medido en este kit: añadir un campo `Isbn` de punta a punta en el sample de capas tocó **3 archivos y 17 líneas**. Es poco; el problema real aparece cuando el sistema y el equipo crecen.

### DDD táctico
Responde a otra pregunta: **cómo modelar las reglas**. En capas, las reglas viven en *servicios* que manipulan filas (`LoanService` cambia `LoanRecord.ReturnedOn`): es el patrón que DDD llama modelo anémico. Con DDD, las reglas pasan a los objetos del dominio. Se combinan: la capa de negocio puede contener un modelo rico.

### Modular Monolith
Aplica **otra división**: por capacidad de negocio, con fronteras impuestas. Se combina con capas: cada módulo puede tener sus propias capas por dentro.

### Microservicios
Capas es **lógica** (dentro de un proceso). Microservicios es **física** (procesos separados). Un microservicio puede tener sus capas dentro. Microsoft: *"Monolithic design prevents independent deployment of features"* es un límite de N-tier que los microservicios resuelven a un alto coste.

### MVC / MVVM (siguiente tema)
Son patrones de **presentación**. Viven *dentro* de la capa de presentación; no sustituyen a la arquitectura de capas.

## 3. Capas cerradas vs abiertas

| | Cerradas (sample) | Abiertas |
|---|---|---|
| Regla | Cada capa solo llama a la inmediata inferior | Puede llamar a cualquiera inferior |
| Ventaja | Menos dependencias; cambios aislados | Menos código de "paso" |
| Coste | Código que solo reenvía | Más acoplamiento |

Microsoft: la cerrada *"limits the dependencies between layers"* pero *"might create unnecessary network traffic if one layer only passes requests along"* (en tiers físicos).

## 4. Cuándo usar Layered

### Úsalo cuando
- El dominio es **tradicional y estable**, con reglas modestas.
- El equipo es pequeño y conoce el patrón.
- Se **migra una aplicación existente** con cambios mínimos.
- Los requisitos **aún evolucionan** y no quieres sobre-diseñar (Microsoft).

### Replantéalo cuando
- Cada funcionalidad nueva toca las tres capas y eso frena al equipo → Vertical Slice.
- No puedes probar el negocio sin la base → Clean/Hexagonal/Onion.
- La capa de negocio es solo CRUD de paso (*"adds latency and complexity without delivering meaningful value"*, Microsoft) → simplifica.
- El negocio crece y los servicios acumulan condicionales → DDD táctico.

## 5. Cómo se combina

```
Layered           → la estructura base conocida por todos
 └ dentro de cada módulo de un Modular Monolith
 └ con DDD táctico dentro de la capa de negocio
 └ evolucionando a Clean/Onion/Hexagonal al invertir la dependencia con datos
 └ la capa de presentación puede usar MVC, MVVM o simples endpoints
```

## 6. Qué muestra cada sample

| Aspecto | **LAYERED** | CLEAN | VERTICAL SLICE | DDD |
|---|---|---|---|---|
| Dependencia del negocio con datos | **Concreta, hacia abajo** | Invertida (interfaz) | Directa al `DbContext` | Invertida (repositorio) |
| Se corta por | **Capa técnica** | Capa de dependencias | Funcionalidad | Agregado |
| Modelo del negocio | **Servicios + filas (anémico)** | Entidades | Entidad compartida | Agregados ricos |
| Probar negocio | **Con la capa de datos real** | Con fakes | Con la base | Con fakes |
| Reglas verificadas | **Capas cerradas; filas no salen** | Dependencias entre capas | Aislamiento entre slices | Reglas tácticas |

## 7. Errores al comparar

- "Capas = mala arquitectura": no. Es suficiente y adecuada en muchos casos; el problema es aplicarla donde ya no basta, o hacerla mal (capas que solo reenvían, filas expuestas).
- "N-tier = tres servidores": las capas son lógicas; los niveles, físicos. Se pueden tener 3 capas en 1 nivel (como este sample).
- "Clean es capas + interfaces": la clave es la **inversión de la dependencia**, no el número de capas.
- "Layered no se puede probar": se puede, sobre una capa de datos en memoria como aquí; lo que no tiene es el *seam* para sustituirla.
- "Si uso Vertical Slice ya no tengo capas": las slices pueden tener capas finas por dentro.
