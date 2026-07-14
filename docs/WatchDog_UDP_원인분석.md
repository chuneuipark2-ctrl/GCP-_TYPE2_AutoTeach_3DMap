# UDP 스레드 WatchDog 간헐적 종료 원인 분석

## WatchDog 목적

- **메인 프로그램이 죽어서 메인 타이머가 돌지 않으면, 통신 스레드들도 정리**하기 위한 감시 기능.
- 따라서 “메인 타이머가 일정 시간 동안 한 번도 안 돌면” 통신 스레드를 종료하는 것이 올바른 동작이다.

## WatchDog 동작 요약

- **메인 타이머**(`TestTimer_Elapsed`, 1초 주기): `cIniAccess.watchDogCnt` 증가
- **UDP 스레드**(`ReceiveData`): 매 루프에서 `watchCnt` 1 감소, `watchCnt < 0`일 때만 아래 검사
  - `watchDogCnt`(로컬) < `cIniAccess.watchDogCnt`(전역) 이면 **동기화** (로컬 = 전역, `watchCnt = WatchDogCheckInterval`)
  - 그렇지 않으면 **스레드 종료** ("COMM == Udp 스레드 WatchDog 종료")
- **검사 주기**: `WatchDogCheckInterval = 30` → (1+30)회 루프마다 검사 (루프당 Sleep 300ms 기준으로 **약 10초**).  
  메인 타이머가 1초 주기이므로, 실제로 메인이 죽었을 때만 10초 이상 전역이 안 올라가서 종료되도록 유도.

---

## 원인 후보

### 1. **[가장 유력] 전역 watchDogCnt 메모리 가시성 (volatile 미적용)**

- `cIniAccess.watchDogCnt`는 **다른 스레드(메인 타이머)**에서만 증가하고, **UDP 스레드**에서 읽습니다.
- `long`에 `volatile`이 없으면, UDP 스레드가 **캐시된 옛날 값**만 보는 경우가 있습니다.
- 동기화 직후 로컬 = 100으로 맞춰 두어도, (1+WatchDogCheckInterval)번 루프 후에 전역을 다시 읽을 때 **아직 100으로 보이면**  
  `watchDogCnt >= cIniAccess.watchDogCnt`가 되어 잘못 종료할 수 있습니다.

**대응:** `watchDogCnt`를 **`volatile long`**으로 선언. (적용함. UDP 쪽에서는 기존대로 `cIniAccess.watchDogCnt` 읽기만 하면, volatile로 인해 최신 값이 보임.)

---

### 2. 메인 타이머가 검사 주기(약 10초) 이상 한 번도 안 돌 경우

- (1+WatchDogCheckInterval)번 루프 최소 시간: 첫 루프 `Thread.Sleep(10000)` 후에는 `timerMs = 300` 이므로  
  **31 × 300ms ≈ 9.3초 이상** (WatchDogCheckInterval=30 기준).
- 이 동안 `cIniAccess.watchDogCnt`가 한 번도 증가하지 않으면 (메인 타이머가 한 번도 실행되지 않으면)  
  동기화 직후 (1+WatchDogCheckInterval)번 만에 전역이 그대로라서 WatchDog 종료 조건을 만족합니다.
- `TestTimer_Elapsed` 안에서 **`Dispatcher.Invoke(...)`로 UI 스레드에 큰 블록**을 넣고 있어,  
  UI 스레드가 오래 블로킹되면 타이머 콜백이 완료되지 못하고, 다음 타이머 틱이 지연되거나 쌓일 수 있습니다.
- `System.Timers.Timer`는 기본적으로 SynchronizingObject를 쓰지 않으므로, 콜백이 스레드 풀에서 실행되고  
  한 콜백이 `Dispatcher.Invoke`에서 블로킹되어 있어도 다음 틱은 다른 스레드에서 실행됩니다.  
  다만 **스레드 풀/UI 부하**로 인해 1초 간격이 검사 주기(약 10초) 이상으로 늘어나는 구간이 있으면 동일 현상이 나올 수 있습니다.

**대응:**  
- UI 쪽에서 1초마다 돌아가는 작업을 가볍게 하거나,  
- 오래 걸리는 부분은 `Dispatcher.BeginInvoke` 등으로 비동기 처리해 메인 타이머가 가능한 한 1초마다 끝나도록 유지.

---

### 3. UDP 수신 블로킹으로 인한 “빠른 11번 루프” 가능성 (이론적)**

- `udpClient.Receive(ref remoteEndPoint)`는 **데이터가 올 때까지 블로킹**됩니다.
- SRM에서 패킷이 자주 오면, `Thread.Sleep(300)` + `Send_Command()` + 수신 + 파싱으로 한 루프가 1초보다 짧아질 수 있습니다.
- (1+WatchDogCheckInterval)번이 1초 안에 끝나면, 동기화 직후 다음 검사 시점에 **전역이 아직 같은 값**일 수 있고,  
  그때 **1번(가시성)**과 겹치면 종료가 더 잘 발생할 수 있습니다.

**대응:** 1번(volatile/Volatile.Read) 적용이 우선.

---

### 4. watchDogCnt = 0 으로 초기화되는 시점

- `MainWindow_Closing`에서 `cIniAccess.watchDogCnt = 0` 실행.
- 이때 UDP 스레드는 아직 살아 있을 수 있어,  
  로컬은 이전에 동기화된 값(예: 100)인데 전역만 0이 되면 `watchDogCnt >= cIniAccess.watchDogCnt`가 되어  
  종료 로그가 한 번 나올 수 있습니다.  
  정상 종료 시나리오라면 “간헐적”이라기보다 **종료 시 한 번** 발생 가능성에 가깝습니다.

---

## 권장 수정 사항

1. **즉시 적용 권장**  
   - `cIniAccess.watchDogCnt`를 **`volatile long`**으로 선언 (적용 완료).
2. **추가 개선**  
   - `TestTimer_Elapsed` 내부의 UI 업데이트를 가능한 한 짧게 유지하고,  
   - 무거운 작업은 비동기/다른 주기로 분리해, 1초 주기가 검사 주기(약 10초) 이상 지연되지 않도록 유지.

이 중 **1번(volatile)** 적용으로 대부분의 간헐적 WatchDog 종료는 줄어드는 것으로 보는 것이 타당합니다.
