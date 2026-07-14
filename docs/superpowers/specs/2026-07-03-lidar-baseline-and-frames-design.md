# 라이다 baseline 설정 + 스캔 frames 노출 — 설계 스펙

> 작성일: 2026-07-03 · 대상: `gcp_type2-TP2_VER1` (지상반 WPF, .NET 6)
> 근거: [오토티칭_라이다_API_요청_로직_분석.md](../../오토티칭_라이다_API_요청_로직_분석.md) 에서 발견한 두 갭 + 비전팀 스펙(2026-07-02)

## 1. 배경 · 목적

기존 코드 분석에서 비전팀 라이다 스펙 대비 두 갭이 확인됨:

1. **baseline 설정(스펙 ③ `POST :9000/api/lidar/baseline`)이 지상반에 미구현.** `LogLidarDiagAsync`의 안내 로그 문자열([PageAutoTeaching.xaml.cs:2098](../../../PageAutoTeaching.xaml.cs))에만 언급되어, 지상반에서는 baseline을 설정할 수 없고 진단만 가능. 최초 설치/기구 재배치 시 비전 웹 UI로만 잡을 수 있어 현장 운용이 불편.
2. **`frames`(5~100) 파라미터가 정의만 있고 실사용 안 됨.** 모든 호출부가 `LidarScanStartAsync(null)`([PageAutoTeaching.xaml.cs:2047](../../../PageAutoTeaching.xaml.cs)) → 항상 서버 기본 20프레임. 프레임 수를 조정할 수단이 없음.

**목적**: (①) 지상반에서 직접 baseline을 설정하는 버튼 추가, (②) 수동 라이다 스캔 시 frames를 지정할 수 있게 노출.

## 2. 범위

**포함(In)**
- `VisionApiClient`에 baseline POST 메서드 + 응답 모델 신설.
- `PageAutoTeaching`에 baseline 버튼·핸들러·실행 래퍼 추가.
- 수동 라이다 스캔 버튼에 frames 입력 TextBox 추가 + 값 전달 경로 연결.

**제외(Out)**
- 자동 티칭/CALIB/헬스체크 경로의 frames는 **기본 20 그대로 유지**(동작 불변).
- baseline 결과 시각화/이력 저장, 프레임 수 프리셋, 넘패드 팝업 등은 범위 밖(YAGNI).

## 3. 확정 결정사항 (사용자 승인)

| 항목 | 결정 |
|---|---|
| frames 적용 범위 | **수동 [라이다 스캔] 버튼만.** 자동 경로는 기본 20 유지 |
| baseline 실행 절차 | **확인 다이얼로그 + 가드**(isRunning 차단, 크레인 정지 상태 안내 Yes/No) |
| baseline 요청 바디 | **body 없이 전송**(스펙 ③대로). 서버 고정 50프레임 측정 |
| baseline HTTP 처리 | **전용 메서드 + 응답 모델 신설**(기존 라이다 메서드와 동일한 관대한 파싱 패턴) |
| frames 입력 UI | 기존 `edit_BayRange`/`edit_LevRange`와 동일한 평범한 `TextBox` + `EditStyle`(넘패드 없음) |

## 4. 상세 설계

### ① baseline 설정

#### 4.1 `commClass/VisionApiClient.cs` — 메서드 + 모델 신설

**메서드** `LidarSetBaselineAsync(CancellationToken ct = default)` (기존 `LidarScanStartAsync` 바로 아래 배치):
- 요청: `POST {_baseUrl 아님 → _lidarSvcUrl}/api/lidar/baseline`. **`_lidarSvcUrl`(포트 9000)** 사용 — baseline은 `LidarStatusAsync`와 같은 라이다 서비스(:9000) 엔드포인트.
- **body 없음**(`content = null`). `LastRequestJson = "(no body — 서버 고정 50프레임 baseline)"`.
- 클라이언트: **`_lidarClient`(180s)** — 50프레임 측정이 30s를 넘길 수 있어 스캔과 동일한 넉넉한 상한.
- 에러 처리: `LidarScanStartAsync`와 동일 —
  - `catch (OperationCanceledException) when (!ct.IsCancellationRequested)` → `{ Success=false, Error="라이다 baseline 타임아웃 초과 — 라이다 서비스 무응답" }`. 사용자 취소(ct)는 전파.
  - 응답: `LastResponseJson`/`LastHttpStatusCode` 저장, `JsonException` → 파싱실패 객체, `null` → 빈응답 방어 객체, `HttpStatusCode` 대입 후 반환.

