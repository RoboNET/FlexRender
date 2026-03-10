import { dotnet } from './_framework/dotnet.js';

// --- .NET WASM initialization ---
const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.FlexRender.Playground.PlaygroundApi;

await runMain();

// --- VFS, Splitter & Projects modules ---
import * as vfs from './vfs.mjs';
import * as projects from './projects.mjs';
import { initSplitters, initCollapsible } from './splitter.mjs';

// --- Monaco Editor via modern-monaco from CDN ---
const { init } = await import('https://esm.sh/modern-monaco');
const monaco = await init({
    langs: ['yaml', 'json'],
    themes: ['one-dark-pro'],
});

// --- Custom YAML autocomplete (schema-driven, no workers) ---
try {
    const { registerYamlAutocomplete } = await import('./yaml-autocomplete.mjs');
    const schemaResponse = await fetch('schemas/flexrender-template.json');
    const flexrenderSchema = await schemaResponse.json();
    registerYamlAutocomplete(monaco, flexrenderSchema, {
        getVfsFiles: () => vfs.listFiles().map(p => ({ path: p, type: vfs.detectType(p) })),
    });
    console.log('YAML autocomplete registered with FlexRender schema');
} catch (e) {
    console.warn('YAML autocomplete setup failed:', e.message);
}

// --- Built-in examples (used to seed example projects on first run) ---
const EXAMPLES = {
    'Simple Text': {
        yaml: `canvas:
  width: 400
  height: 150
  fixed: both
  background: "#ffffff"
fonts:
  - name: main
    path: Inter-Regular.ttf
layout:
  - type: text
    content: "Hello, FlexRender!"
    size: 28
    color: "#333333"
    padding: "30"`,
        json: '{}'
    },
    'Flex Layout': {
        yaml: `canvas:
  width: 400
  height: 200
  fixed: both
  background: "#f5f5f5"
fonts:
  - name: main
    path: Inter-Regular.ttf
layout:
  - type: flex
    direction: row
    gap: "10"
    padding: "20"
    children:
      - type: flex
        background: "#4CAF50"
        padding: "20"
        grow: 1
        children:
          - type: text
            content: "Left"
            color: "#ffffff"
            size: 16
      - type: flex
        background: "#2196F3"
        padding: "20"
        grow: 2
        children:
          - type: text
            content: "Right (grow: 2)"
            color: "#ffffff"
            size: 16`,
        json: '{}'
    },
    'Data Binding': {
        yaml: `canvas:
  width: 400
  height: 300
  fixed: both
  background: "#ffffff"
fonts:
  - name: main
    path: Inter-Regular.ttf
layout:
  - type: flex
    padding: "20"
    gap: "8"
    children:
      - type: text
        content: "{{title}}"
        size: 24
        color: "#333"
      - type: text
        content: "By {{author}}"
        size: 14
        color: "#888"
      - type: separator
        color: "#eee"
      - type: each
        array: items
        children:
          - type: text
            content: "\u2022 {{item}}"
            size: 14
            color: "#555"`,
        json: `{
  "title": "Shopping List",
  "author": "FlexRender",
  "items": ["Apples", "Bread", "Milk", "Cheese"]
}`
    },
    'Image Scaling': {
        yaml: `canvas:
  fixed: width
  width: 440
  background: "#ffffff"

layout:
  - type: flex
    direction: column
    gap: "20"
    padding: "20"
    children:
      - type: text
        content: "Image Fit Modes"
        size: 20
        color: "#333"
        fontWeight: bold

      - type: flex
        direction: row
        gap: "16"
        children:
          - type: flex
            direction: column
            gap: "4"
            align: center
            children:
              - type: text
                content: "contain"
                size: 11
                color: "#888"
              - type: flex
                width: "120"
                height: "120"
                background: "#f0f0f0"
                border: "1"
                borderColor: "#ddd"
                children:
                  - type: image
                    src: test-pattern.png
                    width: "120"
                    height: "120"
                    fit: contain

          - type: flex
            direction: column
            gap: "4"
            align: center
            children:
              - type: text
                content: "cover"
                size: 11
                color: "#888"
              - type: flex
                width: "120"
                height: "120"
                background: "#f0f0f0"
                border: "1"
                borderColor: "#ddd"
                children:
                  - type: image
                    src: test-pattern.png
                    width: "120"
                    height: "120"
                    fit: cover

          - type: flex
            direction: column
            gap: "4"
            align: center
            children:
              - type: text
                content: "fill"
                size: 11
                color: "#888"
              - type: flex
                width: "120"
                height: "120"
                background: "#f0f0f0"
                border: "1"
                borderColor: "#ddd"
                children:
                  - type: image
                    src: test-pattern.png
                    width: "120"
                    height: "120"
                    fit: fill`,
        json: '{}',
        assets: ['test-pattern.png'],
    },
    'Dynamic Receipt': {
        yaml: `canvas:
  fixed: width
  width: 320
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: "12"
    children:
      - type: flex
        gap: "4"
        align: center
        children:
          - type: text
            content: "{{shopName}}"
            fontWeight: bold
            size: 1.5em
            align: center
            color: "#1a1a1a"
          - type: text
            content: "{{address}}"
            size: 0.85em
            align: center
            color: "#888888"

      - type: separator
        style: dashed
        color: "#cccccc"

      - type: if
        condition: items
        hasItems: true
        then:
          - type: flex
            gap: "6"
            children:
              - type: each
                array: items
                as: item
                children:
                  - type: flex
                    direction: row
                    justify: space-between
                    children:
                      - type: flex
                        direction: column
                        gap: "2"
                        shrink: 1
                        children:
                          - type: text
                            content: "{{item.name}}"
                            size: 1em
                            color: "#333"
                          - type: if
                            condition: item.quantity
                            then:
                              - type: text
                                content: "x{{item.quantity}}"
                                size: 0.8em
                                color: "#888"
                      - type: text
                        content: "{{item.price}} $"
                        size: 1em
                        color: "#333"
                        align: right

      - type: separator
        color: "#1a1a1a"

      - type: if
        condition: discount
        then:
          - type: flex
            gap: "6"
            children:
              - type: flex
                direction: row
                justify: space-between
                children:
                  - type: text
                    content: "Subtotal"
                    size: 0.9em
                    color: "#666"
                  - type: text
                    content: "{{subtotal}} $"
                    size: 0.9em
                    color: "#666"
              - type: flex
                direction: row
                justify: space-between
                children:
                  - type: text
                    content: "Discount"
                    size: 0.9em
                    color: "#22c55e"
                  - type: text
                    content: "-{{discount}} $"
                    size: 0.9em
                    color: "#22c55e"
              - type: separator
                style: dashed
                color: "#ccc"

      - type: flex
        direction: row
        justify: space-between
        align: center
        children:
          - type: text
            content: "TOTAL"
            fontWeight: bold
            size: 1.2em
            color: "#1a1a1a"
          - type: text
            content: "{{total}} $"
            fontWeight: bold
            size: 1.2em
            color: "#1a1a1a"
          - type: if
            condition: totalNumber
            greaterThan: 10
            then:
              - type: image
                position: absolute
                top: "-8"
                left: "36"
                rotate: 30
                src: star-badge.png
                width: "24"
                height: "24"
                fit: contain

      - type: separator
        style: dotted
        color: "#ccc"

      - type: if
        condition: paymentStatus
        equals: "paid"
        then:
          - type: flex
            align: center
            gap: "4"
            children:
              - type: text
                content: "PAID"
                fontWeight: bold
                size: 1.1em
                color: "#22c55e"
                align: center
              - type: text
                content: "Thank you!"
                size: 0.85em
                color: "#666"
                align: center
        elseIf:
          condition: paymentStatus
          equals: "pending"
          then:
            - type: flex
              align: center
              gap: "6"
              children:
                - type: qr
                  data: "{{paymentUrl}}"
                  size: 120
                  errorCorrection: M
                - type: text
                  content: "Scan to pay"
                  size: 0.75em
                  color: "#999"
                  align: center
        else:
          - type: text
            content: "Payment required at counter"
            size: 0.9em
            color: "#ef4444"
            align: center

      - type: separator
        style: dotted
        color: "#ccc"

      - type: text
        content: "{{date}}"
        size: 0.75em
        align: center
        color: "#999"`,
        json: `{
  "shopName": "Coffee & Co",
  "address": "123 Main St, Downtown",
  "items": [
    {"name": "Cappuccino", "quantity": 2, "price": "4.50"},
    {"name": "Croissant", "price": "3.20"},
    {"name": "Fresh Juice", "quantity": 1, "price": "5.00"}
  ],
  "subtotal": "17.20",
  "discount": "2.00",
  "total": "15.20",
  "totalNumber": 15.20,
  "paymentStatus": "paid",
  "paymentUrl": "https://pay.example.com/inv/12345",
  "date": "2026-03-10 14:30"
}`,
        assets: ['star-badge.png'],
    },
    'NDC Receipt': {
        yaml: `# NDC (ATM receipt) format — binary terminal data rendered as a receipt
# The .ndc file in VFS contains raw ESC-sequence data from an ATM

fonts:
  - name: default
    path: JetBrainsMono-Regular.ttf
  - name: bold
    path: JetBrainsMono-Bold.ttf

canvas:
  fixed: width
  width: 576
  background: "#ffffff"

layout:
  - type: content
    source: bank-receipt.ndc
    format: ndc
    options:
      columns: 44
      font_family: JetBrains Mono
      charsets:
        I:
          font: bold
          font_style: bold
          encoding: qwerty-jcuken
          uppercase: true
        "2":
          font: default`,
        json: '{}',
        assets: ['bank-receipt.ndc', 'JetBrainsMono-Regular.ttf', 'JetBrainsMono-Bold.ttf'],
    },
};

