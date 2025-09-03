const apiBase = '/api/files';
const previewApi = '/api/preview';
const selected = new Set();
const previewableExt = new Set([
    '.txt', '.json', '.xml', '.csv', '.kml', '.geojson',
    '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg'
]);

function showSpinner() {
    document.getElementById('spinner').classList.add('show');
}

function hideSpinner() {
    document.getElementById('spinner').classList.remove('show');
}

async function apiFetch(url, options) {
    showSpinner();
    try {
        const res = await fetch(url, options);
        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || res.statusText);
        }
        return res;
    } catch (err) {
        alert('Error: ' + err.message);
        throw err;
    } finally {
        hideSpinner();
    }
}

function isPreviewable(name) {
    const dot = name.lastIndexOf('.');
    if (dot === -1) return false;
    return previewableExt.has(name.substring(dot).toLowerCase());
}

async function load() {
    const params = new URLSearchParams(window.location.search);
    const path = params.get('path') || '';
    try {
        const r = await apiFetch(apiBase + '?path=' + encodeURIComponent(path));
        const data = await r.json();
        document.getElementById('stats').textContent =
            `Folders: ${data.stats.directoryCount}, Files: ${data.stats.fileCount}, Size: ${data.stats.totalSize} bytes`;

        const list = document.getElementById('listing');
        list.innerHTML = '';
        selected.clear();

        if (path) {
            const parent = path.split('/').slice(0, -1).join('/');
            const li = document.createElement('li');
            li.innerHTML = `<a href="?path=${encodeURIComponent(parent)}">..</a>`;
            list.appendChild(li);
        }

        data.directories.forEach(d => {
            const li = document.createElement('li');
            const newPath = (path ? path + '/' : '') + d;
            const enc = encodeURIComponent(newPath);
            li.innerHTML = `<input type="checkbox" class="select" data-path="${enc}" /> ` +
                `<a href="?path=${encodeURIComponent(newPath)}">${d}/</a>` +
                ` <button onclick="deletePath('${enc}')">delete</button>` +
                ` <button onclick="movePath('${enc}')">move</button>` +
                ` <button onclick="copyPath('${enc}')">copy</button>`;
            list.appendChild(li);
        });

        data.files.forEach(f => {
            const li = document.createElement('li');
            const filePath = (path ? path + '/' : '') + f.name;
            const enc = encodeURIComponent(filePath);
            let html = `<input type="checkbox" class="select" data-path="${enc}" /> ` +
                `${f.name} (${f.size} bytes) <a href="${apiBase}/download?path=${encodeURIComponent(filePath)}">download</a>`;
            if (isPreviewable(f.name)) {
                html += ` <button onclick="preview('${enc}')">preview</button>`;
            }
            html += ` <button onclick="deletePath('${enc}')">delete</button>` +
                ` <button onclick="movePath('${enc}')">move</button>` +
                ` <button onclick="copyPath('${enc}')">copy</button>`;
            li.innerHTML = html;
            list.appendChild(li);
        });

        document.querySelectorAll('.select').forEach(cb => {
            cb.onchange = () => {
                const p = decodeURIComponent(cb.dataset.path);
                if (cb.checked) selected.add(p); else selected.delete(p);
            };
        });

        document.getElementById('uploadBtn').onclick = async () => {
            const fileInput = document.getElementById('uploadFile');
            const file = fileInput.files[0];
            if (!file) return;
            const form = new FormData();
            form.append('file', file);
            try {
                await apiFetch(apiBase + '/upload?path=' + encodeURIComponent(path), {
                    method: 'POST',
                    body: form
                });
                load();
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('searchBtn').onclick = async () => {
            const q = document.getElementById('search').value;
            try {
                const r = await apiFetch(apiBase + '/search?query=' + encodeURIComponent(q));
                const d = await r.json();
                showSearch(d);
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('createFolderBtn').onclick = async () => {
            const name = prompt('Folder name:');
            if (!name) return;
            const newPath = (path ? path + '/' : '') + name;
            try {
                await apiFetch(apiBase + '/mkdir?path=' + encodeURIComponent(newPath), { method: 'POST' });
                load();
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('zipBtn').onclick = async () => {
            if (selected.size === 0) return;
            try {
                const res = await apiFetch(apiBase + '/zip', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ paths: Array.from(selected) })
                });
                const total = parseInt(res.headers.get('Content-Length')) || 0;
                const reader = res.body.getReader();
                let received = 0;
                const chunks = [];
                const progress = document.getElementById('progress');
                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    chunks.push(value);
                    received += value.length;
                    if (total) progress.textContent = `Downloading... ${(received / total * 100).toFixed(1)}%`;
                }
                progress.textContent = '';
                const blob = new Blob(chunks, { type: 'application/zip' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'files.zip';
                a.click();
                URL.revokeObjectURL(url);
                selected.clear();
                document.querySelectorAll('.select').forEach(cb => cb.checked = false);
            } catch (err) {
                console.error(err);
            }
        };
    } catch (err) {
        console.error(err);
    }
}

function showSearch(data) {
    const list = document.getElementById('listing');
    list.innerHTML = '';
    data.directories.forEach(d => {
        const li = document.createElement('li');
        li.innerHTML = `<strong>Dir:</strong> <a href="?path=${encodeURIComponent(d.path)}">${d.path}</a>`;
        list.appendChild(li);
    });
    data.files.forEach(f => {
        const li = document.createElement('li');
        li.innerHTML = `<strong>File:</strong> ${f.path} (${f.size} bytes) <a href="${apiBase}/download?path=${encodeURIComponent(f.path)}">download</a>`;
        list.appendChild(li);
    });
    document.getElementById('stats').textContent = 'Search results';
}

async function deletePath(p) {
    const path = decodeURIComponent(p);
    if (!confirm('Delete ' + path + '?')) return;
    try {
        await apiFetch(apiBase + '?path=' + encodeURIComponent(path), { method: 'DELETE' });
        load();
    } catch (err) {
        console.error(err);
    }
}

async function promptAndPost(p, url, message) {
    const path = decodeURIComponent(p);
    const dest = prompt(message, path);
    if (!dest) return;
    try {
        await apiFetch(apiBase + url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ from: path, to: dest })
        });
        load();
    } catch (err) {
        console.error(err);
    }
}

function movePath(p) {
    promptAndPost(p, '/move', 'Move to:');
}

function copyPath(p) {
    promptAndPost(p, '/copy', 'Copy to:');
}

let map;

async function preview(p) {
    const path = decodeURIComponent(p);
    try {
        const r = await apiFetch(previewApi + '?path=' + encodeURIComponent(path));
        const dlg = document.getElementById('previewDialog');
        const container = document.getElementById('previewContainer');
        const ct = r.headers.get('Content-Type') || '';
        dlg.showModal();
        if (ct.includes('application/geo+json')) {
            const data = await r.json();
            container.innerHTML = '<div id="map" style="width:100%;height:100%;"></div>';
            if (map) {
                map.remove();
            }
            map = L.map('map');
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors'
            }).addTo(map);
            const layer = L.geoJSON(data).addTo(map);
            const bounds = layer.getBounds();
            if (bounds.isValid()) {
                map.fitBounds(bounds);
            }
        } else if (ct.startsWith('image/')) {
            const blob = await r.blob();
            const url = URL.createObjectURL(blob);
            container.innerHTML = `<img src="${url}" style="max-width:100%;max-height:100%;" />`;
        } else {
            const text = await r.text();
            container.innerHTML = `<pre style="white-space:pre-wrap;">${escapeHtml(text)}</pre>`;
        }
    } catch (err) {
        console.error(err);
    }
}

function escapeHtml(str) {
    return str.replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

document.getElementById('closePreview').onclick = () => {
    document.getElementById('previewDialog').close();
    const container = document.getElementById('previewContainer');
    container.innerHTML = '';
    if (map) {
        map.remove();
        map = null;
    }
};

window.onload = load;

