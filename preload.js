const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('electronAPI', {
    setAutoLaunch: (enable) => ipcRenderer.send('set-auto-launch', enable),
    showDialog: () => ipcRenderer.invoke('show-dialog')
});