// --- Create Monaco editors ---
const yamlModelUri = monaco.Uri.parse('file:///template.yaml');
const yamlModel = monaco.editor.createModel('', 'yaml', yamlModelUri);

const yamlEditor = monaco.editor.create(document.getElementById('yaml-editor'), {
    model: yamlModel,
    theme: 'one-dark-pro',
    minimap: { enabled: false },
    fontSize: 13,
    tabSize: 2,
    automaticLayout: true,
    scrollBeyondLastLine: true,
    fixedOverflowWidgets: true,
    quickSuggestions: {
        other: true,
        comments: false,
        strings: true,
    },
});

const jsonEditor = monaco.editor.create(document.getElementById('json-editor'), {
    value: '{}',
    language: 'json',
    theme: 'one-dark-pro',
    minimap: { enabled: false },
    fontSize: 13,
    tabSize: 2,
    automaticLayout: true,
    scrollBeyondLastLine: true,
    fixedOverflowWidgets: true,
});

// --- UI elements ---
const statusBar = document.getElementById('status-bar');
const statusText = document.getElementById('status-text');
const previewImg = document.getElementById('preview-img');
const errorsPane = document.getElementById('errors-pane');
const layoutPane = document.getElementById('layout-pane');

// --- Project UI elements ---
const projectSelect = document.getElementById('project-select');
const btnNewProject = document.getElementById('btn-new-project');
const btnDeleteProject = document.getElementById('btn-delete-project');
const btnResetExample = document.getElementById('btn-reset-example');

// --- Debug overlay toggle ---
const previewTabs = document.querySelector('.preview-tabs');
const zoomSelect = document.getElementById('zoom-select');

const overlayToggle = document.createElement('label');
overlayToggle.id = 'overlay-toggle';
overlayToggle.innerHTML = '<input type="checkbox" id="overlay-checkbox" /> Debug overlay';
previewTabs.insertBefore(overlayToggle, zoomSelect);

const overlayCheckbox = document.getElementById('overlay-checkbox');
let debugMode = false;

overlayCheckbox.addEventListener('change', () => {
    debugMode = overlayCheckbox.checked;
    scheduleRender();
});

// --- Bounds overlay toggle ---
const boundsToggle = document.createElement('label');
boundsToggle.id = 'bounds-toggle';
boundsToggle.innerHTML = '<input type="checkbox" id="bounds-checkbox" /> Bounds';
previewTabs.insertBefore(boundsToggle, zoomSelect);

const boundsCheckbox = document.getElementById('bounds-checkbox');
let boundsMode = false;

boundsCheckbox.addEventListener('change', () => {
    boundsMode = boundsCheckbox.checked;
    if (boundsMode && lastLayoutData) {
        showAllBounds(lastLayoutData);
    } else {
        hideHighlight();
    }
});

// --- Layout tree builder (with data attributes for highlight) ---
let canvasWidth = 0;
let canvasHeight = 0;
let lastLayoutData = null;

