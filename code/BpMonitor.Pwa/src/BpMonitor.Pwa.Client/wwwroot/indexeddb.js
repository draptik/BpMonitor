(function () {
    const DB_NAME = 'BpMonitor';
    const READINGS_STORE = 'readings';
    const HANDLES_STORE = 'handles';

    function openDb() {
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, 2);
            req.onupgradeneeded = e => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains(READINGS_STORE)) {
                    db.createObjectStore(READINGS_STORE, { autoIncrement: true });
                }
                if (!db.objectStoreNames.contains(HANDLES_STORE)) {
                    db.createObjectStore(HANDLES_STORE);
                }
            };
            req.onsuccess = e => resolve(e.target.result);
            req.onerror = e => reject(e.target.error);
        });
    }

    async function getDirectoryHandle() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(HANDLES_STORE, 'readonly');
            const store = tx.objectStore(HANDLES_STORE);
            const req = store.get('directory');
            req.onsuccess = e => resolve(e.target.result || null);
            req.onerror = e => reject(e.target.error);
        });
    }

    window.bpMonitor = {
        saveReading: async function (reading) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(READINGS_STORE, 'readwrite');
                const store = tx.objectStore(READINGS_STORE);
                const req = store.add(reading);
                req.onsuccess = () => resolve();
                req.onerror = e => reject(e.target.error);
            });
        },
        loadReadings: async function () {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(READINGS_STORE, 'readonly');
                const store = tx.objectStore(READINGS_STORE);
                const req = store.getAll();
                req.onsuccess = e => resolve(e.target.result);
                req.onerror = e => reject(e.target.error);
            });
        },
        clearReadings: async function () {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(READINGS_STORE, 'readwrite');
                const store = tx.objectStore(READINGS_STORE);
                const req = store.clear();
                req.onsuccess = () => resolve();
                req.onerror = e => reject(e.target.error);
            });
        },
        pickDirectory: async function () {
            const handle = await window.showDirectoryPicker({ mode: 'readwrite' });
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(HANDLES_STORE, 'readwrite');
                const store = tx.objectStore(HANDLES_STORE);
                const req = store.put(handle, 'directory');
                req.onsuccess = () => resolve();
                req.onerror = e => reject(e.target.error);
            });
        },
        hasDirectory: async function () {
            const handle = await getDirectoryHandle();
            return handle !== null;
        },
        writeReadings: async function (json) {
            const handle = await getDirectoryHandle();
            if (!handle) throw new Error('No directory selected');
            const perm = await handle.requestPermission({ mode: 'readwrite' });
            if (perm !== 'granted') throw new Error('Permission denied');
            const fileHandle = await handle.getFileHandle('readings.json', { create: true });
            const writable = await fileHandle.createWritable();
            await writable.write(json);
            await writable.close();
        },
        readFileReadings: async function () {
            const handle = await getDirectoryHandle();
            if (!handle) return null;
            const perm = await handle.requestPermission({ mode: 'read' });
            if (perm !== 'granted') return null;
            try {
                const fileHandle = await handle.getFileHandle('readings.json');
                const file = await fileHandle.getFile();
                return await file.text();
            } catch (e) {
                if (e.name === 'NotFoundError') return null;
                throw e;
            }
        },
        saveSettings: function (settings) {
            localStorage.setItem('bpMonitor.webdav', JSON.stringify(settings));
        },
        loadSettings: function () {
            const s = localStorage.getItem('bpMonitor.webdav');
            return s ? JSON.parse(s) : { url: '', username: '', password: '' };
        }
    };
})();
