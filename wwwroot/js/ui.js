const previewableExt = new Set([
    '.txt', '.json', '.xml', '.csv', '.kml', '.geojson',
    '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg'
]);

export function renderDirectoryList(list, directories, path, navigate) {
    directories.forEach(d => {
        const li = document.createElement('li');
        const newPath = (path ? path + '/' : '') + d;
        const enc = encodeURIComponent(newPath);
        li.innerHTML = `<input type="checkbox" class="select" data-path="${enc}" /> ` +
            `<button class="navigate" data-path="${enc}">${d}/</button>` +
            ` <button data-action="deletePath" data-path="${enc}">delete</button>` +
            ` <button data-action="movePath" data-path="${enc}">move</button>` +
            ` <button data-action="copyPath" data-path="${enc}">copy</button>`;
        const nav = li.querySelector('.navigate');
        nav.onclick = () => navigate(newPath);
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

export function renderSearchResults(list, data, navigate) {
    list.innerHTML = '';
    data.directories.forEach(d => {
        const li = document.createElement('li');
        const enc = encodeURIComponent(d.path);
        li.innerHTML = `<strong>Dir:</strong> <button class="navigate" data-path="${enc}">${d.path}</button>`;
        li.querySelector('.navigate').onclick = () => navigate(d.path);
        list.appendChild(li);
    });
    data.files.forEach(f => {
        const li = document.createElement('li');
        li.innerHTML = `<strong>File:</strong> ${f.path} (${f.size} bytes) <a href="/api/files/download?path=${encodeURIComponent(f.path)}">download</a>`;
        list.appendChild(li);
    });
}