function buildLayoutTree(node, depth, parentAbsX, parentAbsY) {
    if (!node || !node.type) return '';
    parentAbsX = parentAbsX || 0;
    parentAbsY = parentAbsY || 0;
    const absX = parentAbsX + node.x;
    const absY = parentAbsY + node.y;
    const dims = `${node.w}\u00d7${node.h} @ (${node.x}, ${node.y})`;
    const hasChildren = node.children && node.children.length > 0;
    const openAttr = depth < 2 ? ' open' : '';
    const dataAttrs = `data-x="${absX}" data-y="${absY}" data-w="${node.w}" data-h="${node.h}"`;

    const props = [];
    if (node.content) props.push(`"${escHtml(node.content)}"`);
    if (node.font) props.push(`font=${escHtml(node.font)}`);
    if (node.fontFamily) props.push(`family=${escHtml(node.fontFamily)}`);
    if (node.size) props.push(`size=${escHtml(String(node.size))}`);
    if (node.color) props.push(`color=${escHtml(node.color)}`);
    if (node.fontWeight) props.push(`weight=${escHtml(String(node.fontWeight))}`);
    if (node.fontStyle) props.push(`style=${escHtml(node.fontStyle)}`);
    if (node.direction && node.type === 'Flex') props.push(escHtml(node.direction));
    if (node.align) props.push(`align=${escHtml(node.align)}`);
    if (node.justify) props.push(`justify=${escHtml(node.justify)}`);
    if (node.fontSize) props.push(`fontSize=${escHtml(String(node.fontSize))}px`);
    if (node.textLines) props.push(`lines=${escHtml(String(node.textLines))}`);

    const propsStr = props.length > 0 ? ` <span class="node-props">[${props.join(', ')}]</span>` : '';
    const safeType = escHtml(node.type);
    const safeDims = escHtml(dims);

    if (hasChildren) {
        const childrenHtml = node.children.map(c => buildLayoutTree(c, depth + 1, absX, absY)).join('');
        return `<details${openAttr}><summary ${dataAttrs}><span class="node-type" data-type="${safeType}">${safeType}</span> <span class="node-dims">${safeDims}</span>${propsStr}</summary>${childrenHtml}</details>`;
    }
    return `<div class="layout-leaf" ${dataAttrs}><span class="node-type" data-type="${safeType}">${safeType}</span> <span class="node-dims">${safeDims}</span>${propsStr}</div>`;
}

// --- File tree rendering ---
const filesTree = document.getElementById('files-tree');

function renderFileTree() {
    const tree = vfs.buildTree();
    filesTree.innerHTML = tree.length === 0 ? '' : renderTreeNodes(tree);
}

function renderTreeNodes(nodes) {
    return nodes.map(node => {
        if (node.isDir) {
            return `<div class="ft-dir open" data-path="${escHtml(node.path)}">
                <div class="ft-node" draggable="true" data-path="${escHtml(node.path)}" data-dir="true">
                    <span class="ft-arrow">&#x25B6;</span>
                    <span class="ft-icon">&#x1F4C1;</span>
                    <span class="ft-name">${escHtml(node.name)}</span>
                </div>
                <div class="ft-children">${renderTreeNodes(node.children)}</div>
            </div>`;
        }
        const icon = node.type === 'font' ? '&#x1F524;' : node.type === 'image' ? '&#x1F5BC;' : '&#x1F4C4;';
        const file = vfs.getFile(node.path);
        const size = file ? formatFileSize(file.data.length) : '';
        return `<div class="ft-file" data-path="${escHtml(node.path)}">
            <div class="ft-node" draggable="true" data-path="${escHtml(node.path)}">
                <span class="ft-icon">${icon}</span>
                <span class="ft-name">${escHtml(node.name)}</span>
                <span class="ft-size">${size}</span>
            </div>
        </div>`;
    }).join('');
}

function escHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatFileSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
}

// Toggle directory open/closed + click to copy path
filesTree.addEventListener('click', (e) => {
    const dir = e.target.closest('.ft-dir');
    const node = e.target.closest('.ft-node');
    if (dir && node && node.dataset.dir) {
        dir.classList.toggle('open');
        return;
    }
    if (node && !node.dataset.dir) {
        navigator.clipboard.writeText(node.dataset.path).then(() => {
            statusText.textContent = `Copied: ${node.dataset.path}`;
        });
    }
});

// Re-render tree when VFS changes
vfs.subscribe(() => renderFileTree());
renderFileTree();

// --- New folder button ---
document.getElementById('btn-add-folder').addEventListener('click', (e) => {
    e.stopPropagation();
    const name = prompt('Folder name:');
    if (!name || !name.trim()) return;
    vfs.addFile(name.trim() + '/.gitkeep', new Uint8Array(0), 'other');
});

// --- Context menu ---
let ctxMenu = null;

function showContextMenu(x, y, items) {
    hideContextMenu();
    ctxMenu = document.createElement('div');
    ctxMenu.className = 'ctx-menu';
    ctxMenu.style.left = x + 'px';
    ctxMenu.style.top = y + 'px';

    for (const item of items) {
        if (item === '---') {
            const sep = document.createElement('div');
            sep.className = 'ctx-menu-sep';
            ctxMenu.appendChild(sep);
            continue;
        }
        const el = document.createElement('div');
        el.className = 'ctx-menu-item';
        el.innerHTML = `<span>${item.label}</span>${item.shortcut ? `<span class="ctx-menu-shortcut">${item.shortcut}</span>` : ''}`;
        el.addEventListener('click', () => { hideContextMenu(); item.action(); });
        ctxMenu.appendChild(el);
    }

    document.body.appendChild(ctxMenu);
    const rect = ctxMenu.getBoundingClientRect();
    if (rect.right > window.innerWidth) ctxMenu.style.left = (window.innerWidth - rect.width - 4) + 'px';
    if (rect.bottom > window.innerHeight) ctxMenu.style.top = (window.innerHeight - rect.height - 4) + 'px';
}

function hideContextMenu() {
    if (ctxMenu) { ctxMenu.remove(); ctxMenu = null; }
}

document.addEventListener('click', hideContextMenu);
document.addEventListener('contextmenu', (e) => {
    if (!e.target.closest('.files-tree')) hideContextMenu();
});