**모델** `LidarBaselineResponse` (기존 `LidarScanResponse` 패턴 그대로, 응답 스펙 미확정이므로 관대하게):
- `success`(bool), `message`, `error`, `failed_step`(string?), `elapsed_ms`(double), `timestamp`(string?).
- `baseline_y_center`(double?) — 설정된 기준 중심높이(있으면 로그로 확인용).
- `[JsonExtensionData] Extra`(Dictionary) — 서버 추가 필드 보존.
- `[JsonIgnore] HttpStatusCode`(int).

#### 4.2 `PageAutoTeaching.xaml` — baseline 버튼

버튼 행(현재 `라이다 스캔 | MM 이동(정비) | CALIB | START`, [130~137](../../../PageAutoTeaching.xaml))에 `btn_LidarBaseline` 추가:
- `Content="기준설정"`, `Click="Btn_LidarBaseline_Click"`.
- 색상: 드물게 쓰는 위험 동작 → 주황 계열 `Background="#FFB8860B"`(DarkGoldenrod)로 라이다/START와 구분. Foreground/Border는 기존 라이다 버튼과 동일 톤.
- `ToolTip`: "라이다 baseline 설정 — 최초 설치/기구 재배치 후 1회. 크레인 정지 상태에서 현재 프레임을 기준으로 등록(POST :9000/api/lidar/baseline). 미설정 시 승강 추론이 lidar_missing으로 실패".
- 배치 위치: `라이다 스캔` 버튼 **바로 오른쪽**(즉 `프레임 입력 → 라이다 스캔 → 기준설정 → MM 이동(정비) → CALIB → START` 순).

#### 4.3 `PageAutoTeaching.xaml.cs` — 핸들러 + 래퍼

**핸들러** `Btn_LidarBaseline_Click` (기존 `Btn_Lidar_Click` [2103](../../../PageAutoTeaching.xaml.cs) 옆):
- `if (isRunning) { AddLog("[BASELINE] 오토티칭/캘리브레이션 실행 중에는 설정 불가"); return; }`
- `if (lidarBusy) return;` (기존 `lidarBusy` 플래그 재사용) → `lidarBusy = true`, 버튼 비활성.
- **확인 다이얼로그**: `MessageBox.Show("크레인이 정지한 상태에서 현재 라이다 프레임을 기준(baseline)으로 설정합니다.\n설치/기구 재배치 후 1회만 수행하세요. 계속할까요?", "LiDAR Baseline", YesNo, Warning)` → `No`면 return.
- Config.ini에서 `VISIONIP`/`VISIONPORT` 로드 → `visionApi.SetBaseUrl(ip, port)` (`Btn_Lidar_Click`과 동일 로직).
- `await RunLidarBaselineAsync(CancellationToken.None)`.
- `finally`: `lidarBusy = false`, 버튼 재활성.

**래퍼** `RunLidarBaselineAsync(CancellationToken ct)` (기존 `RunLidarScanAsync` [2042](../../../PageAutoTeaching.xaml.cs) 구조 미러):
- TX 로그: `[BASELINE] set baseline 요청 → {LidarSvcUrl}/api/lidar/baseline (body 없음 = 서버 고정 50프레임)`.
- `visionApi.LidarSetBaselineAsync(ct)` 호출. `OperationCanceledException`은 re-throw.
- 결과 로그: HTTP/elapsed/success, `message`/`error`/`failed_step`, 응답 원문(1000자 컷).
- 성공 시 `baseline_y_center` 값 로그.
- 실패 시 `LogLidarDiagAsync(ct)` 호출(:9000 status 진단).
- 반환: `res.Success`(bool).

### ② frames 노출 (수동 버튼만)

#### 4.4 `PageAutoTeaching.xaml` — frames 입력

`btn_Lidar` **왼쪽**(버튼 행 맨 앞)에 라벨 + TextBox 추가:
- `<Label Content="프레임" .../>` + `<TextBox x:Name="edit_LidarFrames" Style="{StaticResource EditStyle}" Width="48" .../>` (좁은 폭, 기본 `Text=""` 빈칸).
- `ToolTip`: "비우면 기본 20프레임, 지정 시 5~100 (범위 밖은 자동 보정)".

