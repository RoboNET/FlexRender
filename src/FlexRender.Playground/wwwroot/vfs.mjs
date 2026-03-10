// Virtual File System for FlexRender Playground.
// Manages an in-memory Map<path, {data, type}> with IndexedDB persistence.

const DB_NAME = 'flexrender-vfs';
const DB_VERSION = 1;
const STORE_NAME = 'files';

/** @type {Map<string, {data: Uint8Array, type: string}>} */
const files = new Map();

/** @type {Set<(event: string, path: string) => void>} */
const listeners = new Set();

const FONT_EXT = new Set(['.ttf', '.otf', '.woff2', '.woff']);
const IMAGE_EXT = new Set(['.png', '.jpg', '.jpeg', '.svg', '.gif', '.webp', '.bmp']);
const CONTENT_EXT = new Set(['.ndc', '.txt', '.html', '.md']);

/** Detect resource type from file extension. */
export function detectType(path) {
    const ext = ('.' + path.split('.').pop()).toLowerCase();
    if (FONT_EXT.has(ext)) return 'font';
    if (IMAGE_EXT.has(ext)) return 'image';
    if (CONTENT_EXT.has(ext)) return 'content';
    return 'other';
}

/** Normalize path: strip leading ./ or /, collapse double slashes. */
function normalizePath(p) {
    p = p.replace(/\\/g, '/');
    if (p.startsWith('./')) p = p.slice(2);
    if (p.startsWith('/')) p = p.slice(1);
    return p.replace(/\/+/g, '/');
}

/** Subscribe to VFS changes. Callback receives (event, path). Events: 'add', 'remove', 'rename', 'clear'. */
export function subscribe(fn) {
    listeners.add(fn);
    return () => listeners.delete(fn);
}

function notify(event, path) {
    for (const fn of listeners) {
        try { fn(event, path); } catch (e) { console.warn('VFS listener error:', e); }
    }
}

// --- IndexedDB helpers ---

function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME);
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

async function dbPut(path, entry) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, 'readwrite');
        tx.objectStore(STORE_NAME).put({ data: entry.data.buffer, type: entry.type }, path);
        tx.oncomplete = () => { db.close(); resolve(); };
        tx.onerror = () => { db.close(); reject(tx.error); };
    });
}

async function dbDelete(path) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, 'readwrite');
        tx.objectStore(STORE_NAME).delete(path);
        tx.oncomplete = () => { db.close(); resolve(); };
        tx.onerror = () => { db.close(); reject(tx.error); };
    });
}

async function dbClear() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, 'readwrite');
        tx.objectStore(STORE_NAME).clear();
        tx.oncomplete = () => { db.close(); resolve(); };
        tx.onerror = () => { db.close(); reject(tx.error); };
    });
}

async function dbGetAll() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const req = store.openCursor();
        const entries = [];
        req.onsuccess = () => {
            const cursor = req.result;
            if (cursor) {
                entries.push({ path: cursor.key, data: new Uint8Array(cursor.value.data), type: cursor.value.type });
                cursor.continue();
            } else {
                db.close();
                resolve(entries);
            }
        };
        req.onerror = () => { db.close(); reject(req.error); };
    });
}

// --- Public API ---

/** Add or overwrite a file. */
export async function addFile(path, data, type) {
    path = normalizePath(path);
    if (!type) type = detectType(path);
    files.set(path, { data, type });
    await dbPut(path, { data, type });
    notify('add', path);
}

/** Remove a file. */
export async function removeFile(path) {
    path = normalizePath(path);
    if (!files.has(path)) return;
    files.delete(path);
    await dbDelete(path);
    notify('remove', path);
}

/** Rename/move a file. */
export async function renameFile(oldPath, newPath) {
    oldPath = normalizePath(oldPath);
    newPath = normalizePath(newPath);
    const entry = files.get(oldPath);
    if (!entry) return;
    files.delete(oldPath);
    files.set(newPath, entry);
    await dbDelete(oldPath);
    await dbPut(newPath, entry);
    notify('rename', oldPath);
    notify('add', newPath);
}

/** Get a file entry. */
export function getFile(path) {
    return files.get(normalizePath(path)) || null;
}

/** Get all file paths sorted. */
export function listFiles() {
    return [...files.keys()].sort();
}

/** Get all entries as [{path, data, type}]. */
export function allEntries() {
    return [...files.entries()].map(([path, entry]) => ({ path, ...entry }));
}

/** Check if a path exists. */
export function exists(path) {
    return files.has(normalizePath(path));
}

/** Clear all files. */
export async function clearAll() {
    files.clear();
    await dbClear();
    notify('clear', '');
}

/** Load all files from IndexedDB into memory. Call once at startup. */
export async function restore() {
    try {
        const entries = await dbGetAll();
        for (const e of entries) {
            files.set(e.path, { data: e.data, type: e.type });
        }
        return entries.length;
    } catch (err) {
        console.warn('VFS restore from IndexedDB failed:', err);
        return 0;
    }
}

/**
 * Build a tree structure from flat file paths.
 * Returns: { name, path, children, isDir, type? }[]
 */
export function buildTree() {
    const root = [];

    for (const [path, entry] of files) {
        const parts = path.split('/');
        let current = root;

        for (let i = 0; i < parts.length; i++) {
            const name = parts[i];
            const isLast = i === parts.length - 1;
            const partialPath = parts.slice(0, i + 1).join('/');

            let node = current.find(n => n.name === name);
            if (!node) {
                node = {
                    name,
                    path: partialPath,
                    isDir: !isLast,
                    children: [],
                    type: isLast ? entry.type : undefined,
                };
                current.push(node);
            }
            if (!isLast) {
                node.isDir = true;
                current = node.children;
            }
        }
    }

    function sortTree(nodes) {
        nodes.sort((a, b) => {
            if (a.isDir !== b.isDir) return a.isDir ? -1 : 1;
            return a.name.localeCompare(b.name);
        });
        for (const n of nodes) {
            if (n.children.length) sortTree(n.children);
        }
    }
    sortTree(root);
    return root;
}