filesTree.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    const node = e.target.closest('.ft-node');
    if (!node) return;

    const path = node.dataset.path;
    const isDir = !!node.dataset.dir;
    const items = [];

    items.push({ label: 'Copy path', action: () => navigator.clipboard.writeText(path) });
    items.push({
        label: 'Rename',
        action: () => startRename(node, path, isDir),
    });

    if (isDir) {
        items.push({
            label: 'New folder inside',
            action: () => {
                const name = prompt('Folder name:');
                if (name?.trim()) vfs.addFile(path + '/' + name.trim() + '/.gitkeep', new Uint8Array(0), 'other');
            },
        });
    } else {
        items.push({
            label: 'Duplicate',
            action: () => {
                const entry = vfs.getFile(path);
                if (!entry) return;
                const parts = path.split('/');
                const filename = parts.pop();
                const ext = filename.includes('.') ? '.' + filename.split('.').pop() : '';
                const base = ext ? filename.slice(0, -ext.length) : filename;
                const newName = base + '-copy' + ext;
                const newPath = [...parts, newName].join('/');
                vfs.addFile(newPath, entry.data, entry.type);
            },
        });
    }

    items.push('---');
    items.push({
        label: 'Delete',
        action: () => {
            if (isDir) {
                for (const f of vfs.listFiles()) {
                    if (f.startsWith(path + '/')) vfs.removeFile(f);
                }
            } else {
                vfs.removeFile(path);
            }
        },
    });

    showContextMenu(e.clientX, e.clientY, items);
});

function startRename(node, path, isDir) {
    const nameSpan = node.querySelector('.ft-name');
    const oldName = nameSpan.textContent;
    const input = document.createElement('input');
    input.className = 'ft-name-input';
    input.value = oldName;
    nameSpan.replaceWith(input);
    input.focus();
    input.select();

    function commit() {
        const newName = input.value.trim();
        input.replaceWith(nameSpan);
        if (!newName || newName === oldName) return;

        if (isDir) {
            const prefix = path + '/';
            const parts = path.split('/');
            parts[parts.length - 1] = newName;
            const newPrefix = parts.join('/') + '/';
            for (const f of vfs.listFiles()) {
                if (f.startsWith(prefix)) {
                    vfs.renameFile(f, newPrefix + f.slice(prefix.length));
                }
            }
        } else {
            const parts = path.split('/');
            parts[parts.length - 1] = newName;
            vfs.renameFile(path, parts.join('/'));
        }
    }

    input.addEventListener('blur', commit);
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); input.blur(); }
        if (e.key === 'Escape') { input.value = oldName; input.blur(); }
    });
}

// --- Internal drag & drop (move files between folders) ---
filesTree.addEventListener('dragstart', (e) => {
    const node = e.target.closest('.ft-node');
    if (!node) return;
    e.dataTransfer.setData('text/x-vfs-path', node.dataset.path);
    e.dataTransfer.effectAllowed = 'move';
});

filesTree.addEventListener('dragover', (e) => {
    if (!e.dataTransfer.types.includes('text/x-vfs-path')) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    filesTree.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
    const dir = e.target.closest('.ft-dir');
    if (dir) dir.querySelector(':scope > .ft-node')?.classList.add('drag-over');
});

filesTree.addEventListener('dragleave', (e) => {
    const node = e.target.closest('.ft-node');
    if (node) node.classList.remove('drag-over');
});

filesTree.addEventListener('drop', (e) => {
    filesTree.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
    const sourcePath = e.dataTransfer.getData('text/x-vfs-path');
    if (!sourcePath) return;
    e.preventDefault();

    const targetDir = e.target.closest('.ft-dir');
    const targetPath = targetDir ? targetDir.dataset.path : '';
    if (sourcePath === targetPath) return;

    const fileName = sourcePath.split('/').pop();
    const newPath = targetPath ? targetPath + '/' + fileName : fileName;
    if (sourcePath === newPath) return;

    const isDir = vfs.listFiles().some(f => f.startsWith(sourcePath + '/'));
    if (isDir) {
        const prefix = sourcePath + '/';
        const newPrefix = newPath + '/';
        for (const f of vfs.listFiles()) {
            if (f.startsWith(prefix)) vfs.renameFile(f, newPrefix + f.slice(prefix.length));
        }
    } else {
        vfs.renameFile(sourcePath, newPath);
    }
});

// --- Tab switching (preview / errors only) ---
function switchToTab(tabName) {
    document.querySelectorAll('.preview-tabs button').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
    document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');
    document.getElementById(`${tabName}-pane`).classList.add('active');
}

document.querySelectorAll('.preview-tabs button').forEach(btn => {
    btn.addEventListener('click', () => switchToTab(btn.dataset.tab));
});

// --- Layout inspector toggle ---
const layoutSection = document.getElementById('layout-section');
document.getElementById('layout-header').addEventListener('click', () => {
    layoutSection.classList.toggle('collapsed');
});

// --- Canvas highlight overlay for layout tree hover ---
const highlightCanvas = document.getElementById('highlight-canvas');
const highlightCtx = highlightCanvas.getContext('2d');
const previewImageWrap = document.getElementById('preview-image-wrap');

function syncCanvasSize() {
    const w = previewImg.offsetWidth;
    const h = previewImg.offsetHeight;
    if (w === 0) return;
    highlightCanvas.width = w * devicePixelRatio;
    highlightCanvas.height = h * devicePixelRatio;
    highlightCanvas.style.width = w + 'px';
    highlightCanvas.style.height = h + 'px';
    highlightCtx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
}

function showHighlight(x, y, w, h) {
    if (!previewImg.naturalWidth || canvasWidth === 0) return;
    syncCanvasSize();
    const imgW = previewImg.offsetWidth;
    const imgH = previewImg.offsetHeight;
    if (imgW === 0) return;

    const scaleX = imgW / canvasWidth;
    const scaleY = imgH / canvasHeight;
    const px = x * scaleX;
    const py = y * scaleY;
    const pw = w * scaleX;
    const ph = h * scaleY;

    highlightCtx.clearRect(0, 0, imgW, imgH);

    highlightCtx.fillStyle = 'rgba(255, 90, 50, 0.12)';
    highlightCtx.fillRect(px, py, pw, ph);

    highlightCtx.strokeStyle = 'rgba(255, 90, 50, 0.8)';
    highlightCtx.lineWidth = 2;
    highlightCtx.strokeRect(px, py, pw, ph);

    highlightCtx.font = '10px system-ui, sans-serif';
    highlightCtx.fillStyle = 'rgba(255, 90, 50, 0.9)';
    const label = `${Math.round(w)}\u00d7${Math.round(h)}`;
    const textY = py > 14 ? py - 3 : py + ph + 12;
    highlightCtx.fillText(label, px + 2, textY);
}

