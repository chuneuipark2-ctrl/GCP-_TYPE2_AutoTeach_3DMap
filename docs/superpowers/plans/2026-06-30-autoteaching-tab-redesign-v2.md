# 오토티칭 탭 재설계 v2 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 평소 3탭, 로고 로그인 시 오토티칭 4탭으로 노출하고, 오토티칭 탭을 목업대로 상태 기반(진행/결과) 화면 + MM 보조탭으로 정리한다.

**Architecture:** 런 엔진·안전 로직은 **로직 무변경**(출력만 새 패널로 재배선). PageAutoTeaching의 TabControl을 SETUP/RUN/REVIEW 패널 + MM 보조탭으로 재편하고 `SetView()`로 전환. 신규는 셀-경계 PAUSE 하나. 권한은 기존 `Logo_Click`/`adminModeTimer`에 노출/숨김만 부착.

**Tech Stack:** .NET 6 WPF, 기존 스타일(LblStyle/BtnStyle, #FF22B9AF/#FFB8D8FC), gClass 싱글톤, cIniAccess.

**검증:** 테스트 프로젝트·git 없음 → 모듈마다 빌드 검증 + 동작 보존 수동 확인. 0x95 반영·스냅샷·PAUSE 실런은 **크레인 실기 게이트**. 빌드(PowerShell, `2>&1` 금지):
```powershell
$out = dotnet build "C:\Users\H-YUN\Desktop\프로젝트\신규지상반 및 오토티칭\gcp_type2-TP2_final\gcp_type2-TP2_VER1\gcp_Wpf.csproj" -c Debug -nologo; $out | Select-String ": error "
```
출력에 `: error` 없으면 통과.

---

## 파일 구조

| 파일 | 변경 | 모듈 |
|---|---|---|
| `MainWindow.xaml.cs` | `RevealAutoTeachingTab` 호출을 로고 로그인으로; `HideAutoTeachingTab` 신규 + 타임아웃/로그오프에 부착 | M1 |
| `PageCraneSet.xaml` / `.cs` | `btn_AutoTeaching` 버튼 + `Btn_AutoTeaching_Click` 제거 | M1 |
| `PageAutoTeaching.xaml` | TabControl → SETUP/RUN/REVIEW 패널 + MM 보조탭; 하단 버튼바 분해·재배치 | M2/M3/M4 |
| `PageAutoTeaching.xaml.cs` | `SetView` 상태머신; PAUSE 훅; 진행화면 갱신(스텝/ETA/카운트/최근/스냅샷); 결과화면 정돈 | M2/M3/M4 |

**컨트롤 이름 계약(신규 x:Name — XAML과 C#가 합의):**
- 패널: `pnl_Setup`, `pnl_Run`, `pnl_Review`, `pnl_MmMove`
- 진행: `lblRun_Cell`, `imgRun_Snap`, `stepMove`/`stepArrive`/`stepShoot`/`stepAnalyze`(Border), `lblRun_Progress`, `progRun`(ProgressBar), `lblRun_Eta`, `lblRun_Ok`, `lblRun_Fail`, `lstRun_Recent`(ListBox), `btnRun_Pause`, `btnRun_Review`
- 결과: `lblRev_Badge`, `lblRev_LiftBA`, `lblRev_TravBA`, `btnRev_ApplyOne`
- 기존 이동(이름·Click 유지): 설정 컨트롤들(`combo_Camera`/`combo_Row`/`edit_BayRange`/`edit_LevRange`/`chk_HasCargo`), `btn_Start`/`btn_Calib`→pnl_Setup, `btn_Stop`/`btn_Skip`→pnl_Run, `btn_Save`/`btn_Verify`→pnl_Review, `grid_Review`/`img_Raw`/`img_Cal`/`btn_SelectNormal`/`btn_RejectCell`/`btn_ApplySelected`→pnl_Review, `listBox_Log` 유지

---

## Module M1 — 탭 권한(로고 로그인 노출/숨김)

### Task M1.1: 로고 로그인 성공 시 탭 노출

**Files:** Modify `MainWindow.xaml.cs:2229` 부근 (`Logo_Click` 로그인 성공 블록)

- [ ] **Step 1: 로그인 성공 분기에 노출 호출 추가**

`Logo_Click`의 `if (result.GetValueOrDefault()) { ... }` 블록 안, `gClass.str.GcpInfo.isAdminMode = true;` 다음 줄에 추가:
```csharp
                RevealAutoTeachingTab();
```

- [ ] **Step 2: 빌드 검증** — Run 빌드 명령. Expected: `: error` 없음.

### Task M1.2: HideAutoTeachingTab + 로그오프/타임아웃에 부착

**Files:** Modify `MainWindow.xaml.cs` (신규 메서드 + `Logo_Click` 해제 분기 + `AdminModeTimer_Elapsed`)

- [ ] **Step 1: HideAutoTeachingTab 메서드 추가**

`RevealAutoTeachingTab()`(2003) 바로 아래에 추가:
```csharp
        public void HideAutoTeachingTab()
        {
            Dispatcher.Invoke(() =>
            {
                Btn_AutoTeaching.Visibility = Visibility.Collapsed;
                // 현재 오토티칭 페이지를 보고 있으면 안전 페이지로 이탈
                // (런 중이면 Page_Change가 AbortAndDisarmForShutdown으로 무장해제 처리)
                if (curPageIdx == cConstDefine.PAGE_AUTOTEACHING)
                    Page_Change(cConstDefine.PAGE_SEMI);
            });
        }
```
(주: `curPageIdx`, `Page_Change`, `PAGE_SEMI`는 기존 멤버. `HideAutoTeachingTab`은 UI스레드 외에서도 호출될 수 있어 `Dispatcher.Invoke`.)

- [ ] **Step 2: 로고 재클릭(관리자 해제) 분기에 호출**

`Logo_Click`의 `if (gClass.str.GcpInfo.isAdminMode) { ... }` 블록 안, `pageDio.SetPageMode(false);` 다음(또는 `return;` 직전)에 추가:
```csharp
                HideAutoTeachingTab();
```

- [ ] **Step 3: 5분 타임아웃 해제에도 호출**

`AdminModeTimer_Elapsed`(2269)의 `Dispatcher.Invoke(() => { ... })` 안, `pageCraneSet.SetPageMode(false);` 다음에 추가:
```csharp
                    Btn_AutoTeaching.Visibility = Visibility.Collapsed;
                    if (curPageIdx == cConstDefine.PAGE_AUTOTEACHING)
                        Page_Change(cConstDefine.PAGE_SEMI);
```
(이미 `Dispatcher.Invoke` 안이므로 `HideAutoTeachingTab()` 대신 인라인. 동일 동작.)

- [ ] **Step 4: 빌드 검증** — Expected: `: error` 없음.

### Task M1.3: 크레인 설정 진입 버튼 제거

**Files:** Modify `PageCraneSet.xaml:32-47`(버튼 제거), `PageCraneSet.xaml.cs:240-253`(핸들러 제거)

- [ ] **Step 1: XAML 버튼 제거**

`PageCraneSet.xaml`에서 `<Button ... x:Name="btn_AutoTeaching" ... Click="Btn_AutoTeaching_Click" ...> ... </Button>` 블록 전체(32-47) 삭제.

- [ ] **Step 2: 핸들러 제거**

`PageCraneSet.xaml.cs`에서 `Btn_AutoTeaching_Click` 메서드(240-253) 전체 삭제.

- [ ] **Step 3: 잔존 참조 확인** — Grep `btn_AutoTeaching`/`Btn_AutoTeaching_Click` across `*.cs`/`*.xaml`. Expected: 0건(생성 파일 제외).

- [ ] **Step 4: 빌드 검증** — Expected: `: error` 없음. (수동: 로고 로그인 시 탭 노출, 로고 재클릭/5분 후 숨김 + 오토티칭에 있었으면 반자동으로 이탈.)

---

## Module M2 — PageAutoTeaching 상태머신 셸

> 목표: 기존 3-TabItem을 「SETUP/RUN/REVIEW 패널 + MM 보조탭」으로 재편하고 `SetView`로 전환. **버튼은 x:Name·Click 유지한 채 위치만 이동**(엔진 핸들러 무변경).

### Task M2.1: 상태 enum + SetView + 패널 골격

**Files:** Modify `PageAutoTeaching.xaml`(콘텐츠 영역 재구성), `PageAutoTeaching.xaml.cs`(SetView)

- [ ] **Step 1: 콘텐츠 영역을 4패널 + MM탭으로 재편(XAML 골격)**

기존 `<TabControl ...>`의 메인 콘텐츠를 아래 골격으로 교체. **SETUP에는 기존 AUTO TEACHING 탭의 설정 그리드(combo_Camera/lbl_camStatus/combo_Row/edit_BayRange/edit_LevRange/chk_HasCargo)와 listBox_Log를 그대로 옮기고**, START/CALIB 버튼을 같은 패널에 둔다. RUN/REVIEW 패널은 M3/M4에서 내용 채움(지금은 빈 Grid + x:Name만). MM은 기존 MM POSITION MOVE 내용을 `pnl_MmMove`로 옮긴다. 시각 폴리시는 목업 참조, 단 아래 x:Name은 고정:
```xml
<Grid Grid.Row="1">
    <!-- SETUP: 기존 설정 그리드 + 로그 + START/CALIB(이동) -->
    <Grid x:Name="pnl_Setup" Visibility="Visible">
        <!-- 기존 AUTO TEACHING 탭의 설정 Grid(설정 입력) + listBox_Log 를 여기로 이동 -->
        <!-- btn_Start, btn_Calib 를 이 패널 하단에 배치(x:Name·Click 유지) -->
    </Grid>
    <!-- RUN: M3에서 채움 -->
    <Grid x:Name="pnl_Run" Visibility="Collapsed"/>
    <!-- REVIEW: M4에서 채움(기존 grid_Review/img_Raw/img_Cal/버튼 이동) -->
    <Grid x:Name="pnl_Review" Visibility="Collapsed"/>
    <!-- MM(정비) 보조 -->
    <Grid x:Name="pnl_MmMove" Visibility="Collapsed">
        <!-- 기존 MM POSITION MOVE 탭 내용 이동 -->
    </Grid>
</Grid>
```
하단 공통 버튼바(START/STOP/SKIP/CALIB/SAVE/EXCEL/VERIFY/CLOSE)는 **분해**: 각 버튼을 해당 패널로 이동(이름·Click 유지). `btn_Close`는 SETUP/REVIEW에서 접근 가능하게 둔다. MM 진입 버튼 `btnSetup_Mm`(신규, Click 핸들러 `Btn_Mm_Click`)을 SETUP에 추가, MM에는 복귀 버튼 `btnMm_Back`(Click `Btn_MmBack_Click`).

- [ ] **Step 2: SetView + MM 전환 핸들러(.cs)**

```csharp
private enum TeachView { Setup, Run, Review, MmMove }

private void SetView(TeachView v)
{
    Dispatcher.Invoke(() =>
    {
        pnl_Setup.Visibility   = v == TeachView.Setup  ? Visibility.Visible : Visibility.Collapsed;
        pnl_Run.Visibility     = v == TeachView.Run    ? Visibility.Visible : Visibility.Collapsed;
        pnl_Review.Visibility  = v == TeachView.Review ? Visibility.Visible : Visibility.Collapsed;
        pnl_MmMove.Visibility  = v == TeachView.MmMove ? Visibility.Visible : Visibility.Collapsed;
    });
}

private void Btn_Mm_Click(object sender, RoutedEventArgs e)   => SetView(TeachView.MmMove);
private void Btn_MmBack_Click(object sender, RoutedEventArgs e) => SetView(TeachView.Setup);
```

- [ ] **Step 3: PageInit에서 기본 뷰 = Setup**

`PageInit()` 끝(기존 `PopulateReview();` 직전/직후)에 추가:
```csharp
            SetView(TeachView.Setup);
```

- [ ] **Step 4: 빌드 검증** — Expected: `: error` 없음. (모든 기존 x:Name이 유지·이동만 됐는지 컴파일로 확인.)

### Task M2.2: 상태 전환 배선(START→Run, 완료→Review 버튼 활성)

**Files:** Modify `PageAutoTeaching.xaml.cs`(`Btn_Start_Click` 흐름, 런 완료 지점)

- [ ] **Step 1: START 시 Run 뷰로 전환**

`Btn_Start_Click`에서 재진입 가드 통과 후(런을 실제로 시작하는 지점, Phase1 호출 직전)에 추가:
```csharp
            SetView(TeachView.Run);
            Dispatcher.Invoke(() => btnRun_Review.IsEnabled = false);
```

- [ ] **Step 2: 런 완료 시 '결과 확인·반영하기' 활성(자동전환 안 함)**

`Btn_Start_Click`의 런 종료 처리부(정상 완료/취소 후 finally 또는 종료 로그 지점)에 추가:
```csharp
            Dispatcher.Invoke(() => btnRun_Review.IsEnabled = true);
```
`btnRun_Review`의 Click 핸들러:
```csharp
private void Btn_Review_Click(object sender, RoutedEventArgs e) => SetView(TeachView.Review);
```
(REVIEW 진입 시 표는 이미 `PopulateReview()`로 채워짐 — 필요 시 여기서 재호출.)

- [ ] **Step 3: 빌드 검증** — Expected: `: error` 없음.

---

## Module M3 — 진행 화면 + PAUSE

### Task M3.1: 진행 화면 XAML(pnl_Run 내용)

**Files:** Modify `PageAutoTeaching.xaml`(`pnl_Run`)

- [ ] **Step 1: 목업 2 레이아웃 골격(고정 x:Name)**

`pnl_Run` 안에 — 좌(현재 셀 + 스냅샷 + 스텝), 우(진행률+ETA+카운트+최근), 하단(일시정지/중지/결과확인). 시각 폴리시는 목업 참조, x:Name 고정:
```xml
<Grid>
    <Grid.ColumnDefinitions><ColumnDefinition Width="55*"/><ColumnDefinition Width="45*"/></Grid.ColumnDefinitions>
    <Grid Grid.Column="0" Margin="0,0,3,0">
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
        <Label x:Name="lblRun_Cell" Grid.Row="0" Content="현재 셀 —" Foreground="#FFB8D8FC" FontWeight="Bold" FontSize="14"/>
        <Image x:Name="imgRun_Snap" Grid.Row="1" Stretch="Uniform"/>
        <StackPanel Grid.Row="2" Orientation="Horizontal">
            <Border x:Name="stepMove" Background="#33000000" CornerRadius="6" Padding="8,3" Margin="2"><Label Content="이동" Foreground="#FFADADAD" FontSize="11"/></Border>
            <Border x:Name="stepArrive" Background="#33000000" CornerRadius="6" Padding="8,3" Margin="2"><Label Content="도착" Foreground="#FFADADAD" FontSize="11"/></Border>
            <Border x:Name="stepShoot" Background="#33000000" CornerRadius="6" Padding="8,3" Margin="2"><Label Content="촬영" Foreground="#FFADADAD" FontSize="11"/></Border>
            <Border x:Name="stepAnalyze" Background="#33000000" CornerRadius="6" Padding="8,3" Margin="2"><Label Content="분석" Foreground="#FFADADAD" FontSize="11"/></Border>
        </StackPanel>
    </Grid>
    <Grid Grid.Column="1" Margin="3,0,0,0">
        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
        <StackPanel Grid.Row="0">
            <Label Content="전체 진행률" Foreground="#FFADADAD" FontSize="12"/>
            <Label x:Name="lblRun_Progress" Content="0 / 0" Foreground="#FFB8D8FC" FontWeight="Bold" FontSize="20"/>
            <ProgressBar x:Name="progRun" Height="6" Minimum="0" Maximum="100" Value="0" Foreground="#FF22B9AF" Background="#33000000"/>
            <Label x:Name="lblRun_Eta" Content="예상 남은 시간 —" Foreground="#FFADADAD" FontSize="11"/>
        </StackPanel>
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,8,0,0">
            <StackPanel Margin="0,0,16,0"><Label x:Name="lblRun_Ok" Content="0" Foreground="LightGreen" FontWeight="Bold" FontSize="20"/><Label Content="정상" Foreground="#FFADADAD" FontSize="11"/></StackPanel>
            <StackPanel><Label x:Name="lblRun_Fail" Content="0" Foreground="#FFE57373" FontWeight="Bold" FontSize="20"/><Label Content="실패" Foreground="#FFADADAD" FontSize="11"/></StackPanel>
        </StackPanel>
        <Label Grid.Row="2" Content="최근 처리" Foreground="#FFADADAD" FontSize="12" Margin="0,8,0,2"/>
        <ListBox x:Name="lstRun_Recent" Grid.Row="3" Background="#19000000" Foreground="#CCADD8E6" BorderBrush="#FF555151" FontSize="11"/>
    </Grid>
    <!-- 하단 버튼: btn_Stop(이동) + btnRun_Pause(신규) + btnRun_Review(신규) -->
</Grid>
```
하단에 `btnRun_Pause`(Content="⏸ 일시정지", Click `Btn_Pause_Click`), 기존 `btn_Stop`(이동, Content 유지), `btnRun_Review`(Content="결과 확인·반영하기", Click `Btn_Review_Click`, 기본 IsEnabled=False) 배치.

- [ ] **Step 2: 빌드 검증** — 핸들러 미정의로 실패할 수 있으니 M3.2와 함께 빌드.

### Task M3.2: 진행 갱신(스텝/카운트/ETA/최근/스냅샷) — 표시만, 엔진 로직 무변경

**Files:** Modify `PageAutoTeaching.xaml.cs`

- [ ] **Step 1: 진행 표시 헬퍼 + 필드 추가**

```csharp
private readonly System.Collections.Generic.List<long> cellDurMs = new();   // ETA용 셀 소요시간
private System.Diagnostics.Stopwatch cellSw;

private void SetStep(string active)   // "move"/"arrive"/"shoot"/"analyze"/""
{
    Dispatcher.Invoke(() =>
    {
        Brush on = new SolidColorBrush(Color.FromRgb(0x22,0xB9,0xAF)), off = new SolidColorBrush(Color.FromRgb(0x33,0x33,0x33));
        stepMove.Background    = active=="move"    ? on : off;
        stepArrive.Background  = active=="arrive"  ? on : off;
        stepShoot.Background   = active=="shoot"   ? on : off;
        stepAnalyze.Background = active=="analyze" ? on : off;
    });
}

private void UpdateRunStats(int current, int total, string cellName)
{
    int ok = currentResults.Count(r => r.Success);
    int fail = currentResults.Count(r => !r.Success);
    double avg = cellDurMs.Count > 0 ? cellDurMs.Average() : 0;
    int remain = Math.Max(0, total - current);
    string eta = avg > 0 ? $"예상 남은 시간 약 {Math.Ceiling(avg*remain/60000.0)}분" : "예상 남은 시간 —";
    Dispatcher.Invoke(() =>
    {
        lblRun_Cell.Content = $"현재 셀 — {cellName}";
        lblRun_Progress.Content = $"{current} / {total}";
        progRun.Value = total > 0 ? (double)current/total*100 : 0;
        lblRun_Ok.Content = ok.ToString();
        lblRun_Fail.Content = fail.ToString();
        lblRun_Eta.Content = eta;
    });
}

private void PushRecent(TeachingResult r)
{
    string txt = r.Success ? $"{CellKey(r.Row,r.Bay,r.Level)}   정상 · 편차 {(r.BayPos>=0?"":"")}"
                           : $"{CellKey(r.Row,r.Bay,r.Level)}   실패 · {r.FailedStep}";
    Dispatcher.Invoke(() =>
    {
        lstRun_Recent.Items.Insert(0, txt);
        while (lstRun_Recent.Items.Count > 6) lstRun_Recent.Items.RemoveAt(lstRun_Recent.Items.Count-1);
    });
}
```

- [ ] **Step 2: Phase2 루프에 표시 호출 삽입(로직 무변경)**

`Phase2_TeachingLoopAsync`의 셀 루프(`for (int i = 0; i < targets.Count; i++)`, 690행) 내부에 **표시 호출만** 삽입:
- 루프 본문 시작부(이동 직전): `cellSw = System.Diagnostics.Stopwatch.StartNew(); SetStep("move"); UpdateRunStats(i, targets.Count, cellName);`
- 이동/도착 후, 캡처 직전: `SetStep("shoot");` (도착 직후 `SetStep("arrive");`)
- 추론 시작부: `SetStep("analyze");`
- `currentResults.Add(cellResult);` 직후: `cellSw?.Stop(); if (cellSw!=null) cellDurMs.Add(cellSw.ElapsedMilliseconds); PushRecent(cellResult); if (!string.IsNullOrEmpty(cellResult.RawPath)) Dispatcher.Invoke(() => imgRun_Snap.Source = LoadImageNoLock(cellResult.CalibratedPath ?? cellResult.RawPath));`
- 셀 종료(다음 셀로): `SetStep("");`
(기존 `UpdateProgress`/`AddLog` 호출은 그대로 둠 — listBox_Log·라벨 갱신 유지. `cellName`은 기존 루프 변수 사용.)

- [ ] **Step 3: 빌드 검증** — Expected: `: error` 없음.

### Task M3.3: PAUSE(셀 경계 협조적 일시정지)

**Files:** Modify `PageAutoTeaching.xaml.cs`

- [ ] **Step 1: pause 필드 + 핸들러**

```csharp
private volatile bool pauseRequested = false;

private void Btn_Pause_Click(object sender, RoutedEventArgs e)
{
    pauseRequested = !pauseRequested;
    Dispatcher.Invoke(() => btnRun_Pause.Content = pauseRequested ? "▶ 재개" : "⏸ 일시정지");
    AddLog(pauseRequested ? "[PAUSE] 일시정지 요청 — 현재 셀 완료 후 정지" : "[RESUME] 재개");
}
```

- [ ] **Step 2: 루프 셀 시작에 pause 대기 삽입(이동 전, 안전)**

`Phase2_TeachingLoopAsync` 셀 루프 `for (...)` **바로 다음 줄**(이동 시작 전, `ct.ThrowIfCancellationRequested()` 692행 근처)에:
```csharp
                while (pauseRequested && !ct.IsCancellationRequested)
                {
                    SetStatus("PAUSE", ClrWarn);
                    await Task.Delay(200, ct);
                }
```
(셀 경계에서만 대기 → 이동·캡처 도중엔 절대 멈추지 않음. STOP은 기존 `cts.Cancel()`로 `ct`가 취소되어 즉시 탈출.)

- [ ] **Step 3: 런 시작 시 pause 리셋**

`Btn_Start_Click`의 런 시작부(SetView(Run) 근처)에: `pauseRequested = false; Dispatcher.Invoke(() => btnRun_Pause.Content = "⏸ 일시정지");`

- [ ] **Step 4: 빌드 검증** — Expected: `: error` 없음. (수동/실기: 일시정지가 현재 셀 끝난 뒤 멈추고, 재개 시 다음 셀부터 진행.)

---

## Module M4 — 결과 화면 정돈 (pnl_Review)

> v1 Module C의 `grid_Review`/`img_Raw`/`img_Cal`/`ApplySelectedCellsAsync`/`Grid_Review_SelectionChanged`를 pnl_Review로 옮기고 목업 1 형태로 정돈. 반영 로직 무변경.

### Task M4.1: 결과 화면 XAML(pnl_Review)

**Files:** Modify `PageAutoTeaching.xaml`(`pnl_Review`)

- [ ] **Step 1: 기존 RESULT REVIEW 내용 이동 + 헤더/상세 정돈**

기존 RESULT REVIEW TabItem의 내용(grid_Review, 원본/보정 TabControl, 버튼들)을 `pnl_Review`로 이동하고 아래를 추가/정돈(x:Name 고정):
- 헤더: 우측에 `<Label x:Name="lblRev_Badge" Content="" Foreground="#FFFFC107" FontWeight="Bold"/>`(확인 필요 뱃지).
- 상세: 선택 셀 영역에 두 줄 — `<Label x:Name="lblRev_LiftBA" .../>`(승강 위치 X → Y) + `<Label x:Name="lblRev_TravBA" .../>`(주행 위치 X → Y). 기존 `lbl_ReviewDetail`는 제거하거나 유지(중복이면 제거).
- 버튼: 기존 `btn_RejectCell`(이 셀 반려)/`btn_ApplySelected`(선택 셀 반영)/`btn_SelectNormal`(정상 일괄) 유지 + 신규 `btnRev_ApplyOne`(Content="이 셀 반영", Click `Btn_ApplyOne_Click`). SAVE(`btn_Save`)/VERIFY(`btn_Verify`)도 이 패널로 이동(이름·Click 유지).

- [ ] **Step 2: 빌드 검증** — M4.2와 함께(신규 핸들러 의존).

### Task M4.2: 상세 before→after + '이 셀 반영' 단일

**Files:** Modify `PageAutoTeaching.xaml.cs`

- [ ] **Step 1: SelectionChanged에 승강/주행 before→after 채우기**

`Grid_Review_SelectionChanged`에서 이미지 로드 다음에 추가(기존 `info`/`levSrc` 재사용):
```csharp
                lblRev_TravBA.Content = $"주행 위치   {row.Existing} → {row.Measured}";
                int levExistBA = (info.cellLev != null && row.Lev >= 1 && row.Lev <= info.cellLev.Length) ? info.cellLev[row.Lev - 1] : 0;
                lblRev_LiftBA.Content = levSrc.Success ? $"승강 위치   {levExistBA} → {levSrc.LevelPos}" : "승강 위치   —";
```

- [ ] **Step 2: '이 셀 반영' = 단일 셀만 선택해 기존 경로 호출**

```csharp
private async void Btn_ApplyOne_Click(object sender, RoutedEventArgs e)
{
    if (grid_Review.SelectedItem is not ReviewRow sel) return;
    foreach (var r in reviewRows) r.Apply = (r == sel);
    grid_Review.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
    grid_Review.Items.Refresh();
    btn_ApplySelected.IsEnabled = false;
    try { await ApplySelectedCellsAsync(); }   // 백업/0x95/마커/가드 전부 기존 로직
    finally { btn_ApplySelected.IsEnabled = true; }
}
```

- [ ] **Step 3: PopulateReview에 '확인 필요' 뱃지**

`PopulateReview()` 끝에 추가(편차 임계 ±15mm 초과 셀이 있으면 표시 — 임계는 기존 관례 따름):
```csharp
            int needCheck = reviewRows.Count(x => Math.Abs(x.Deviation) > 15);
            Dispatcher.Invoke(() => lblRev_Badge.Content = needCheck > 0 ? $"확인 필요 {needCheck}" : "");
```

- [ ] **Step 4: 빌드 검증** — Expected: `: error` 없음. (수동: 행 선택 시 원본/보정 + 승강/주행 before→after 표시, '이 셀 반영'은 해당 셀만 반영, 편차 큰 셀 있으면 뱃지.)

---

## Self-Review (작성자 체크)

**1. Spec 커버리지:** §5 M1(권한)→M1.1-1.3 ✅ / M2(셸)→M2.1-2.2 ✅ / M3(진행+PAUSE)→M3.1-3.3 ✅ / M4(결과)→M4.1-4.2 ✅. §4 결정: 목업 그대로(M3/M4 골격+목업 참조)✅ 상태기반(SetView)✅ MM 보조탭(pnl_MmMove)✅ 원본+보정만(M4, 검출 미포함)✅ PAUSE(M3.3)✅ 크레인버튼 제거(M1.3)✅. §8 검출=범위밖 ✅.

**2. 플레이스홀더 스캔:** 코드 스텝에 실제 코드. XAML 골격은 "기존 X를 이동 + 고정 x:Name + 목업 참조 폴리시"로 명시(픽셀 디테일은 목업 위임 — 의도된 위임이며 컨트롤 계약은 고정).

**3. 타입/이름 일관성:** `SetView`/`TeachView`, `pnl_Setup/Run/Review/MmMove`, `lblRun_*`/`progRun`/`lstRun_Recent`/`btnRun_Pause`/`btnRun_Review`, `lblRev_Badge/LiftBA/TravBA`/`btnRev_ApplyOne`, `pauseRequested`, `cellDurMs`/`cellSw`, `SetStep`/`UpdateRunStats`/`PushRecent` — 전 모듈 일치. 기존 재사용: `currentResults`/`CellKey`/`TeachingResult.RawPath/CalibratedPath`/`LoadImageNoLock`/`ApplySelectedCellsAsync`/`Grid_Review_SelectionChanged`/`reviewRows`/`ReviewRow`/`cts`/`ct`/`SetStatus`/`ClrWarn`/`AddLog`/`PopulateReview` 시그니처 일치.

**주의(구현자):**
- 버튼 이동 시 **x:Name·Click 반드시 유지**(엔진 핸들러 무변경). 새 핸들러(`Btn_Pause_Click`/`Btn_Review_Click`/`Btn_Mm_Click`/`Btn_MmBack_Click`/`Btn_ApplyOne_Click`)만 추가.
- M3.2 루프 삽입은 **표시 호출만** — 이동/캡처/추론 로직·순서·반환을 건드리지 말 것. `cellName` 변수명이 다르면 실제 루프의 셀 이름 변수를 사용.
- M3.3 pause 대기는 반드시 **이동 시작 전(셀 경계)** 에만. 캡처/추론/이동 도중 삽입 금지.
- `UpdateResultCounts`(기존, 미사용)는 건드리지 않음 — 카운트는 `UpdateRunStats`가 `currentResults`에서 파생.
- M4/M3 0x95·스냅샷 실경로·PAUSE 실런은 빌드 후 **크레인 실기 게이트**.
