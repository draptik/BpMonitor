(function () {
    const DB_NAME = 'BpMonitor';
    const STORE = 'readings';

    function openDb() {
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, 1);
            req.onupgradeneeded = e => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains(STORE)) {
                    db.createObjectStore(STORE, { autoIncrement: true });
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
                const tx = db.transaction(STORE, 'readwrite');
                const store = tx.objectStore(STORE);
                const req = store.add(reading);
                req.onsuccess = () => resolve();
                req.onerror = e => reject(e.target.error);
            });
        },
        loadReadings: async function () {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(STORE, 'readonly');
                const store = tx.objectStore(STORE);
                const req = store.getAll();
                req.onsuccess = e => resolve(e.target.result);
                req.onerror = e => reject(e.target.error);
            });
        }
    };
})();