function hideHighlight() {
    const imgW = previewImg.offsetWidth;
    const imgH = previewImg.offsetHeight;
    highlightCtx.clearRect(0, 0, imgW, imgH);
    // Redraw bounds overlay if active
    if (boundsMode && lastLayoutData) {
        showAllBounds(lastLayoutData);
    }
}

function showAllBounds(layoutData) {
    if (!previewImg.naturalWidth || canvasWidth === 0) return;
    syncCanvasSize();
    const imgW = previewImg.offsetWidth;
    const imgH = previewImg.offsetHeight;
    if (imgW === 0) return;

    const scaleX = imgW / canvasWidth;
    const scaleY = imgH / canvasHeight;

    highlightCtx.clearRect(0, 0, imgW, imgH);

    function drawNode(node, parentX, parentY) {
        if (!node || !node.type) return;
        const absX = parentX + node.x;
        const absY = parentY + node.y;
        const px = absX * scaleX;
        const py = absY * scaleY;
        const pw = node.w * scaleX;
        const ph = node.h * scaleY;

        if (node.type === 'text') {
            highlightCtx.strokeStyle = 'rgba(33, 150, 243, 0.7)';
            highlightCtx.lineWidth = 1.5;
            highlightCtx.strokeRect(px, py, pw, ph);

            // Draw label with font name and content preview
            const fontLabel = node.fontFamily || node.font || '';
            const contentPreview = (node.content || '').substring(0, 10);
            const label = fontLabel ? `${fontLabel}: ${contentPreview}` : contentPreview;
            if (label) {
                highlightCtx.font = '9px system-ui, sans-serif';
                const textMetrics = highlightCtx.measureText(label);
                const labelX = px + 1;
                const labelY = py > 12 ? py - 2 : py + ph + 10;
                highlightCtx.fillStyle = 'rgba(33, 150, 243, 0.85)';
                highlightCtx.fillRect(labelX - 1, labelY - 9, textMetrics.width + 4, 11);
                highlightCtx.fillStyle = '#fff';
                highlightCtx.fillText(label, labelX, labelY);
            }
        } else if (node.type === 'flex' && node.children && node.children.length > 0) {
            highlightCtx.strokeStyle = 'rgba(76, 175, 80, 0.4)';
            highlightCtx.lineWidth = 0.5;
            highlightCtx.strokeRect(px, py, pw, ph);
        }

        if (node.children) {
            node.children.forEach(c => drawNode(c, absX, absY));
        }
    }

    drawNode(layoutData, 0, 0);
}

layoutPane.addEventListener('mouseover', (e) => {
    const target = e.target.closest('summary[data-x], .layout-leaf[data-x]');
    if (!target) return;
    showHighlight(
        parseFloat(target.dataset.x),
        parseFloat(target.dataset.y),
        parseFloat(target.dataset.w),
        parseFloat(target.dataset.h)
    );
});

layoutPane.addEventListener('mouseout', (e) => {
    const target = e.target.closest('summary[data-x], .layout-leaf[data-x]');
    if (target) hideHighlight();
});

// --- Preview zoom & scroll ---
let zoomLevel = 1;
const previewContent = document.querySelector('.preview-content');

function applyZoom(level) {
    zoomLevel = level;
    if (level === 1) {
        previewImageWrap.style.transform = '';
    } else {
        previewImageWrap.style.transform = `scale(${level})`;
        previewImageWrap.style.transformOrigin = 'top left';
    }
    const nearest = [...zoomSelect.options].find(o => o.value !== 'fit' && Math.abs(parseFloat(o.value) - level) < 0.05);
    zoomSelect.value = nearest ? nearest.value : '';
}

zoomSelect.addEventListener('change', () => {
    const val = zoomSelect.value;
    if (val === 'fit') {
        applyZoom(1);
        previewImageWrap.style.transform = '';
        zoomSelect.value = 'fit';
        return;
    }
    applyZoom(parseFloat(val));
});

previewContent.addEventListener('wheel', (e) => {
    if (!e.ctrlKey && !e.metaKey) return;
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.1 : 0.1;
    applyZoom(Math.max(0.25, Math.min(5, zoomLevel + delta)));
}, { passive: false });

previewContent.addEventListener('dblclick', () => {
    applyZoom(1);
});

// --- Render function ---
let renderTimeout = null;
let lastObjectUrl = null;

