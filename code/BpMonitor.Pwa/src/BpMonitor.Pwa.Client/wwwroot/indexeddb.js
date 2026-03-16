(function () {
    const DB_NAME = 'BpMonitor';
    const READINGS_STORE = 'readings';

    function openDb() {
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, 3);
            req.onupgradeneeded = e => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains(READINGS_STORE)) {
                    db.createObjectStore(READINGS_STORE, { autoIncrement: true });
                }
                if (db.objectStoreNames.contains('handles')) {
                    db.deleteObjectStore('handles');
                }
            };
            req.onsuccess = e => resolve(e.target.result);
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
        writeReadings: async function (json) {
            const dirHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
            const fileHandle = await dirHandle.getFileHandle('readings.json', { create: true });
            const writable = await fileHandle.createWritable();
            await writable.write(json);
            await writable.close();
        },
        readFileReadings: async function () {
            const dirHandle = await window.showDirectoryPicker({ mode: 'readwrite' });
            try {
                const fileHandle = await dirHandle.getFileHandle('readings.json');
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
