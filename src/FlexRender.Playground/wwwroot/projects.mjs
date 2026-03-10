// Project management for FlexRender Playground.
// Each project bundles YAML template, JSON data, and VFS files together.
// Persistence via IndexedDB; VFS is purely in-memory — this module owns storage.

const DB_NAME = 'flexrender-projects';
const DB_VERSION = 1;
const STORE_NAME = 'projects';
const LS_KEY = 'flexrender-current-project';

/** @typedef {{path: string, data: Uint8Array, type: string}} VfsFile */
/** @typedef {{id: string, name: string, yaml: string, json: string, files: VfsFile[], isExample: boolean, createdAt: number, updatedAt: number}} Project */

/** @type {IDBDatabase|null} */
let db = null;

function openDb() {
    return new Promise((resolve, reject) => {
        if (db) { resolve(db); return; }
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const database = req.result;
            if (!database.objectStoreNames.contains(STORE_NAME)) {
                database.createObjectStore(STORE_NAME, { keyPath: 'id' });
            }
        };
        req.onsuccess = () => {
            db = req.result;
            db.onversionchange = () => { db.close(); db = null; };
            resolve(db);
        };
        req.onerror = () => reject(req.error);
    });
}

function txReadOnly() {
    return db.transaction(STORE_NAME, 'readonly').objectStore(STORE_NAME);
}

function txReadWrite() {
    return db.transaction(STORE_NAME, 'readwrite').objectStore(STORE_NAME);
}

function reqToPromise(req) {
    return new Promise((resolve, reject) => {
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

/**
 * Serialize a project for IndexedDB storage.
 * Uint8Array instances must be converted to ArrayBuffer for structured clone.
 */
function serializeForDb(project) {
    return {
        ...project,
        files: project.files.map(f => ({
            path: f.path,
            type: f.type,
            data: f.data.buffer.slice(f.data.byteOffset, f.data.byteOffset + f.data.byteLength),
        })),
    };
}

/**
 * Deserialize a project from IndexedDB storage.
 * ArrayBuffer instances are converted back to Uint8Array.
 */
function deserializeFromDb(record) {
    if (!record) return null;
    return {
        ...record,
        files: (record.files || []).map(f => ({
            path: f.path,
            type: f.type,
            data: new Uint8Array(f.data),
        })),
    };
}

/**
 * Initialize the projects database. Returns the list of all projects (summary only).
 * @returns {Promise<{id: string, name: string, isExample: boolean, updatedAt: number}[]>}
 */
export async function init() {
    await openDb();
    return listProjects();
}

/**
 * List all projects (summary: id, name, isExample, updatedAt).
 * @returns {Promise<{id: string, name: string, isExample: boolean, updatedAt: number}[]>}
 */
export async function listProjects() {
    const store = txReadOnly();
    const all = await reqToPromise(store.getAll());
    return all.map(p => ({
        id: p.id,
        name: p.name,
        isExample: p.isExample,
        updatedAt: p.updatedAt,
    })).sort((a, b) => {
        // Examples first, then by name
        if (a.isExample !== b.isExample) return a.isExample ? -1 : 1;
        return a.name.localeCompare(b.name);
    });
}

/**
 * Load a full project by ID.
 * @param {string} id
 * @returns {Promise<Project|null>}
 */
export async function loadProject(id) {
    const store = txReadOnly();
    const record = await reqToPromise(store.get(id));
    return deserializeFromDb(record);
}

/**
 * Upsert a project. Updates the updatedAt timestamp.
 * @param {Project} project
 * @returns {Promise<void>}
 */
export async function saveProject(project) {
    project.updatedAt = Date.now();
    const store = txReadWrite();
    await reqToPromise(store.put(serializeForDb(project)));
}

/**
 * Delete a project by ID. Prevents deleting example projects.
 * @param {string} id
 * @returns {Promise<boolean>} true if deleted, false if prevented
 */
export async function deleteProject(id) {
    const project = await loadProject(id);
    if (!project) return false;
    if (project.isExample) return false;
    const store = txReadWrite();
    await reqToPromise(store.delete(id));
    return true;
}

/**
 * Create a new empty project.
 * @param {string} name
 * @returns {Promise<Project>}
 */
export async function createProject(name) {
    const now = Date.now();
    const project = {
        id: crypto.randomUUID(),
        name,
        yaml: '',
        json: '{}',
        files: [],
        isExample: false,
        createdAt: now,
        updatedAt: now,
    };
    const store = txReadWrite();
    await reqToPromise(store.put(serializeForDb(project)));
    return project;
}

/**
 * Create or reset an example project with the given slug, name, yaml, json, and files.
 * @param {string} id - Stable slug ID for the example
 * @param {string} name
 * @param {string} yaml
 * @param {string} json
 * @param {VfsFile[]} files
 * @returns {Promise<Project>}
 */
export async function seedExample(id, name, yaml, json, files = []) {
    const now = Date.now();
    const project = {
        id,
        name,
        yaml,
        json,
        files,
        isExample: true,
        createdAt: now,
        updatedAt: now,
    };
    const store = txReadWrite();
    await reqToPromise(store.put(serializeForDb(project)));
    return project;
}

/**
 * Check if a project exists by ID.
 * @param {string} id
 * @returns {Promise<boolean>}
 */
export async function projectExists(id) {
    const store = txReadOnly();
    const key = await reqToPromise(store.getKey(id));
    return key !== undefined;
}

/**
 * Get the last-used project ID from localStorage.
 * @returns {string|null}
 */
export function getCurrentProjectId() {
    return localStorage.getItem(LS_KEY);
}

/**
 * Set the last-used project ID in localStorage.
 * @param {string} id
 */
export function setCurrentProjectId(id) {
    localStorage.setItem(LS_KEY, id);
}