function render() {
    const yaml = yamlEditor.getValue();
    const json = jsonEditor.getValue();

    statusBar.classList.remove('error');
    statusText.textContent = 'Rendering...';

    try {
        const errorsJson = api.Validate(yaml);
        const errors = JSON.parse(errorsJson);

        if (errors.length > 0) {
            errorsPane.textContent = errors.map(e =>
                e.line > 0 ? `Line ${e.line}: ${e.message}` : e.message
            ).join('\n');
            statusBar.classList.add('error');
            statusText.textContent = `${errors.length} error(s)`;
            switchToTab('errors');
            return;
        }

        errorsPane.textContent = '';

        const start = performance.now();
        const dataArg = json.trim() === '{}' || json.trim() === '' ? null : json;
        const pngBytes = debugMode
            ? api.RenderDebugPng(yaml, dataArg)
            : api.RenderToPng(yaml, dataArg);
        const elapsed = (performance.now() - start).toFixed(0);

        if (pngBytes && pngBytes.length > 0) {
            if (lastObjectUrl) URL.revokeObjectURL(lastObjectUrl);
            const blob = new Blob([pngBytes], { type: 'image/png' });
            lastObjectUrl = URL.createObjectURL(blob);
            previewImg.src = lastObjectUrl;
            const modeLabel = debugMode ? ' [debug]' : '';
            statusText.textContent = `Rendered in ${elapsed}ms \u00b7 ${(pngBytes.length / 1024).toFixed(1)} KB${modeLabel}`;

            try {
                const layoutJson = api.GetLayout(yaml, dataArg);
                const layoutData = JSON.parse(layoutJson);
                canvasWidth = layoutData.w || 0;
                canvasHeight = layoutData.h || 0;
                lastLayoutData = layoutData;

                // Font diagnostics (logged to console for debugging)
                try {
                    const diagJson = api.GetFontDiagnostics();
                    const diag = JSON.parse(diagJson);
                    if (diag.fonts?.length > 0) {
                        console.group('Font diagnostics');
                        for (const f of diag.fonts) {
                            const status = f.isDefault ? '⚠️ DEFAULT FALLBACK' : `✅ ${f.familyName}`;
                            console.log(`${f.name}: ${status} (fixed=${f.isFixedPitch}, weight=${f.fontWeight})`);
                            console.log(`  boldVariant: ${f.boldVariant}`);
                            console.log(`  normalVariant: ${f.normalVariant}`);
                            console.log(`  ndcBoldResolve: ${f.ndcBoldResolve}`);
                            console.log(`  ndcNormalResolve: ${f.ndcNormalResolve}`);
                        }
                        console.log(`Memory resources (${diag.memoryResourceCount}):`, diag.memoryResources);
                        console.groupEnd();
                    }
                } catch (diagErr) { console.warn('Font diagnostics failed:', diagErr); }

                // Layout debug: log first few text elements with metrics
                console.group('Layout metrics (first text elements)');
                function logTexts(node, depth, parentX, parentY) {
                    if (!node) return;
                    const ax = (parentX || 0) + node.x, ay = (parentY || 0) + node.y;
                    if (node.type === 'text') {
                        console.log(`[${ax.toFixed(1)},${ay.toFixed(1)}] ${node.w.toFixed(1)}×${node.h.toFixed(1)} fontSize=${node.fontSize || '?'} fontSizeExact=${node.fontSizeExact || '?'} font=${node.font || '?'} resolved=${node.resolvedTypeface || '?'} "${(node.content || '').substring(0, 30)}"`);
                    }
                    (node.children || []).forEach(c => logTexts(c, depth + 1, ax, ay));
                }
                logTexts(layoutData, 0, 0, 0);
                console.groupEnd();

                layoutPane.innerHTML = '<div class="layout-tree">' + buildLayoutTree(layoutData, 0) + '</div>';
                // Compare rendered PNG size vs layout size
                previewImg.addEventListener('load', () => {
                    console.log(`PNG: ${previewImg.naturalWidth}×${previewImg.naturalHeight}, Layout: ${canvasWidth}×${canvasHeight}, Ratio: ${(previewImg.naturalWidth / canvasWidth).toFixed(4)}×${(previewImg.naturalHeight / canvasHeight).toFixed(4)}`);
                    if (boundsMode) showAllBounds(layoutData);
                }, { once: true });

            } catch (layoutErr) {
                console.warn('Layout computation failed:', layoutErr);
                layoutPane.innerHTML = '<div class="layout-error">Layout unavailable</div>';
                lastLayoutData = null;
            }

            switchToTab('preview');
        } else {
            statusText.textContent = 'Render returned empty \u2014 check console';
        }
    } catch (e) {
        errorsPane.textContent = e.message || String(e);
        statusBar.classList.add('error');
        statusText.textContent = 'Error';
        switchToTab('errors');
    }
}

// --- Debounced render ---
function scheduleRender() {
    clearTimeout(renderTimeout);
    renderTimeout = setTimeout(render, 300);
}

yamlEditor.onDidChangeModelContent(scheduleRender);
jsonEditor.onDidChangeModelContent(scheduleRender);

// --- Project management ---

/** Currently loaded project (full object). */
let currentProject = null;
let autoSaveTimeout = null;
let isSwitching = false; // Guard to prevent auto-save during project switch
let switchGeneration = 0; // Guard against rapid project switching race conditions

/** Convert an example name to a stable slug ID. */
function exampleSlug(name) {
    return 'example-' + name.toLowerCase().replace(/[^a-z0-9]+/g, '-');
}

/** Populate the project selector dropdown. */
async function refreshProjectSelect() {
    const list = await projects.listProjects();
    projectSelect.innerHTML = '';

    const examplesGroup = document.createElement('optgroup');
    examplesGroup.label = 'Examples';
    const userGroup = document.createElement('optgroup');
    userGroup.label = 'My Projects';

    let hasExamples = false;
    let hasUser = false;

    for (const p of list) {
        const option = document.createElement('option');
        option.value = p.id;
        option.textContent = p.name;
        if (p.isExample) {
            examplesGroup.appendChild(option);
            hasExamples = true;
        } else {
            userGroup.appendChild(option);
            hasUser = true;
        }
    }

    if (hasExamples) projectSelect.appendChild(examplesGroup);
    if (hasUser) projectSelect.appendChild(userGroup);

    if (currentProject) {
        projectSelect.value = currentProject.id;
    }

    // Update delete/reset button visibility
    updateProjectButtons();
}

/** Show/hide delete and reset buttons based on current project type. */
function updateProjectButtons() {
    if (!currentProject) return;
    btnDeleteProject.style.display = currentProject.isExample ? 'none' : '';
    btnResetExample.style.display = currentProject.isExample ? '' : 'none';
}

/** Save the current project state (editors + VFS) to IndexedDB. */
async function saveCurrentProject() {
    if (!currentProject || isSwitching) return;
    currentProject.yaml = yamlEditor.getValue();
    currentProject.json = jsonEditor.getValue();
    currentProject.files = vfs.exportFiles();
    await projects.saveProject(currentProject);
}

/** Debounced auto-save (500ms). */
function scheduleAutoSave() {
    if (isSwitching) return;
    clearTimeout(autoSaveTimeout);
    autoSaveTimeout = setTimeout(() => saveCurrentProject(), 500);
}

/** Switch to a different project by ID. Saves current first, then loads new. */
async function switchProject(id) {
    const myGeneration = ++switchGeneration;
    isSwitching = true;
    clearTimeout(autoSaveTimeout);

    // Save current project before switching
    if (currentProject) {
        currentProject.yaml = yamlEditor.getValue();
        currentProject.json = jsonEditor.getValue();
        currentProject.files = vfs.exportFiles();
        await projects.saveProject(currentProject);
    }
    if (myGeneration !== switchGeneration) return;

    // Load new project
    const project = await projects.loadProject(id);
    if (myGeneration !== switchGeneration) return;
    if (!project) {
        isSwitching = false;
        console.warn('Project not found:', id);
        return;
    }

    currentProject = project;
    projects.setCurrentProjectId(id);

    // Clear WASM resources before loading new VFS
    for (const path of vfs.listFiles()) {
        api.RemoveResource(path);
    }

    // Set editor values (this will trigger onDidChangeModelContent, but auto-save is guarded)
    yamlEditor.setValue(project.yaml);
    jsonEditor.setValue(project.json);

    // Load VFS files — this triggers 'clear' then 'add' for each file,
    // which syncs to WASM via the VFS subscriber
    vfs.loadFromProject(project.files);

    // Update UI
    projectSelect.value = id;
    updateProjectButtons();

    isSwitching = false;
    scheduleRender();
}

