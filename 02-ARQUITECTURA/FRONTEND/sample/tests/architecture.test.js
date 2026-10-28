import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { describe, it } from 'node:test';
import * as designSystem from '../packages/design-system/index.js';
import { validateMicroFrontend } from '../packages/contracts/index.js';
import { manifest } from '../packages/shell/manifest.js';

// The rules that make this an Atomic Design + Container/Presentational + Micro-frontends codebase, as executable checks.
// They read the source files and the imports between them; nothing here depends on how the code looks at run time.

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const PACKAGES = join(ROOT, 'packages');

const RANK = { foundations: 0, atoms: 1, molecules: 2, organisms: 3, templates: 4, pages: 5, containers: 6 };
const PRESENTATIONAL_MAX_RANK = RANK.pages;
const ALLOWED_PACKAGE_DEPENDENCIES = {
  'ui-runtime': [],
  contracts: [],
  'design-system': ['ui-runtime'],
  catalog: ['ui-runtime', 'contracts', 'design-system'],
  cart: ['ui-runtime', 'contracts', 'design-system'],
  shell: ['ui-runtime', 'contracts', 'design-system'],
};
const CLASS_PREFIX = { 'design-system': 'ds-', catalog: 'catalog-', cart: 'cart-', shell: 'shell-' };
const MICRO_FRONTENDS = ['catalog', 'cart'];

function listJs(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) return listJs(path);
    return entry.name.endsWith('.js') ? [path] : [];
  });
}

const stripComments = (source) => source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:'"`])\/\/.*$/gm, '$1');

