const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
    showDialog: () => ipcRenderer.invoke('show-dialog'),
    onShowInfo: (callback) => ipcRenderer.on('show-info', callback)
})