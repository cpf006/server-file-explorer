const previewableExt = new Set([
    '.txt', '.json', '.xml', '.csv', '.kml', '.geojson',
    '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg'
]);

export function renderDirectoryList(list, directories, path) {
    directories.forEach(d => {
        const li = document.createElement('li');
        const newPath = (path ? path + '/' : '') + d;
        const enc = encodeURIComponent(newPath);
        li.innerHTML = `<input type="checkbox" class="select" data-path="${enc}" /> ` +
            `<a href="?path=${encodeURIComponent(newPath)}">${d}/</a>` +
            ` <button data-action="deletePath" data-path="${enc}">delete</button>` +
            ` <button data-action="movePath" data-path="${enc}">move</button>` +
            ` <button data-action="copyPath" data-path="${enc}">copy</button>`;
        list.appendChild(li);
    });
}

function isPreviewable(name) {
    const dot = name.lastIndexOf('.');
    if (dot === -1) return false;
    return previewableExt.has(name.substring(dot).toLowerCase());
}

export function renderFileList(list, files, path) {
    files.forEach(f => {
        const li = document.createElement('li');
        const filePath = (path ? path + '/' : '') + f.name;
        const enc = encodeURIComponent(filePath);
        let html = `<input type="checkbox" class="select" data-path="${enc}" /> ` +
            `${f.name} (${f.size} bytes) <a href="/api/files/download?path=${encodeURIComponent(filePath)}">download</a>`;
        if (isPreviewable(f.name)) {
            html += ` <button data-action="preview" data-path="${enc}">preview</button>`;
        }
        html += ` <button data-action="deletePath" data-path="${enc}">delete</button>` +
            ` <button data-action="movePath" data-path="${enc}">move</button>` +
            ` <button data-action="copyPath" data-path="${enc}">copy</button>`;
        li.innerHTML = html;
        list.appendChild(li);
    });
}

export function renderSearchResults(list, data) {
    list.innerHTML = '';
    data.directories.forEach(d => {
        const li = document.createElement('li');
        li.innerHTML = `<strong>Dir:</strong> <a href="?path=${encodeURIComponent(d.path)}">${d.path}</a>`;
        list.appendChild(li);
    });
    data.files.forEach(f => {
        const li = document.createElement('li');
        li.innerHTML = `<strong>File:</strong> ${f.path} (${f.size} bytes) <a href="/api/files/download?path=${encodeURIComponent(f.path)}">download</a>`;
        list.appendChild(li);
    });
}
