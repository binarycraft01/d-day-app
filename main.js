const { app, BrowserWindow, Menu, ipcMain, dialog } = require('electron');
const path = require('path');
const settings = require('electron-settings');

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
            devTools: isDevelopment // 개발 모드에서만 DevTools 활성화
        },
        resizable: false,
        autoHideMenuBar: true // 메뉴바 자동 숨김 설정
    });

    // 강제로 DevTools가 열리지 않도록 차단
    win.webContents.on('devtools-opened', () => {
        win.webContents.closeDevTools();
    });

    win.loadFile('index.html');
    return win;
}

// 자동 실행 설정 함수
function setAutoLaunch(enable) {
    app.setLoginItemSettings({
        openAtLogin: enable,
        path: app.getPath('exe'),
        args: ['--no-sandbox']
    });

    settings.set('autoLaunch', enable);
}

// 앱 초기화
app.whenReady().then(() => {
    createWindow();

    // 저장된 설정을 불러와 자동 실행 적용
    const autoLaunch = settings.get('autoLaunch', true);
    setAutoLaunch(autoLaunch);

    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) {
            createWindow();
        }
    });
});

// 자동 실행 설정 변경 이벤트
ipcMain.on('set-auto-launch', (event, enable) => {
    setAutoLaunch(enable);
});


// 메인 프로세스에서 다이얼로그 이벤트 처리
ipcMain.handle('show-dialog', async () => {
    const result = await dialog.showMessageBox({
        type: 'warning',
        message: '이벤트 이름과 날짜를 모두 입력해주세요.',
        buttons: ['확인']
    });
    return result;
});

// 창닫히면 앱 종료
app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

// 추가: DevTools 열기 방지를 위한 앱 플래그 설정
app.commandLine.appendSwitch('disable-devtools');