// Sync VFS changes to WASM MemoryResourceLoader AND trigger auto-save
vfs.subscribe((event, path) => {
    if (event === 'add') {
        const entry = vfs.getFile(path);
        if (entry && entry.data.length > 0) api.LoadResource(path, entry.data);
    } else if (event === 'remove') {
        api.RemoveResource(path);
    }
    scheduleRender();
    scheduleAutoSave();
});

// Auto-save on editor changes
yamlEditor.onDidChangeModelContent(scheduleAutoSave);
jsonEditor.onDidChangeModelContent(scheduleAutoSave);

// --- Project selector change ---
projectSelect.addEventListener('change', () => {
    const id = projectSelect.value;
    if (id && (!currentProject || id !== currentProject.id)) {
        switchProject(id);
    }
});

// --- New project button ---
btnNewProject.addEventListener('click', async () => {
    const name = prompt('Project name:');
    if (!name || !name.trim()) return;
    const project = await projects.createProject(name.trim());
    await refreshProjectSelect();
    await switchProject(project.id);
    statusText.textContent = `Created project: ${project.name}`;
});

// --- Delete project button ---
btnDeleteProject.addEventListener('click', async () => {
    if (!currentProject || currentProject.isExample) return;
    if (!confirm(`Delete project "${currentProject.name}"?`)) return;

    const deletedId = currentProject.id;
    await projects.deleteProject(deletedId);

    // Switch to first available project
    const list = await projects.listProjects();
    const nextId = list.length > 0 ? list[0].id : null;

    if (nextId) {
        currentProject = null; // Clear so switchProject doesn't try to save deleted project
        await refreshProjectSelect();
        await switchProject(nextId);
    }

    statusText.textContent = 'Project deleted';
});

// --- Reset example button ---
btnResetExample.addEventListener('click', async () => {
    if (!currentProject || !currentProject.isExample) return;
    if (!confirm(`Reset "${currentProject.name}" to its default state?`)) return;

    // Find the original example data
    const originalName = Object.keys(EXAMPLES).find(name => exampleSlug(name) === currentProject.id);
    if (!originalName) return;

    const example = EXAMPLES[originalName];
    const files = await loadExampleAssets(example.assets);
    const resetId = currentProject.id;
    await projects.seedExample(resetId, originalName, example.yaml, example.json, files);
    currentProject = null; // Prevent switchProject from overwriting the fresh seed
    await switchProject(resetId);
    statusText.textContent = `Reset example: ${originalName}`;
});

/** Fetch example asset files from example-assets/ directory. */
async function loadExampleAssets(assetNames) {
    if (!assetNames || assetNames.length === 0) return [];
    const files = [];
    for (const name of assetNames) {
        try {
            const resp = await fetch(`example-assets/${name}`);
            if (!resp.ok) continue;
            const data = new Uint8Array(await resp.arrayBuffer());
            files.push({ path: name, data, type: vfs.detectType(name) });
        } catch (e) {
            console.warn(`Failed to load example asset: ${name}`, e);
        }
    }
    return files;
}

// --- Initialize projects on startup ---
async function initProjects() {
    await projects.init();
    const list = await projects.listProjects();

    // Seed any missing examples (supports adding new examples without clearing DB)
    const existingIds = new Set(list.map(p => p.id));
    for (const [name, example] of Object.entries(EXAMPLES)) {
        const id = exampleSlug(name);
        if (!existingIds.has(id)) {
            const files = await loadExampleAssets(example.assets);
            await projects.seedExample(id, name, example.yaml, example.json, files);
        }
    }

    await refreshProjectSelect();

    // Determine which project to load
    let projectId = projects.getCurrentProjectId();

    // Validate that the stored project still exists
    if (projectId) {
        const exists = await projects.projectExists(projectId);
        if (!exists) projectId = null;
    }

    // Default to first example
    if (!projectId) {
        const firstExampleName = Object.keys(EXAMPLES)[0];
        projectId = exampleSlug(firstExampleName);
    }

    await switchProject(projectId);
}

// --- JSZip lazy loader (cached after first use) ---
let _jszip = null;
async function loadJSZip() {
    if (!_jszip) {
        _jszip = (await import('https://esm.sh/jszip')).default;
    }
    return _jszip;
}

// --- Export ZIP ---
document.getElementById('btn-export-zip').addEventListener('click', async () => {
    if (!currentProject) return;
    try {
        statusText.textContent = 'Exporting ZIP...';
        const JSZip = await loadJSZip();
        const zip = new JSZip();

        // Add template.yaml
        zip.file('template.yaml', yamlEditor.getValue());

        // Add data.json (only if non-empty)
        const jsonVal = jsonEditor.getValue().trim();
        if (jsonVal && jsonVal !== '{}') {
            zip.file('data.json', jsonVal);
        }

        // Add VFS files under files/ prefix
        for (const entry of vfs.allEntries()) {
            zip.file('files/' + entry.path, entry.data);
        }

        const blob = await zip.generateAsync({ type: 'blob' });
        const safeName = currentProject.name.replace(/[^a-zA-Z0-9_\-. ]/g, '_');
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = safeName + '.zip';
        a.click();
        setTimeout(() => URL.revokeObjectURL(a.href), 60000);
        statusText.textContent = `Exported: ${safeName}.zip`;
    } catch (e) {
        statusText.textContent = 'ZIP export failed';
        console.error('ZIP export error:', e);
        alert('ZIP export failed: ' + (e.message || e));
    }
});

