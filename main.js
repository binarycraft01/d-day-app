const { app, BrowserWindow, Menu, ipcMain, dialog } = require('electron')
const path = require('path')

// Windows 시작시 프로그램 자동 실행 설정 추가
if (process.platform === 'win32') {
    app.setLoginItemSettings({
        openAtLogin: true,
        path: process.execPath,
        args: ['--no-sandbox'] //필요 시 플래그 추가하기
    });
}

// 실행 모드 확인 (development 또는 production)
const isDevelopment = process.env.NODE_ENV === 'development';

function createWindow() {
    const win = new BrowserWindow({
        width: 500,
        height: 700,
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            preload: path.join(__dirname, 'preload.js'),
            devTools: isDevelopment // 개발 모드인지 확인 후 개발모드에서만 DevTools 활설화하기
        },
        resizable: false,
        autoHideMenuBar: false
    })

    // 강제로 DevTools가 열리지 않도록 차단
    win.webContents.on('devtools-opened', () => {
        win.webContents.closeDevTools();
    });

    win.loadFile('index.html');
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

// 모든 창이 닫히면 앱 종료
app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

// 추가: DevTools 열기 방지를 위한 앱 플래그 설정
app.commandLine.appendSwitch('disable-devtools');