# Frontend (Atomic Design + Container/Presentational + Micro-frontends) – learning kit

| Path | What it is |
|---|---|
| `docs/GUIA-FRONTEND.md` | Step-by-step guide (Spanish): the five atomic levels, pure presentational components, containers that own state, micro-frontends loaded by a shell, contract and events, honest limits |
| `docs/FRONTEND-VS-OTRAS.md` | Comparison with Screaming Architecture, MVC/MVVM, Microservices, Modular Monolith and Microkernel; when to use and combine (Spanish) |
| `sample/` | Reference project, **no dependencies and no build step** (plain JavaScript, Node's built-in test runner): `ui-runtime`, `contracts`, `design-system` (atoms, molecules), two micro-frontends (`catalog`, `cart`) and a `shell`; 116 tests incl. architecture rules |
| `portal/index.html` | Interactive beginner portal (open it in a browser, no build needed) |

Run: `cd sample && npm test` (or `node --test`), and `npm start` (or `node tools/serve.mjs`) to open the shop at `http://localhost:5173`.
The browser needs a server because it refuses ES modules from `file://`. The tests use a fake DOM; the real page was also driven once in headless Chrome 155 (add to cart, search, cart and header updates, no page errors). The portal was driven in the same Chrome too (classifier, presentational lab, shell lab, quiz; no console or page errors). Other browsers were not tried.