// --- Import ZIP ---
async function importZipFile(file) {
    try {
        statusText.textContent = 'Importing ZIP...';
        const JSZip = await loadJSZip();
        const zip = await JSZip.loadAsync(await file.arrayBuffer());

        // Extract template.yaml (required)
        let yamlContent = null;
        let jsonContent = '{}';
        const vfsFiles = [];

        for (const [relativePath, zipEntry] of Object.entries(zip.files)) {
            if (zipEntry.dir) continue;

            // Normalize: strip leading slashes
            const name = relativePath.replace(/^\/+/, '');

            if (name === 'template.yaml') {
                yamlContent = await zipEntry.async('string');
            } else if (name === 'data.json') {
                jsonContent = await zipEntry.async('string');
            } else if (name.startsWith('files/')) {
                const vfsPath = name.slice('files/'.length);
                if (vfsPath && !vfsPath.startsWith('/') && !vfsPath.includes('..')) {
                    const data = new Uint8Array(await zipEntry.async('arraybuffer'));
                    vfsFiles.push({ path: vfsPath, data, type: vfs.detectType(vfsPath) });
                }
            } else {
                // Files outside files/ directory (other than template.yaml/data.json) go into VFS
                if (!name.startsWith('/') && !name.includes('..')) {
                    const data = new Uint8Array(await zipEntry.async('arraybuffer'));
                    vfsFiles.push({ path: name, data, type: vfs.detectType(name) });
                }
            }
        }

        if (!yamlContent) {
            alert('Invalid ZIP: template.yaml not found.');
            statusText.textContent = 'Import failed: no template.yaml';
            return;
        }

        // Derive project name from ZIP filename
        let projectName = file.name.replace(/\.zip$/i, '').trim();
        if (!projectName) projectName = 'Imported Project';

        // Create a new project with the extracted data
        const project = await projects.createProject(projectName);
        project.yaml = yamlContent;
        project.json = jsonContent;
        project.files = vfsFiles;
        await projects.saveProject(project);

        await refreshProjectSelect();
        await switchProject(project.id);
        statusText.textContent = `Imported project: ${projectName} (${vfsFiles.length} file(s))`;
    } catch (e) {
        statusText.textContent = 'ZIP import failed';
        console.error('ZIP import error:', e);
        alert('ZIP import failed: ' + (e.message || e));
    }
}

document.getElementById('btn-import-zip').addEventListener('click', () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.zip';
    input.addEventListener('change', async () => {
        if (input.files.length > 0) {
            await importZipFile(input.files[0]);
        }
    });
    input.click();
});

// --- Export PNG ---
document.getElementById('btn-export-png').addEventListener('click', () => {
    const yaml = yamlEditor.getValue();
    const json = jsonEditor.getValue();
    const dataArg = json.trim() === '{}' || json.trim() === '' ? null : json;

    try {
        const pngBytes = api.RenderToPng(yaml, dataArg);
        if (pngBytes && pngBytes.length > 0) {
            const blob = new Blob([pngBytes], { type: 'image/png' });
            const a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = 'flexrender-output.png';
            a.click();
            setTimeout(() => URL.revokeObjectURL(a.href), 60000);
        }
    } catch (e) {
        alert('Export failed: ' + (e.message || e));
    }
});

// --- Drag & drop from OS ---
const dropOverlay = document.getElementById('drop-overlay');
let dragCounter = 0;

document.addEventListener('dragenter', (e) => {
    e.preventDefault();
    if (e.dataTransfer.types.includes('text/x-vfs-path')) return;
    dragCounter++;
    dropOverlay.classList.add('visible');
});

document.addEventListener('dragleave', (e) => {
    e.preventDefault();
    dragCounter--;
    if (dragCounter === 0) dropOverlay.classList.remove('visible');
});

document.addEventListener('dragover', (e) => e.preventDefault());

document.addEventListener('drop', async (e) => {
    if (e.dataTransfer.getData('text/x-vfs-path')) return;
    e.preventDefault();
    dragCounter = 0;
    dropOverlay.classList.remove('visible');

    // Check for .zip files first — import them as projects
    const droppedFiles = e.dataTransfer.files;
    const zipFiles = [];

    for (let i = 0; i < droppedFiles.length; i++) {
        const file = droppedFiles[i];
        if (file.name.toLowerCase().endsWith('.zip')) {
            zipFiles.push(file);
        }
    }

    if (zipFiles.length > 0) {
        for (const zipFile of zipFiles) {
            await importZipFile(zipFile);
        }
        // If all dropped files are zips, stop here
        if (zipFiles.length === droppedFiles.length) return;
    }

    // Process non-zip files as VFS entries
    const items = e.dataTransfer.items;
    const fileEntries = [];

    if (items) {
        for (const item of items) {
            const entry = item.webkitGetAsEntry?.();
            if (entry) {
                await collectEntries(entry, '', fileEntries);
            }
        }
    }

    if (fileEntries.length === 0) {
        for (const file of droppedFiles) {
            if (file.name.toLowerCase().endsWith('.zip')) continue; // Already handled
            const buffer = new Uint8Array(await file.arrayBuffer());
            fileEntries.push({ path: file.name, data: buffer });
        }
    } else {
        // Filter out zip files that were already imported
        const filtered = fileEntries.filter(f => !f.path.toLowerCase().endsWith('.zip'));
        fileEntries.length = 0;
        fileEntries.push(...filtered);
    }

    for (const { path, data } of fileEntries) {
        const type = vfs.detectType(path);
        vfs.addFile(path, data, type);
    }

    if (fileEntries.length > 0) {
        statusText.textContent = `Added ${fileEntries.length} file(s) to VFS`;
        scheduleRender();
    }
});

async function collectEntries(entry, prefix, results) {
    if (entry.isFile) {
        const file = await new Promise((resolve) => entry.file(resolve));
        const data = new Uint8Array(await file.arrayBuffer());
        results.push({ path: prefix + entry.name, data });
    } else if (entry.isDirectory) {
        const reader = entry.createReader();
        const allEntries = [];
        let batch;
        do {
            batch = await new Promise((resolve) => reader.readEntries(resolve));
            allEntries.push(...batch);
        } while (batch.length > 0);
        for (const child of allEntries) {
            await collectEntries(child, prefix + entry.name + '/', results);
        }
    }
}

// --- Init resizable panels ---
const editorPanel = document.getElementById('editor-panel');
const yamlSection = document.getElementById('yaml-section');
const jsonSection = document.getElementById('json-section');
const filesSection = document.getElementById('files-section');
const previewPanel = document.querySelector('.preview-panel');

initSplitters({
    'yaml-json': { before: yamlSection, after: jsonSection, container: editorPanel, direction: 'h' },
    'json-files': { before: jsonSection, after: filesSection, container: editorPanel, direction: 'h' },
    'editor-preview': { before: editorPanel, after: previewPanel, container: document.querySelector('.main-content'), direction: 'v' },
    'preview-layout': { before: previewContent, after: layoutSection, container: previewPanel, direction: 'h' },
});

initCollapsible();

// --- Show app & initialize projects ---
document.getElementById('loading').style.display = 'none';
document.getElementById('app').style.display = 'flex';
await initProjects();
