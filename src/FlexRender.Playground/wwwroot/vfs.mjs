// Virtual File System for FlexRender Playground.
// Purely in-memory Map<path, {data, type}>. Persistence is owned by projects.mjs.

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

// --- Public API ---

/** Add or overwrite a file (in-memory only). */
export function addFile(path, data, type) {
    path = normalizePath(path);
    if (!type) type = detectType(path);
    files.set(path, { data, type });
    notify('add', path);
}

/** Remove a file (in-memory only). */
export function removeFile(path) {
    path = normalizePath(path);
    if (!files.has(path)) return;
    files.delete(path);
    notify('remove', path);
}

/** Rename/move a file (in-memory only). */
export function renameFile(oldPath, newPath) {
    oldPath = normalizePath(oldPath);
    newPath = normalizePath(newPath);
    const entry = files.get(oldPath);
    if (!entry) return;
    files.delete(oldPath);
    files.set(newPath, entry);
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

/** Clear all files (in-memory only). */
export function clearAll() {
    files.clear();
    notify('clear', '');
}

/**
 * Load files from a project into the in-memory VFS.
 * Clears current files, loads given array, and notifies listeners.
 * Does NOT write to IndexedDB — projects.mjs handles persistence.
 * @param {{path: string, data: Uint8Array, type: string}[]} projectFiles
 */
export function loadFromProject(projectFiles) {
    files.clear();
    for (const f of projectFiles) {
        const path = normalizePath(f.path);
        files.set(path, { data: f.data, type: f.type || detectType(path) });
    }
    notify('clear', '');
    // Notify for each file so WASM resource loader can pick them up
    for (const [path] of files) {
        notify('add', path);
    }
}

/**
 * Export current VFS files as an array for saving into a project.
 * @returns {{path: string, data: Uint8Array, type: string}[]}
 */
export function exportFiles() {
    return [...files.entries()].map(([path, entry]) => ({
        path,
        data: entry.data,
        type: entry.type,
    }));
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