#### 4.5 `PageAutoTeaching.xaml.cs` — frames 전달

**`RunLidarScanAsync` 시그니처 확장** ([2042](../../../PageAutoTeaching.xaml.cs)):
```csharp
private async Task<bool> RunLidarScanAsync(string context, CancellationToken ct, bool verbose = true, int? frames = null)
```
- 본문의 `visionApi.LidarScanStartAsync(null, ct)` → `visionApi.LidarScanStartAsync(frames, ct)`.
- 시작 로그(2044)의 "body 없음 = 서버 기본 20프레임" 문구를 frames 유무에 따라 분기(`frames.HasValue ? $"frames={frames}" : "body 없음 = 서버 기본 20프레임"`).

**기존 자동 호출부는 인자 미지정 → `frames=null` → 동작 불변**:
- CALIB [605](../../../PageAutoTeaching.xaml.cs), 셀 Z추론 직전 [1074](../../../PageAutoTeaching.xaml.cs), START 헬스체크 [1943](../../../PageAutoTeaching.xaml.cs) — 수정 없음.

**`Btn_Lidar_Click` 수정** ([2103](../../../PageAutoTeaching.xaml.cs)):
- `SetBaseUrl` 후, `edit_LidarFrames.Text`를 `int.TryParse`로 파싱: 성공 시 `int?` 값, 빈칸/실패 시 `null`.
- `await RunLidarScanAsync("수동 버튼", CancellationToken.None, frames: parsed);`
- (클램프 5~100은 `LidarScanStartAsync`가 처리하므로 UI에서 별도 클램프 불필요.)

## 5. 에러 · 취소 · 타임아웃

| 상황 | 처리 |
|---|---|
| baseline 타임아웃(180s 초과) | `Success=false, Error="...타임아웃..."` → 실패 로그 + `LogLidarDiagAsync` |
| baseline 사용자 취소 | `RunLidarBaselineAsync`는 `CancellationToken.None`으로 호출되어 실질 취소 없음(수동 단발). OCE 경로는 방어적으로 re-throw |
| baseline 실패(502/503/파싱) | 응답 원문 로그 + `LogLidarDiagAsync`(미연결/워밍업/baseline 등 진단) |
| frames 무효 입력 | `null` 처리 → 서버 기본 20. 5~100 밖 정수 → 클라이언트가 클램프 |

## 6. 검증 기준

- **자동(이 세션에서 실행)**: `dotnet build` 성공(컴파일·XAML 바인딩 이름 일치). 이 프로젝트엔 자동 테스트 스위트가 없어 빌드가 유일한 자동 게이트.
- **기능(장비 환경, 사용자 확인)**:
  1. 설정 패널에 `기준설정` 버튼과 `프레임` 입력칸이 보인다.
  2. `기준설정` 클릭 → 확인 다이얼로그 → 예 → 로그에 `[BASELINE]` TX/RX와 `baseline_y_center` 출력, `:9000/api/lidar/baseline` 호출됨.
  3. `프레임`에 예: 50 입력 후 `라이다 스캔` → 응답 `frames=50` echo 확인. 빈칸이면 기본 20.
  4. `기준설정`은 오토티칭/캘리브 실행 중 차단된다.
  5. 자동 티칭 셀 스캔은 여전히 기본 20(회귀 없음).

## 7. 영향 범위 · 비변경 보장

- 신규: `VisionApiClient.LidarSetBaselineAsync` + `LidarBaselineResponse`, `Btn_LidarBaseline_Click` + `RunLidarBaselineAsync`, XAML 버튼/입력칸 2개.
- 수정: `RunLidarScanAsync`에 옵션 파라미터 1개 추가(기존 호출부 호환), `Btn_Lidar_Click`에서 frames 파싱·전달, 시작 로그 문구 분기.
- **비변경 보장**: 자동 티칭/CALIB/헬스체크의 라이다 스캔 프레임 수(20)와 흐름은 그대로. 기존 API 메서드·모델 무수정.
- 리스크: baseline 응답 스펙 미확정 → `[JsonExtensionData]`로 관대 파싱하여 파싱 실패로 인한 거짓 실패 방지(기존 라이다 응답과 동일 전략).
