const { app, BrowserWindow, Menu, ipcMain, dialog } = require('electron')
const path = require('path')

// Windows 시작시 프로그램 자동 실행 설정 추가
if (process.platform === 'win32') {
    app.setLoginItemSettings({
        openAtLogin: true,
        path: process.execPath,
        args: []
    });
}

function createWindow() {
    const win = new BrowserWindow({
        width: 500,
        height: 700,
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            preload: path.join(__dirname, 'preload.js'),
            devTools: false // 개발자 도구 비활성화
        },
        resizable: false,
        autoHideMenuBar: false
    })

    // 개발자 도구 창 비활성화
    win.webContents.on('devtools-opened', () => {
        win.webContents.closeDevTools();
    });

    win.loadFile('index.html')
    return win;
}

// 메인 프로세스에서 다이얼로그 이벤트 처리
ipcMain.handle('show-dialog', async () => {
    const result = await dialog.showMessageBox({
        type: 'warning',
        message: '이벤트 이름과 날짜를 모두 입력해주세요.',
        buttons: ['확인']
    });
    return result;
});

app.whenReady().then(() => {
    createWindow()

    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) {
            createWindow()
        }
    })
})

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit()
    }
})