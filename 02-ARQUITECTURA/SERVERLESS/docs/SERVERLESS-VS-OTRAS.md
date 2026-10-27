# Serverless / FaaS vs otras arquitecturas

Serverless responde una pregunta distinta de las demás: **"¿quién opera el código y cómo se ejecuta?"**. Clean y Hexagonal hablan de cómo se estructura el código; Microservicios, de cómo se parte el sistema; Serverless, de **qué pasa con tu código una vez escrito**: lo dispara una plataforma, corre en instancias efímeras y se paga (o se escala) por uso.

> Fuentes: [Martin Fowler – Serverless Architectures](https://martinfowler.com/articles/serverless.html), [Azure Functions best practices](https://learn.microsoft.com/en-us/azure/azure-functions/functions-best-practices), [Performance and reliability](https://learn.microsoft.com/en-us/azure/azure-functions/performance-reliability). Ejemplos del repo: [`SERVERLESS/sample`](../sample), [`EVENT-DRIVEN/`](../../EVENT-DRIVEN), [`MICROSERVICIOS/`](../../MICROSERVICIOS), [`HEXAGONAL/`](../../HEXAGONAL), [`PIPES-AND-FILTERS/`](../../PIPES-AND-FILTERS).

---

## 1. Qué organiza cada una

| Arquitectura | Qué organiza | Unidad |
|---|---|---|
| Layered / Clean / Hexagonal | La estructura interna del código | Capa / anillo / puerto |
| Modular Monolith | Los módulos de un solo despliegue | Módulo |
| Microservicios | El sistema partido en servicios que se despliegan solos | Servicio |
| Event-Driven | Quién reacciona a qué ocurrió | Evento |
| **Serverless / FaaS** | **Cómo se ejecuta el código: disparado, efímero, gestionado** | **Función** |

Serverless **no compite** con la mayoría: es un modelo de ejecución sobre el que podés aplicar casi cualquiera de las otras.

## 2. Serverless vs Microservicios

| | Microservicios | Serverless / FaaS |
|---|---|---|
| Unidad de despliegue | Un servicio, normalmente con su proceso siempre encendido | Una función (o un conjunto) que la plataforma enciende cuando llega un evento |
| Quién opera la infraestructura | Tu equipo (o una plataforma que armaste) | El proveedor |
| Estado | Cada servicio puede tener su base y su memoria | La instancia es efímera: "you should not assume that state from one invocation of a function will be available to another invocation" |
| Duración de una petición | Libre | Limitada: "FaaS functions are typically limited in how long each invocation is allowed to run" |
| Frontera de negocio | Sí (un servicio por capacidad) | No necesariamente: una función es una pieza de ejecución, no una capacidad de negocio |

Se combinan: un microservicio puede estar hecho de funciones, y una función puede ser una pieza de un servicio. La diferencia clave es que "microservicio" es una decisión sobre **cómo dividir**, y "serverless" una decisión sobre **dónde y cómo correr**.

## 3. Serverless vs Event-Driven

| | Event-Driven | Serverless / FaaS |
|---|---|---|
| Pregunta | ¿Cómo se desacoplan emisores y receptores? | ¿Quién ejecuta al receptor? |
| Transporte | Bus o broker | La plataforma lo conecta con un disparador |
| Garantías | Dependen del broker | Aquí: "at-least-once": "the same event could be processed twice" |

Son aliados naturales: en este sample, `PlaceOrder` publica un mensaje y `ReserveStock` reacciona. **Event-Driven describe el flujo; Serverless, quién corre cada paso.** Todo lo que sabés de idempotencia y duplicados en Event-Driven aplica sin cambios.

## 4. Serverless vs Hexagonal

Se llevan bien. Una función es un **adaptador de entrada**: recibe un evento de la plataforma, lo traduce y llama a la lógica. Lo que escribas dentro de la función debería ser lo mínimo: parsear, llamar al núcleo, devolver la respuesta. En este sample las funciones todavía mezclan algo de lógica (por ejemplo, validar el pedido en `PlaceOrder`); en un sistema real convendría moverla a un núcleo sin ninguna referencia a la plataforma, para poder cambiar de proveedor sin reescribirla. El riesgo que señala Fowler es justo ese: "whatever Serverless features you’re using from one vendor will be implemented differently by another vendor".

## 5. Serverless vs Modular Monolith

| | Modular Monolith | Serverless / FaaS |
|---|---|---|
| Despliegue | Uno solo | Por función (o por aplicación de funciones) |
| Llamadas entre piezas | En proceso, baratas y transaccionales | Por colas o red: asíncronas, con fallos y duplicados |
| Pruebas | Todo en un proceso | Probar cada función es fácil; probar el conjunto no: "Integration testing Serverless apps, on the other hand, is hard." |

Si tu problema es organización del código y no escala o elasticidad, un monolito modular es más simple.

## 6. Serverless vs Pipes & Filters

Una cadena de funciones unidas por colas **es** un pipeline distribuido: cada función es un filtro y cada cola, una tubería. Todo lo que dice [PIPES-AND-FILTERS](../../PIPES-AND-FILTERS/docs/GUIA-PIPES-AND-FILTERS.md) sobre duplicados, idempotencia y fallos en filtros distribuidos pasa a ser real acá, porque la plataforma entrega al menos una vez.

## 7. Qué obtenés y qué pagás

| Ganás | Pagás |
|---|---|
| No operás servidores: la plataforma aprovisiona y escala | Dependés del proveedor: otra nube implementa lo mismo de otra manera |
| Elasticidad para cargas irregulares | Arranque en frío: crear una instancia nueva cuesta tiempo |
| Piezas chicas, fáciles de probar de a una | Probar el conjunto y depurar en producción es difícil |
| Reintentos y mensajes muertos listos | Tenés que ser idempotente y pensar en duplicados |
| Pagar por uso, en general | Duración limitada por invocación y estado externo siempre |

## 8. Cuándo elegir cada una

| Situación | Elección |
|---|---|
| Tareas disparadas por eventos o por reloj, con carga irregular | **Serverless / FaaS** |
| Webhooks, procesamiento al subir un archivo, colas de trabajo | **Serverless / FaaS** |
| Proceso largo o que necesita estado en memoria | Servicio siempre encendido |
| Organizar un dominio grande en áreas | Modular Monolith / Microservicios |
| Aislar la lógica de la tecnología | Hexagonal / Clean (también dentro de las funciones) |
| Reaccionar a hechos del negocio | Event-Driven (se puede implementar con funciones) |

## 9. Errores al comparar

- "Serverless = sin servidores": hay servidores; **no los operás vos**.
- "Serverless = microservicios chicos": una función no es una capacidad de negocio.
- "Una función por endpoint, y listo": sin estructura, el sistema se vuelve un conjunto de pegamentos duplicados.
- "Es siempre más barato": depende del patrón de uso; una carga constante y alta puede costar más que un servidor encendido.
- "Si lo pruebo en mi máquina, funciona en la nube": este sample, por ejemplo, **no mide** arranques en frío ni escalado.