const FILES = listJs(PACKAGES).map((path) => {
  const rel = relative(PACKAGES, path).split(sep);
  const source = stripComments(readFileSync(path, 'utf8'));
  const pkg = rel[0];
  const folder = rel.length > 2 ? rel[1] : null;
  const imports = [];
  for (const match of source.matchAll(/^\s*(?:import|export)\s+(?:([^;'"]*?)\s+from\s+)?['"]([^'"]+)['"]/gm)) {
    const names = [...(match[1] ?? '').matchAll(/[{,]\s*(\w+)(?:\s+as\s+\w+)?/g)].map((m) => m[1]);
    imports.push({ specifier: match[2], names, dynamic: false });
  }
  for (const match of source.matchAll(/import\(\s*['"]([^'"]+)['"]\s*\)/g)) {
    imports.push({ specifier: match[1], names: [], dynamic: true });
  }
  for (const imported of imports) {
    imported.target = imported.specifier.startsWith('.') ? resolve(dirname(path), imported.specifier) : null;
    imported.targetRel = imported.target ? relative(PACKAGES, imported.target).split(sep) : null;
  }
  return { path, name: rel.join('/'), pkg, folder, rank: RANK[folder] ?? null, source, imports };
});

const byName = (name) => FILES.find((file) => file.name === name);
const inFolders = (...folders) => FILES.filter((file) => folders.includes(file.folder));
const presentational = () => FILES.filter((file) => file.rank !== null && file.rank <= PRESENTATIONAL_MAX_RANK);
const fileOf = (imported) => FILES.find((file) => file.path === imported.target);

describe('the import scanner these rules rely on', () => {
  it('reads re-exports one statement at a time and ignores the word "export" inside strings', () => {
    assert.deepEqual(byName('contracts/index.js').imports, []);
    assert.equal(byName('design-system/index.js').imports.length, 9);
    assert.deepEqual(byName('design-system/index.js').imports[0].names, ['formatMoney']);
  });

  it('finds every named import of a multi-line import', () => {
    const imported = byName('cart/containers/CartContainer.js').imports.find((i) => i.specifier === '../domain/cart.js');
    assert.deepEqual(imported.names, ['addItem', 'emptyCart', 'quantityOf', 'removeItem', 'setQuantity', 'totals']);
  });

  it('sees all the files', () => {
    assert.equal(FILES.length, 36);
  });
});

describe('the code is organized by atomic level', () => {
  it('every file under packages/ is in a known place', () => {
    for (const file of FILES) {
      const known = ['foundations', 'atoms', 'molecules', 'organisms', 'templates', 'pages', 'containers', 'domain', 'api'];
      const isEntry = file.folder === null;
      assert.ok(isEntry || known.includes(file.folder), `${file.name} is in an unknown folder '${file.folder}'`);
    }
  });

  it('a layer only imports from layers BELOW it (organisms may nest), inside the same package', () => {
    for (const file of FILES.filter((f) => f.rank !== null)) {
      for (const imported of file.imports.filter((i) => i.targetRel?.[0] === file.pkg)) {
        const target = fileOf(imported);
        if (target?.rank == null) continue;
        // Atoms contain no atoms and molecules no molecules. Organisms may be built from other organisms (this sample's choice).
        const nestedOrganism = file.folder === 'organisms' && target.folder === 'organisms';
        assert.ok(target.rank < file.rank || nestedOrganism, `${file.name} (${file.folder}) must not import ${target.name} (${target.folder})`);
      }
    }
  });

  it('atoms and molecules live in the design system; organisms, templates and pages live in the micro-frontends', () => {
    for (const file of inFolders('atoms', 'molecules', 'foundations')) assert.equal(file.pkg, 'design-system', file.name);
    for (const file of inFolders('organisms', 'templates', 'pages', 'containers', 'domain', 'api')) {
      assert.ok(MICRO_FRONTENDS.includes(file.pkg), `${file.name} must belong to a micro-frontend`);
    }
  });

  it('the design system exposes exactly its atoms, molecules and helpers', () => {
    assert.deepEqual(Object.keys(designSystem).sort(), [
      'Badge', 'Button', 'Card', 'Heading', 'Price', 'QuantityStepper', 'SearchField', 'TextInput', 'VERSION', 'formatMoney',
    ]);
  });

  it('organisms and pages exist in both micro-frontends, and each has a template and a container', () => {
    for (const mfe of MICRO_FRONTENDS) {
      for (const folder of ['organisms', 'templates', 'pages', 'containers']) {
        assert.ok(FILES.some((f) => f.pkg === mfe && f.folder === folder), `${mfe} has no ${folder}`);
      }
    }
  });
});

describe('presentational components are pure functions of their props', () => {
  it('they import only lower layers, domain rules, the design system and the element factory h', () => {
    for (const file of presentational()) {
      for (const imported of file.imports) {
        if (!imported.target) assert.fail(`${file.name} imports the bare specifier '${imported.specifier}'`);
        const [pkg, second] = imported.targetRel;
        if (pkg === file.pkg) {
          const target = fileOf(imported);
          assert.ok(target.rank !== null || target.folder === 'domain', `${file.name} imports ${target.name}, which is not presentational or domain`);
          continue;
        }
        const isDesignSystem = pkg === 'design-system' && imported.targetRel.join('/') === 'design-system/index.js' && file.pkg !== 'design-system';
        const isRuntime = imported.targetRel.join('/') === 'ui-runtime/index.js';
        assert.ok(isDesignSystem || isRuntime, `${file.name} imports ${imported.specifier} (${pkg}/${second ?? ''})`);
        if (isRuntime) {
          assert.deepEqual(imported.names.filter((name) => !['h', 'Fragment'].includes(name)), [], `${file.name} imports more than h from ui-runtime`);
        }
      }
    }
  });

  it('they never touch the network, storage, the browser, time, randomness, state or events', () => {
    const forbidden = /\b(fetch|XMLHttpRequest|localStorage|sessionStorage|document|window|setTimeout|setInterval|Date|Math\.random|getState|setState|publish|subscribe|addEventListener)\b/;
    for (const file of presentational()) {
      const hit = forbidden.exec(file.source);
      assert.equal(hit, null, `${file.name} uses '${hit?.[0]}'`);
    }
  });

  it('they are plain exported functions, not classes, and have no module-level state', () => {
    for (const file of presentational().filter((f) => f.folder !== 'foundations')) {
      assert.doesNotMatch(file.source, /\bclass\s+\w+/, `${file.name} declares a class`);
      assert.doesNotMatch(file.source, /^(let|var)\s/m, `${file.name} has module-level mutable state`);
    }
  });
});

describe('containers own state and effects, and write no markup', () => {
  it('they import only pages, domain rules, the store and the contracts', () => {
    for (const file of inFolders('containers')) {
      for (const imported of file.imports) {
        const label = `${file.name} imports ${imported.specifier}`;
        const path = imported.targetRel.join('/');
        if (imported.targetRel[0] === file.pkg) {
          assert.ok(['pages', 'domain'].includes(imported.targetRel[1]), label);
        } else {
          assert.ok(['ui-runtime/index.js', 'contracts/index.js'].includes(path), label);
          if (path === 'ui-runtime/index.js') {
            assert.deepEqual(imported.names, ['createStore'], `${file.name} takes more than createStore from ui-runtime`);
          }
        }
      }
    }
  });

  it('they never build elements: no h(...) and no tag names', () => {
    for (const file of inFolders('containers')) {
      assert.doesNotMatch(file.source, /\bh\s*\(/, `${file.name} calls h`);
      assert.doesNotMatch(file.source, /<\w+[\s>]/, `${file.name} contains markup`);
    }
  });

  it('every micro-frontend has a container that exposes its store, actions and view', () => {
    for (const file of inFolders('containers')) {
      assert.match(file.source, /return\s*\{[^}]*\bstore\b[^}]*\bactions\b[^}]*\bview\b|return\s*\{[^}]*\bstore\b[^}]*\bview\b/, file.name);
    }
  });
});

describe('domain rules and data access are kept out of the UI', () => {
  it('domain files are pure: they import only other domain files and touch no browser or network API', () => {
    for (const file of inFolders('domain')) {
      for (const imported of file.imports) assert.equal(imported.targetRel[1], 'domain', `${file.name} imports ${imported.specifier}`);
      assert.doesNotMatch(file.source, /\b(fetch|document|window|localStorage|Date|Math\.random|setTimeout)\b/, file.name);
    }
  });

  it('only api files may import nothing from the UI, and nobody but the micro-frontend entry imports them', () => {
    for (const file of inFolders('api')) assert.deepEqual(file.imports, [], `${file.name} imports something`);
    for (const file of FILES.filter((f) => f.folder !== null && f.folder !== 'api')) {
      assert.ok(
        file.imports.every((i) => i.targetRel?.[1] !== 'api'),
        `${file.name} imports an api file; only the entry point of the micro-frontend may`,
      );
    }
  });
});

describe('micro-frontends are independent', () => {
  it('packages only depend on the packages they are allowed to', () => {
    for (const file of FILES) {
      for (const imported of file.imports) {
        const targetPackage = imported.targetRel?.[0];
        if (targetPackage === undefined || targetPackage === file.pkg) continue;
        assert.ok(
          ALLOWED_PACKAGE_DEPENDENCIES[file.pkg].includes(targetPackage),
          `${file.pkg} must not depend on ${targetPackage} (${file.name})`,
        );
      }
    }
  });

  it('no micro-frontend imports another, in any direction', () => {
    for (const file of FILES.filter((f) => MICRO_FRONTENDS.includes(f.pkg))) {
      const others = MICRO_FRONTENDS.filter((name) => name !== file.pkg);
      for (const imported of file.imports) {
        assert.ok(!others.includes(imported.targetRel?.[0]), `${file.name} imports ${imported.specifier}`);
      }
    }
  });

  it('other packages are used only through their public index.js, never through a deep path', () => {
    for (const file of FILES) {
      for (const imported of file.imports) {
        const targetPackage = imported.targetRel?.[0];
        if (targetPackage === undefined || targetPackage === file.pkg) continue;
        assert.deepEqual(imported.targetRel, [targetPackage, 'index.js'], `${file.name} reaches into ${imported.specifier}`);
      }
    }
  });

  it('the shell never imports a micro-frontend: it only knows their URLs from the manifest', () => {
    for (const file of FILES.filter((f) => f.pkg === 'shell')) {
      for (const imported of file.imports) {
        assert.ok(!MICRO_FRONTENDS.includes(imported.targetRel?.[0]), `${file.name} imports ${imported.specifier}`);
        if (imported.dynamic) assert.fail(`${file.name} has a literal dynamic import of '${imported.specifier}'`);
      }
    }
    assert.match(byName('shell/loader.js').source, /import\(url\)/);
  });

  it('the manifest points at existing micro-frontends whose contract checks out', async () => {
    for (const entry of manifest) {
      const file = resolve(PACKAGES, 'shell', entry.url);
      assert.ok(existsSync(file), `${entry.name}: ${entry.url} does not exist`);
      const module = await import(pathToFileURL(file).href);
      assert.doesNotThrow(() => validateMicroFrontend(module.mfe, entry), entry.name);
      assert.ok(MICRO_FRONTENDS.includes(entry.name));
      assert.equal(entry.requires['design-system'], 1);
    }
    assert.deepEqual(manifest.map((entry) => entry.name).sort(), [...MICRO_FRONTENDS].sort());
  });

  it('there are no import cycles', () => {
    const state = new Map();
    const visit = (file, trail) => {
      if (state.get(file) === 'done') return;
      assert.notEqual(state.get(file), 'visiting', `cycle: ${[...trail, file.name].join(' -> ')}`);
      state.set(file, 'visiting');
      for (const imported of file.imports) {
        const target = fileOf(imported);
        if (target) visit(target, [...trail, file.name]);
      }
      state.set(file, 'done');
    };
    FILES.forEach((file) => visit(file, []));
  });
});

describe('styles do not leak between micro-frontends', () => {
  it('every class a package renders carries that package prefix', () => {
    for (const file of FILES) {
      const prefix = CLASS_PREFIX[file.pkg];
      if (!prefix) continue; // ui-runtime and contracts render nothing of their own
      for (const match of file.source.matchAll(/className:\s*(['"`])((?:(?!\1).)*)\1/g)) {
        const tokens = match[2].replace(/\$\{[^}]*\}/g, '').split(/\s+/).filter(Boolean);
        for (const token of tokens) assert.ok(token.startsWith(prefix), `${file.name}: class '${token}' must start with '${prefix}'`);
      }
    }
  });

  it('every class selector in styles.css belongs to a known prefix', () => {
    const css = readFileSync(join(ROOT, 'styles.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
    const prefixes = Object.values(CLASS_PREFIX);
    const classes = [...css.matchAll(/\.([a-z][\w-]*)/g)].map((m) => m[1]);
    assert.ok(classes.length > 10);
    for (const name of classes) assert.ok(prefixes.some((prefix) => name.startsWith(prefix)), `styles.css: '.${name}' has no known prefix`);
  });
});
