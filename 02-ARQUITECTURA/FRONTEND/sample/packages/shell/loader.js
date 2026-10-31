import { renderStatic } from '../ui-runtime/index.js';
import { majorOf, validateMicroFrontend } from '../contracts/index.js';
import { FailedRemote } from './FailedRemote.js';

/**
 * The shell's job: load each micro-frontend from the manifest, check it, and mount it in its slot.
 * A micro-frontend that cannot be loaded, does not honor the contract, needs another major version of the design
 * system, or throws while mounting is replaced by a message in its slot. The others are not affected.
 */
export function createShell({ manifest, importer = (url) => import(url), designSystemVersion, bus, services = {} }) {
  let mounted = [];

  async function load(entry, slots) {
    const root = slots[entry.slot];
    try {
      if (!root) throw new Error(`the slot '${entry.slot}' does not exist`);

      const required = entry.requires?.['design-system'];
      if (required !== undefined && majorOf(designSystemVersion) !== required) {
        throw new Error(`it needs design-system ${required}.x but the shell provides ${designSystemVersion}`);
      }

      const module = await importer(entry.url);
      validateMicroFrontend(module.mfe, entry);
      const unmount = await module.mfe.mount(root, { bus, services });
      return { name: entry.name, unmount: typeof unmount === 'function' ? unmount : () => {} };
    } catch (error) {
      if (root) renderStatic(root, FailedRemote({ name: entry.name, reason: error.message }));
      return { name: entry.name, failure: error.message };
    }
  }

  return {
    /** Loads every entry at the same time. The report lists them in manifest order. */
    async start(slots) {
      const results = await Promise.all(manifest.map((entry) => load(entry, slots)));
      mounted = results.filter((result) => !result.failure);
      return {
        mounted: mounted.map((result) => result.name),
        failed: results.filter((result) => result.failure).map((result) => ({ name: result.name, reason: result.failure })),
      };
    },

    /** Unmounts in reverse order. One that throws does not stop the others. */
    async stop() {
      for (const remote of mounted.reverse()) {
        try {
          await remote.unmount();
        } catch {
          // a micro-frontend that fails to unmount must not leave the others mounted
        }
      }
      mounted = [];
    },
  };
}
