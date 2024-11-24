// D-day 목록을 저장할 배열
let ddayList = [];

// 전역 범위에서 함수를 윈도우 객체에 추가
window.addDday = addDday;

// D-day 추가 함수
async function addDday() {
    const eventNameInput = document.getElementById('eventName');
    const eventDateInput = document.getElementById('eventDate');
    
    if (!eventNameInput.value || !eventDateInput.value) {
        await window.electronAPI.showDialog();
        setTimeout(() => {
            if (!eventNameInput.value) {
                eventNameInput.focus();
            } else if (!eventDateInput.value) {
                eventDateInput.focus();
            }
        }, 100);
        return;
    }

    const dday = {
        name: eventNameInput.value,
        date: eventDateInput.value,
        id: Date.now()
    };

    ddayList.push(dday);
    updateDdayList();
    saveToLocalStorage();

    // 입력 필드 초기화 및 포커스 설정
    eventNameInput.value = '';
    eventDateInput.value = '';
    eventNameInput.focus();
}

// D-day 목록 업데이트 함수
function updateDdayList() {
    const ddayListElement = document.getElementById('ddayList');
    ddayListElement.innerHTML = '';

    ddayList.forEach(dday => {
        const days = calculateDday(dday.date);
        const ddayElement = document.createElement('div');
        ddayElement.className = 'dday-item';
        ddayElement.innerHTML = `
            <span>${dday.name}: ${days}</span>
            <button onclick="deleteDday(${dday.id})">삭제</button>
        `;
        ddayListElement.appendChild(ddayElement);
    });
}

// D-day 계산 함수
function calculateDday(targetDate) {
    try {
        const today = new Date();
        const target = new Date(targetDate);
        
        if (isNaN(target.getTime())) {
            throw new Error('유효하지 않은 날짜');
        }
        
        today.setHours(0, 0, 0, 0);
        target.setHours(0, 0, 0, 0);
        
        const diffTime = target - today;
        const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
        
        if (diffDays > 0) {
            return `D-${diffDays}`;
        } else if (diffDays < 0) {
            return `D+${Math.abs(diffDays)}`;
        } else {
            return 'D-Day';
        }
    } catch (error) {
        return '날짜 오류';
    }
}

// D-day 삭제 함수
function deleteDday(id) {
    ddayList = ddayList.filter(dday => dday.id !== id);
    updateDdayList();
    saveToLocalStorage();
}

// 로컬 스토리지에 저장
function saveToLocalStorage() {
    try {
        localStorage.setItem('ddayList', JSON.stringify(ddayList));
    } catch (error) {
        console.error('저장 실패:', error);
    }
}

// 로컬 스토리지에서 불러오기
function loadFromLocalStorage() {
    const saved = localStorage.getItem('ddayList');
    if (saved) {
        ddayList = JSON.parse(saved);
        updateDdayList();
    }
}


// 페이지 로드 시 저장된 D-day 불러오기
window.onload = loadFromLocalStorage;

// 정보 표시 이벤트 리스너
window.electronAPI.onShowInfo((event, info) => {
    alert(`버전: ${info.version}\n개발자: ${info.developer}\n설명: ${info.description}\nElectron: ${info.electron}\n제작일: ${info.date}\n이메일: ${info.emailAddress} \n${info.comment}`);
})