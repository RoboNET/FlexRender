import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.FlexRender.Playground.PlaygroundApi;

await runMain();

// Hide loading, show app
document.getElementById('loading').style.display = 'none';
document.getElementById('app').style.display = 'block';

// Test render with a minimal template
const testYaml = `canvas:
  width: 300
  height: 100
  background: "#ffffff"
elements:
  - type: text
    content: "Hello from WASM!"
    size: 24
    color: "#333333"
    padding: "20"
`;

const statusEl = document.getElementById('status');
const errorEl = document.getElementById('error');
const imgEl = document.getElementById('preview-img');

try {
    statusEl.textContent = 'Rendering...';
    const start = performance.now();

    const pngBytes = api.RenderToPng(testYaml, null);
    const elapsed = (performance.now() - start).toFixed(0);

    if (pngBytes && pngBytes.length > 0) {
        const blob = new Blob([pngBytes], { type: 'image/png' });
        imgEl.src = URL.createObjectURL(blob);
        statusEl.textContent = `Rendered in ${elapsed}ms · ${pngBytes.length} bytes`;
    } else {
        errorEl.textContent = 'Render returned empty result — check browser console for errors';
        statusEl.textContent = 'Error';
        statusEl.style.background = '#c72e2e';
    }
} catch (e) {
    errorEl.textContent = e.message || String(e);
    statusEl.textContent = 'Error';
    statusEl.style.background = '#c72e2e';
}
