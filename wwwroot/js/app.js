import * as api from './api.js';
import { renderDirectoryList, renderFileList, renderSearchResults } from './ui.js';
import { showPreview, closePreview } from './preview.js';
import { downloadZip } from './zip.js';

class FileExplorer {
    constructor() {
        this.selected = new Set();
        this.currentPath = '';
    }

    async fetchDirectory(pathInput) {
        const path = typeof pathInput === 'string'
            ? pathInput
            : (new URLSearchParams(window.location.search).get('path') || '');
        this.currentPath = path;
        const listElement = document.getElementById('listing');
        listElement.innerHTML = '<li class="skeleton"></li>';
        document.getElementById('stats').textContent = '';
        document.getElementById('backBtn').style.display = 'none';
        try {
            const data = await api.listDirectory(path);
            document.getElementById('stats').textContent =
                `Folders: ${data.stats.directoryCount}, Files: ${data.stats.fileCount}, Size: ${data.stats.totalSize} bytes`;
            this.renderListing(data, path);
            this.bindListingEvents();
        } catch (err) {
            console.error(err);
        }
    }

    renderListing(data, path) {
        const listElement = document.getElementById('listing');
        listElement.innerHTML = '';
        this.selected.clear();
        if (path) {
            const parent = path.split('/').slice(0, -1).join('/');
            const listItem = document.createElement('li');
            const button = document.createElement('button');
            button.textContent = '..';
            button.onclick = () => this.navigateTo(parent);
            listItem.appendChild(button);
            listElement.appendChild(listItem);
        }
        renderDirectoryList(listElement, data.directories, path, childPath => this.navigateTo(childPath));
        renderFileList(listElement, data.files, path);
    }

    bindListingEvents() {
        document.querySelectorAll('.select').forEach(checkbox => {
            checkbox.onchange = () => {
                const path = decodeURIComponent(checkbox.dataset.path);
                if (checkbox.checked) {
                    this.selected.add(path);
                } else {
                    this.selected.delete(path);
                }
            };
        });

        const actions = {
            deletePath: path => this.deletePath(path),
            movePath: path => this.movePath(path),
            copyPath: path => this.copyPath(path),
            preview: path => showPreview(decodeURIComponent(path))
        };
        document.querySelectorAll('button[data-action]').forEach(button => {
            const action = actions[button.dataset.action];
            if (action) {
                button.onclick = () => action(button.dataset.path);
            }
        });
    }

    bindGlobalEvents() {
        document.getElementById('uploadBtn').onclick = async () => {
            const fileInput = document.getElementById('uploadFile');
            const file = fileInput.files[0];
            if (!file) return;
            try {
                await api.uploadFile(this.currentPath, file);
                this.fetchDirectory(this.currentPath);
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('searchBtn').onclick = async () => {
            const query = document.getElementById('search').value;
            try {
                const results = await api.search(query);
                this.showSearch(results);
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('createFolderBtn').onclick = async () => {
            const name = prompt('Folder name:');
            if (!name) return;
            const newPath = (this.currentPath ? this.currentPath + '/' : '') + name;
            try {
                await api.createFolder(newPath);
                this.fetchDirectory(this.currentPath);
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('zipBtn').onclick = async () => {
            if (this.selected.size === 0) return;
            try {
                await downloadZip(Array.from(this.selected), document.getElementById('progress'));
                this.selected.clear();
                document.querySelectorAll('.select').forEach(checkbox => (checkbox.checked = false));
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('closePreview').onclick = closePreview;
    }

    showSearch(data) {
        const listElement = document.getElementById('listing');
        renderSearchResults(listElement, data, path => this.navigateTo(path));
        document.getElementById('stats').textContent = 'Search results';
        const backButton = document.getElementById('backBtn');
        backButton.style.display = 'inline';
        backButton.onclick = () => {
            document.getElementById('search').value = '';
            this.fetchDirectory(this.currentPath);
        };
    }

    async deletePath(encodedPath) {
        const path = decodeURIComponent(encodedPath);
        if (!confirm('Delete ' + path + '?')) return;
        try {
            await api.deletePath(path);
            this.fetchDirectory(this.currentPath);
        } catch (err) {
            console.error(err);
        }
    }

    async promptAndPost(encodedPath, apiFn, message) {
        const path = decodeURIComponent(encodedPath);
        const destination = prompt(message, path);
        if (!destination) return;
        try {
            await apiFn(path, destination);
            this.fetchDirectory(this.currentPath);
        } catch (err) {
            console.error(err);
        }
    }

    movePath(encodedPath) {
        this.promptAndPost(encodedPath, api.movePath, 'Move to:');
    }

    copyPath(encodedPath) {
        this.promptAndPost(encodedPath, api.copyPath, 'Copy to:');
    }

    navigateTo(path) {
        const url = path ? `?path=${encodeURIComponent(path)}` : location.pathname;
        history.pushState({}, '', url);
        this.fetchDirectory(path);
    }
}

const explorer = new FileExplorer();

window.addEventListener('load', () => {
    explorer.bindGlobalEvents();
    explorer.fetchDirectory();
});

window.onpopstate = () => {
    const params = new URLSearchParams(location.search);
    const path = params.get('path') || '';
    explorer.fetchDirectory(path);
};

