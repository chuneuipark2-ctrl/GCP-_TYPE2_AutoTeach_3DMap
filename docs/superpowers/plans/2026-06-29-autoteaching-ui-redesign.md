# 오토티칭 UI 개편 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 오토티칭을 로그인 시 노출되는 최상위 탭으로 만들고, 결과 확인·반영 화면과 반자동 카메라+적용여부 스트립을 추가하며, 비전 IP/포트를 크레인 설정으로 옮긴다.

**Architecture:** 4개 모듈 A→B→C→D 순차. 전부 *additive* 이거나 *기본값으로 현행과 동일* — 정상(성공) 동작 100% 보존. 비전 서버는 localhost라 캡처 JPEG를 로컬 경로에서 직접 로드(신규 비전 API 0개). 적용은 기존 0x95 `WriteCellRangeAsync`·`BackupCellArrays`를 재사용.

**Tech Stack:** .NET 6 WPF, cIniAccess(INI), singletonClass(gClass) 상태, Vexi UDP(0x95) — *프로토콜/MCU 무수정*.

**검증 모델(이 코드베이스 특성):** 테스트 프로젝트·git 없음 → 각 태스크는 **빌드 검증 + 동작 보존 수동 확인**으로 체크포인트. 크레인 실동작(0x95 반영)이 걸린 단계는 **실기 게이트**로 별도 표시. 빌드 명령은 PowerShell에서 `2>&1` 없이:
```powershell
$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "
```
출력에 `: error` 줄이 없으면 통과(255 종료코드는 PowerShell 네이티브 stderr 오탐이므로 무시).

---

## 파일 구조 (생성/수정 맵)

| 파일 | 책임 | 모듈 |
|---|---|---|
| `PageCraneSet.xaml` / `.cs` | 비전 IP/포트 설정 필드 추가·저장 | A |
| `PageAutoTeaching.xaml` / `.cs` | IP/포트 입력 제거→ini 로드 / 결과확인 탭·반영 로직 | A, C |
| `MainWindow.xaml` / `.cs` | 최상위 오토티칭 ToggleButton(로그인 시 노출) | B |
| `PageSemiAuto.xaml` / `.cs` | 그리드 위 카메라+적용여부 스트립(기존 그리드 wrap) | D |
| `TeachingState.ini` (신규, 런타임 생성) | 셀별 측정값·이미지경로 | C |
| `Rack.ini` (기존, 키 추가) | 셀 인덱스별 적용 마커 | C |

---

## Module A — 비전 IP/포트 → 크레인 설정 이전

**저장 위치**: `Config\Config.ini`, 섹션 `SRMINFO_{srmNum}`, 키 `VISIONIP`(기본 `127.0.0.1`) / `VISIONPORT`(기본 `3080`). 기존 `SRMID` 패턴과 동일. `combo_Camera`는 세션 선택값이므로 오토티칭 탭에 유지.

### Task A1: PageCraneSet에 Vision IP/Port 입력 필드 추가

**Files:**
- Modify: `PageCraneSet.xaml` (기존 설정 그리드에 `edit_srmID1`(114-135행) 패턴을 따라 2필드 추가)

- [ ] **Step 1: XAML에 Vision IP/Port TextBox 추가**

기존 `edit_srmID1` 패턴을 복제해, 설정 입력 그리드의 빈 행/열에 아래를 추가(정확한 Grid.Row/Column은 대상 그리드의 다음 빈 줄에 맞춤):

```xml
<Label Content="Vision IP" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="White" HorizontalContentAlignment="Left" VerticalContentAlignment="Center" FontWeight="Bold" Height="34"/>
<TextBox x:Name="edit_VisionIP1" Grid.Column="1" Text="127.0.0.1" TextAlignment="Center" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" FontSize="16" BorderBrush="#FFB8D8FC" Foreground="White" Background="{x:Null}" CaretBrush="{x:Null}" Height="34"/>
<Label Content="Vision Port" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="White" HorizontalContentAlignment="Left" VerticalContentAlignment="Center" FontWeight="Bold" Height="34"/>
<TextBox x:Name="edit_VisionPort1" Grid.Column="1" Text="3080" TextAlignment="Center" HorizontalContentAlignment="Center" VerticalContentAlignment="Center" FontSize="16" BorderBrush="#FFB8D8FC" Foreground="White" Background="{x:Null}" PreviewTextInput="NumberTextBox_PreviewTextInput" CaretBrush="{x:Null}" Height="34"/>
```

- [ ] **Step 2: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음 (필드만 추가했으므로 컴파일 OK)

### Task A2: PageCraneSet 로드/저장에 Vision 설정 연결

**Files:**
- Modify: `PageCraneSet.xaml.cs` — `SetPageInit()`(168행~) 로드, `Button_Click()`(250행~) 저장

- [ ] **Step 1: SetPageInit()에 ini 로드 추가**

`SetPageInit()` 본문 끝(`edit_srmStn1.Text = ...` 다음)에 추가:

```csharp
string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
edit_VisionIP1.Text = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1");
edit_VisionPort1.Text = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080");
```

- [ ] **Step 2: Button_Click() 저장 핸들러에 검증+쓰기 추가**

`Button_Click()`의 `if (result == VarMessageBoxResult.OK)` 블록 안, `srmID` 저장 직후에 추가:

```csharp
string visIni2 = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
string visIp = edit_VisionIP1.Text.Trim();
if (System.Net.IPAddress.TryParse(visIp, out _) == false)
{
    VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("Vision IP 형식이 올바르지 않습니다."), VarMessageBoxButton.OK);
    return;
}
if (!int.TryParse(edit_VisionPort1.Text.Trim(), out int visPort) || visPort < 1 || visPort > 65535)
{
    VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("Vision Port는 1~65535 숫자만 가능합니다."), VarMessageBoxButton.OK);
    return;
}
cIniAccess.Write(visIni2, "SRMINFO_" + gClass.srmNum, "VISIONIP", visIp);
cIniAccess.Write(visIni2, "SRMINFO_" + gClass.srmNum, "VISIONPORT", visPort.ToString());
```

- [ ] **Step 3: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

### Task A3: PageAutoTeaching의 두 SetBaseUrl 호출부를 ini 로드로 교체

**Files:**
- Modify: `PageAutoTeaching.xaml.cs:294-299` (Phase1_InitAsync), `:1866-1871` (Btn_Calib_Click)

- [ ] **Step 1: 두 호출부를 동일 코드로 교체**

각 위치의 기존 3줄
```csharp
string ip = edit_VisionIP.Text.Trim();
int port = int.TryParse(edit_VisionPort.Text.Trim(), out int p) ? p : 3080;
visionApi.SetBaseUrl(ip, port);
```
을 아래로 교체:
```csharp
string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
string ip = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1").Trim();
int port = int.TryParse(cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080").Trim(), out int p) ? p : 3080;
visionApi.SetBaseUrl(ip, port);
```
(주: 기본값을 `127.0.0.1`/`3080`으로 유지 → 설정 미입력 시 현행과 동일한 BaseUrl. `gClass`는 이 파일에서 이미 사용 중이라 스코프 OK.)

- [ ] **Step 2: 빌드 검증** — 아직 `edit_VisionIP/Port`는 XAML에 남아있어 참조 깨지지 않음.

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

### Task A4: 오토티칭 탭에서 IP/Port 입력 필드 제거

**Files:**
- Modify: `PageAutoTeaching.xaml:117-126` (Vision IP/Port Label+TextBox 제거, Camera ComboBox·camStatus는 유지)

- [ ] **Step 1: XAML에서 IP/Port만 제거**

`PageAutoTeaching.xaml`의 AUTO TEACHING 탭 설정 그리드에서 아래 4개 요소(라벨 2 + 텍스트박스 2)를 삭제:
```xml
<Label Content="Vision IP" .../>
<TextBox x:Name="edit_VisionIP" Text="127.0.0.1" .../>
<Label Content="Port" .../>
<TextBox x:Name="edit_VisionPort" Text="3080" .../>
```
`combo_Camera`, `lbl_camStatus`는 그대로 둔다. 비워진 Grid.Column(0~3)은 Camera 라벨/콤보를 왼쪽으로 당기거나 그대로 비워둬도 무방(레이아웃만 영향, 동작 무관).

- [ ] **Step 2: 잔존 참조 확인**

Grep으로 `edit_VisionIP`, `edit_VisionPort`가 더 이상 참조되지 않는지 확인(A3에서 두 곳을 교체했으므로 0건이어야 함).
Run: `Grep edit_VisionIP / edit_VisionPort across *.cs`
Expected: .cs 참조 0건 (XAML에서도 제거됨)

- [ ] **Step 3: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

- [ ] **Step 4: 동작 보존 확인(수동)**

앱 실행 → 크레인 설정에서 Vision IP/Port 미변경 → 오토티칭 INIT 로그가 `Vision API BaseUrl=http://127.0.0.1:3080` 로 현행과 동일한지 확인.

---

## Module B — 오토티칭 최상위 탭(로그인 시 노출)

**핵심**: `grid_Main`의 예약 컬럼 `GridTest1`(Grid.Column="4", 현재 비어있는 `*` 컬럼)에 ToggleButton 추가. 기본 `Collapsed` → 빈 `*` 컬럼이라 현재 외관 불변. 관리자 로그인(PageCraneSet 기존 핸들러) 성공 시 노출.

### Task B1: MainWindow.xaml에 Btn_AutoTeaching 추가

**Files:**
- Modify: `MainWindow.xaml` (grid_Main, Btn_Main 스타일 413-438행을 복제)

- [ ] **Step 1: Grid.Column="4"에 ToggleButton 추가**

`grid_Main` 안, 기존 ToggleButton들과 같은 레벨에 추가(스타일은 Btn_Main 413-438행 ControlTemplate 그대로 복제):

```xml
<ToggleButton Content="{lx:Loc 오토티칭}" x:Name="Btn_AutoTeaching" Visibility="Collapsed" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" FontSize="18" FontWeight="Bold" Background="#02000000" BorderBrush="{x:Null}" BorderThickness="1,1,1,1" Grid.Column="4">
    <ToggleButton.Style>
        <Style TargetType="{x:Type ToggleButton}">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type ToggleButton}">
                        <Border x:Name="border" CornerRadius="15" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#FFB6D8FB" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Setter Property="Foreground" Value="#7FC8C8C8"/>
            <Style.Triggers>
                <Trigger Property="IsChecked" Value="True">
                    <Setter Property="Foreground" Value="Black"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </ToggleButton.Style>
</ToggleButton>
```

- [ ] **Step 2: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음 (`{lx:Loc 오토티칭}` 키는 PageCraneSet에서 이미 사용 중이라 존재)

### Task B2: MainWindow.xaml.cs 라우팅 + 노출 메서드

**Files:**
- Modify: `MainWindow.xaml.cs` — Mode_Click 바인딩(~306-310), Mode_Click(1938-1990), Page_Change 언체크 블록(~1815-1835)

- [ ] **Step 1: 생성자에서 Click 바인딩**

다른 모드 버튼들이 `Mode_Click`에 바인딩되는 위치(생성자 ~306-310행)에 추가:
```csharp
Btn_AutoTeaching.Click += Mode_Click;
```

- [ ] **Step 2: Mode_Click에 라우팅 분기 추가**

`Mode_Click` 내부, `else if (toggleButton == btn_test1)` 분기 **앞**에 추가:
```csharp
else if (toggleButton == Btn_AutoTeaching)
{
    Page_Change(cConstDefine.PAGE_AUTOTEACHING);
}
```

- [ ] **Step 3: Page_Change 언체크 블록에 추가**

`Page_Change`의 버튼 언체크 구간(`if (btn_test1.IsChecked == true) { btn_test1.IsChecked = false; }` 다음)에 추가:
```csharp
if (Btn_AutoTeaching.IsChecked == true)
{
    Btn_AutoTeaching.IsChecked = false;
}
```
그리고 `case cConstDefine.PAGE_AUTOTEACHING:` 안 `Frm_Page.Content = pageAutoTeaching;` 다음에 추가:
```csharp
Btn_AutoTeaching.IsChecked = true;
```

- [ ] **Step 4: 노출 public 메서드 추가**

MainWindow 클래스에 메서드 추가(아무 위치, public):
```csharp
public void RevealAutoTeachingTab()
{
    Dispatcher.Invoke(() => { Btn_AutoTeaching.Visibility = Visibility.Visible; });
}
```

- [ ] **Step 5: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

### Task B3: 로그인 성공 시 탭 노출 연결

**Files:**
- Modify: `PageCraneSet.xaml.cs:236-248` (Btn_AutoTeaching_Click)

- [ ] **Step 1: 로그인 성공 분기에 노출 호출 추가**

`Btn_AutoTeaching_Click`의 `if (result == true)` 블록, `pMain.Page_Change(...)` **앞**에 추가:
```csharp
pMain.RevealAutoTeachingTab();
```
(기존 `Page_Change(PAGE_AUTOTEACHING)`는 유지 — 로그인 시 기존처럼 바로 진입 + 이후엔 최상위 탭으로도 재진입 가능.)

- [ ] **Step 2: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

- [ ] **Step 3: 동작 확인(수동)**

앱 실행 → 최상위에 오토티칭 탭 안 보임 → 크레인설정 → 오토티칭 → 로그인 성공 → 오토티칭 탭 노출 + 페이지 진입. 자동/수동/반자동 탭 동작 불변.

---

## Module C — 결과 확인·반영 화면 (본체)

**핵심**: PageAutoTeaching에 `RESULT REVIEW` 탭 추가. 표(셀/기존/측정/편차/반영) + 이미지(원본/보정) + 반영/반려. 반영 = `BackupCellArrays`(자동백업) → in-memory `cellBay/cellLev` 갱신 → 기존 `WriteCellRangeAsync` 전체범위 쓰기(restore와 동일) → 0x95 ACK 시 `Rack.ini`에 적용 마커 + Pending=0 커밋.

### Task C1: TeachingResult에 이미지 경로 필드 추가·채우기

**Files:**
- Modify: `PageAutoTeaching.xaml.cs:41-59` (struct), `:902` 및 성공 반환부(`:973-985`)

- [ ] **Step 1: struct에 필드 추가**

`TeachingResult` struct(`public string CapturedFile;` 다음)에 추가:
```csharp
public string? RawPath;          // 비전 캡처 원본 경로 (localhost 디스크)
public string? CalibratedPath;   // 렌즈 보정 이미지 경로 (있을 때만)
```

- [ ] **Step 2: 캡처 응답에서 경로 보존(로컬 변수)**

`CaptureAndInferCellAsync`에서 `captureResp.Filename`을 로그하는 902행 근처, 이미 `captureResp`가 있는 시점에 로컬 변수로 잡아둔다(성공 반환부에서 사용):
```csharp
string capRaw = captureResp.RawPath;
string capCal = captureResp.CalibratedPath;
string capFile = captureResp.Filename;
```

- [ ] **Step 3: 성공 TeachingResult에 채우기**

`CaptureAndInferCellAsync` 성공 반환부(`return new TeachingResult { ... ZInferenceOk = true };`)에 필드 추가:
```csharp
            return new TeachingResult
            {
                Row = row, Bay = bay, Level = lev,
                BayPos = inferredBayPos, LevelPos = inferredLevPos,
                Success = true, HasCargo = hasCargo,
                CaptureOk = true, XInferenceOk = true, ZInferenceOk = true,
                CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal
            };
```

- [ ] **Step 4: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

### Task C2: 런 완료 시 셀별 티칭 레코드 저장 (TeachingState.ini)

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` — 신규 메서드 + 런 완료 지점 호출

- [ ] **Step 1: 저장 메서드 추가**

PageAutoTeaching에 메서드 추가(`CellKey` 재사용):
```csharp
private string TeachingStatePath =>
    AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\Teaching\\TeachingState.ini";

private void SaveTeachingState()
{
    try
    {
        string ini = TeachingStatePath;
        string dir = Path.GetDirectoryName(ini);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        foreach (var r in currentResults)
        {
            if (!r.Success) continue;
            string sec = CellKey(r.Row, r.Bay, r.Level);
            cIniAccess.Write(ini, sec, "BayPos", r.BayPos.ToString());
            cIniAccess.Write(ini, sec, "LevPos", r.LevelPos.ToString());
            cIniAccess.Write(ini, sec, "RawPath", r.RawPath ?? "");
            cIniAccess.Write(ini, sec, "CalibratedPath", r.CalibratedPath ?? "");
            cIniAccess.Write(ini, sec, "Timestamp", ts);
        }
        AddLog($"[STATE] 셀 티칭 레코드 저장 ({currentResults.Count(x => x.Success)}셀) → TeachingState.ini");
    }
    catch (Exception ex) { AddLog($"[STATE][WARN] 티칭 레코드 저장 실패 — {ex.Message}"); }
}
```

- [ ] **Step 2: 런 완료 직후 호출**

기존 자동 엑셀 저장(SaveToExcel) 호출부 근처(런 정상 완료 후)에 `SaveTeachingState();` 추가. (엑셀 저장과 같은 지점 — 둘 다 결과 영속화이므로 함께.)

- [ ] **Step 3: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

### Task C3: Rack.ini 적용 마커 read/write 헬퍼

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` — 신규 헬퍼

- [ ] **Step 1: 헬퍼 추가**

적용 마커는 0-based 인덱스 키(`cellBay[bay-1]`와 정합). 섹션 `RACK_APPLIED_BAY`/`RACK_APPLIED_LEV`:
```csharp
private string RackIniPath =>
    AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\RACK\\Rack.ini";

private void MarkApplied(int bay, int lev, string ts)
{
    cIniAccess.Write(RackIniPath, "RACK_APPLIED_BAY", "BAY" + (bay - 1), ts);
    cIniAccess.Write(RackIniPath, "RACK_APPLIED_LEV", "LEV" + (lev - 1), ts);
}

// "" 이면 미적용
private string AppliedTsBay(int bay) => cIniAccess.Read(RackIniPath, "RACK_APPLIED_BAY", "BAY" + (bay - 1), "nowrite");
private string AppliedTsLev(int lev) => cIniAccess.Read(RackIniPath, "RACK_APPLIED_LEV", "LEV" + (lev - 1), "nowrite");
```
(주: `cIniAccess.Read`의 defaultValue `"nowrite"`는 키가 없을 때 빈 문자열을 반환하고 ini에 쓰지 않음 — 읽기 전용 조회.)

- [ ] **Step 2: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

### Task C4: RESULT REVIEW 탭 UI 추가

**Files:**
- Modify: `PageAutoTeaching.xaml:73-180` (TabControl, MM POSITION MOVE 탭 닫는 `</TabItem>` 다음, `</TabControl>` 앞)

- [ ] **Step 1: 새 TabItem 추가**

```xml
<!-- ======================== TAB 3: 결과 확인·반영 ======================== -->
<TabItem Header="RESULT REVIEW">
    <Grid Margin="5">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="55*"/>
            <ColumnDefinition Width="45*"/>
        </Grid.ColumnDefinitions>
        <!-- 좌: 결과 표 -->
        <DataGrid x:Name="grid_Review" Grid.Column="0" Margin="0,0,3,0" AutoGenerateColumns="False"
                  IsReadOnly="False" Background="#19000000" Foreground="#CCADD8E6" BorderBrush="#FF555151"
                  HeadersVisibility="Column" CanUserAddRows="False" GridLinesVisibility="Horizontal"
                  SelectionChanged="Grid_Review_SelectionChanged">
            <DataGrid.Columns>
                <DataGridTextColumn Header="셀" Binding="{Binding Cell}" Width="*" IsReadOnly="True"/>
                <DataGridTextColumn Header="기존" Binding="{Binding Existing}" Width="0.7*" IsReadOnly="True"/>
                <DataGridTextColumn Header="측정" Binding="{Binding Measured}" Width="0.7*" IsReadOnly="True"/>
                <DataGridTextColumn Header="편차" Binding="{Binding Deviation}" Width="0.6*" IsReadOnly="True"/>
                <DataGridCheckBoxColumn Header="반영" Binding="{Binding Apply}" Width="0.5*"/>
            </DataGrid.Columns>
        </DataGrid>
        <!-- 우: 상세 + 이미지 + 버튼 -->
        <Grid Grid.Column="1" Margin="3,0,0,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <TabControl x:Name="tab_ReviewImg" Grid.Row="1" Background="#19000000" BorderBrush="#FF555151">
                <TabItem Header="원본"><Image x:Name="img_Raw" Stretch="Uniform"/></TabItem>
                <TabItem Header="보정"><Image x:Name="img_Cal" Stretch="Uniform"/></TabItem>
            </TabControl>
            <Label x:Name="lbl_ReviewDetail" Grid.Row="0" Content="선택 셀 없음" Foreground="#FFB8D8FC" FontWeight="Bold" FontSize="13"/>
            <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,4,0,0">
                <Button x:Name="btn_SelectNormal" Content="정상 셀 일괄 선택" Click="Btn_SelectNormal_Click" Margin="0,0,4,0" Padding="8,2" Background="#33000000" Foreground="#FFB8D8FC" BorderBrush="#FF555151"/>
            </StackPanel>
            <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,4,0,0">
                <Button x:Name="btn_RejectCell" Content="이 셀 반려" Click="Btn_RejectCell_Click" Margin="0,0,4,0" Padding="10,4" Background="#33000000" Foreground="#FFADADAD" BorderBrush="#FF555151"/>
                <Button x:Name="btn_ApplySelected" Content="선택 셀 반영" Click="Btn_ApplySelected_Click" Padding="10,4" Background="#FF2E7D32" Foreground="White"/>
            </StackPanel>
        </Grid>
    </Grid>
</TabItem>
```

- [ ] **Step 2: 빌드 검증** — 이벤트 핸들러는 C5에서 구현. 핸들러 미정의로 빌드 에러가 나므로, **이 단계는 C5와 함께 검증**(핸들러 stub을 C5 Step 1에서 먼저 추가). 순서상 C5 Step 1을 먼저 적용 후 빌드.

### Task C5: 행 모델 + 채우기 + 이미지 표시 + 반영/반려 핸들러

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` — 신규 행 모델 클래스 + 핸들러들 + 채우기 메서드

- [ ] **Step 1: 행 모델 + 핸들러 stub + 채우기 메서드 추가**

```csharp
public class ReviewRow
{
    public string Cell { get; set; }
    public int Existing { get; set; }
    public int Measured { get; set; }
    public int Deviation { get; set; }
    public bool Apply { get; set; }
    public int Bay { get; set; }
    public int Lev { get; set; }
    public string RawPath { get; set; }
    public string CalibratedPath { get; set; }
}

private readonly System.Collections.ObjectModel.ObservableCollection<ReviewRow> reviewRows = new();

public void PopulateReview()
{
    reviewRows.Clear();
    var info = gClass.str.SrmInfo[gClass.srmNum];
    foreach (var r in currentResults)
    {
        if (!r.Success) continue;
        int existBay = (info.cellBay != null && r.Bay >= 1 && r.Bay <= info.cellBay.Length) ? info.cellBay[r.Bay - 1] : 0;
        reviewRows.Add(new ReviewRow
        {
            Cell = CellKey(r.Row, r.Bay, r.Level),
            Existing = existBay,
            Measured = r.BayPos,
            Deviation = r.BayPos - existBay,
            Apply = false,
            Bay = r.Bay, Lev = r.Level,
            RawPath = r.RawPath, CalibratedPath = r.CalibratedPath
        });
    }
    // 편차 큰 순
    var sorted = reviewRows.OrderByDescending(x => Math.Abs(x.Deviation)).ToList();
    reviewRows.Clear();
    foreach (var x in sorted) reviewRows.Add(x);
    grid_Review.ItemsSource = reviewRows;
}

private static System.Windows.Media.Imaging.BitmapImage LoadImageNoLock(string path)
{
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
    var bmp = new System.Windows.Media.Imaging.BitmapImage();
    bmp.BeginInit();
    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
    bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
    bmp.UriSource = new Uri(path);
    bmp.EndInit();
    bmp.Freeze();
    return bmp;
}

private void Grid_Review_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
{
    if (grid_Review.SelectedItem is ReviewRow row)
    {
        lbl_ReviewDetail.Content = $"{row.Cell}  기존 {row.Existing} → 측정 {row.Measured} ({(row.Deviation >= 0 ? "+" : "")}{row.Deviation}mm)";
        img_Raw.Source = LoadImageNoLock(row.RawPath);
        img_Cal.Source = LoadImageNoLock(row.CalibratedPath);
    }
}

private void Btn_SelectNormal_Click(object sender, RoutedEventArgs e)
{
    foreach (var r in reviewRows) r.Apply = true;
    grid_Review.Items.Refresh();
}

private void Btn_RejectCell_Click(object sender, RoutedEventArgs e)
{
    if (grid_Review.SelectedItem is ReviewRow row) { row.Apply = false; grid_Review.Items.Refresh(); }
}

private async void Btn_ApplySelected_Click(object sender, RoutedEventArgs e)
{
    await ApplySelectedCellsAsync();
}
```

- [ ] **Step 2: PageInit에서 채우기 + 빌드 검증**

`PageInit()`(Page_Change가 PAGE_AUTOTEACHING 진입 시 호출) 끝에 `PopulateReview();` 추가(currentResults가 비어 있으면 빈 표).
Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음 (C4 XAML + 이 핸들러로 컴파일 OK)

- [ ] **Step 3: 동작 확인(수동, 크레인 불요)**

오토티칭 1열 일부 실행 → RESULT REVIEW 탭 → 표에 셀/기존/측정/편차 표시, 행 선택 시 원본/보정 이미지 표시(localhost 캡처 파일), 정상 일괄선택/반려 체크 토글.

### Task C6: 반영(0x95 쓰기) — 백업·갱신·적용마커 ⚠️ 실기 게이트

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` — `ApplySelectedCellsAsync` 구현(기존 `BackupCellArrays`/`WriteCellRangeAsync` 재사용)

- [ ] **Step 1: ApplySelectedCellsAsync 구현**

```csharp
private async Task ApplySelectedCellsAsync()
{
    var picked = reviewRows.Where(r => r.Apply).ToList();
    if (picked.Count == 0)
    {
        MessageBox.Show("반영할 셀을 선택하세요.", "결과 반영", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    var confirm = MessageBox.Show($"{picked.Count}개 셀을 SRM에 반영합니다.\n반영 전 기존값은 자동 백업됩니다.\n\n진행할까요?",
        "결과 반영", MessageBoxButton.YesNo, MessageBoxImage.Warning);
    if (confirm != MessageBoxResult.Yes) return;

    // 1) 자동 백업 (기존 메커니즘, Pending=1)
    BackupCellArrays();

    // 2) in-memory 갱신 (선택 셀의 bay/lev 인덱스)
    var info = gClass.str.SrmInfo[gClass.srmNum];
    foreach (var r in picked)
    {
        if (info.cellBay != null && r.Bay >= 1 && r.Bay <= info.cellBay.Length) info.cellBay[r.Bay - 1] = r.Measured;
        // Lev 측정값: reviewRows는 Bay 기준 측정만 담았으므로, currentResults에서 LevelPos 조회
        var src = currentResults.FirstOrDefault(x => x.Success && x.Bay == r.Bay && x.Level == r.Lev);
        if (info.cellLev != null && r.Lev >= 1 && r.Lev <= info.cellLev.Length) info.cellLev[r.Lev - 1] = src.LevelPos;
    }
    gClass.str.SrmInfo[gClass.srmNum] = info;

    // 3) 전체범위 0x95 쓰기 (RestoreCellArraysAsync와 동일한 호출 형태)
    bool bayOk = await WriteCellRangeAsync(1, 0, info.cellBay.Length - 1, info.cellBay);
    bool levOk = await WriteCellRangeAsync(2, 0, info.cellLev.Length - 1, info.cellLev);

    if (bayOk && levOk)
    {
        // 4) 적용 마커 + 백업 커밋 (Pending=0)
        string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        foreach (var r in picked) MarkApplied(r.Bay, r.Lev, ts);
        cIniAccess.Write(MmBackupIniPath, "MM_BACKUP", "Pending", "0");
        AddLog($"[APPLY] {picked.Count}셀 반영 완료 (0x95 ACK)");
        MessageBox.Show($"{picked.Count}개 셀 반영 완료.", "결과 반영", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    else
    {
        // ACK 실패 → 적용 마커 미기록. 백업(Pending=1)은 그대로 남아 다음 진입 시 자동 롤백.
        AddLog($"[APPLY][FAIL] 0x95 ACK 실패 (bay={bayOk} lev={levOk}) — 미반영, 백업 보존(자동 롤백 대상)");
        MessageBox.Show("0x95 반영 실패. 다음 페이지 진입 시 자동 복구됩니다.", "결과 반영", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
```

- [ ] **Step 2: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

- [ ] **Step 3: ⚠️ 실기 검증(크레인 필요 — 게이트)**

크레인 연결 상태에서: 셀 선택 → 반영 → 백업 생성 확인 → 0x95 ACK → `Rack.ini`에 `RACK_APPLIED_BAY/LEV` 마커 + `RACK_BAY/LEV` 갱신 + `MM_BACKUP Pending=0` 확인. ACK 실패 시 미반영·Pending=1 유지·다음 진입 자동 롤백 확인. **이 단계는 하드웨어 없이는 검증 불가 — 빌드까지만 선행, 실기 라운드에서 확인.**

---

## Module D — 반자동 카메라+적용여부 스트립

**핵심(안전 우선)**: 기존 반자동 그리드를 **재인덱싱하지 않는다.** 기존 루트 `<Grid>...</Grid>` 전체를 신규 외곽 2행 Grid의 Row=1에 통째로 넣고(Row=0=스트립), 내부 Grid.Row는 한 줄도 안 건드린다 → 기존 레이아웃·동작 100% 보존. 의심 XAML(`Grid Row="3"`, 중첩 `Grid.Row=9/10`)도 그대로 유지(렌더 변화 0).

### Task D1: 반자동 루트를 외곽 그리드로 wrap

**Files:**
- Modify: `PageSemiAuto.xaml:10` (기존 루트 `<Grid>` → 외곽 Grid로 감싸기)

- [ ] **Step 1: 외곽 Grid 삽입**

기존 `<Grid>` (10행, RowDefinitions 20*/50*/10* 가진 루트) **전체**를 아래 구조로 감싼다. 기존 루트 Grid는 그대로 두고 `Grid.Row="1"`만 부여:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="150"/>   <!-- 신규 카메라+적용여부 스트립 -->
        <RowDefinition Height="*"/>      <!-- 기존 반자동 그리드(무변경) -->
    </Grid.RowDefinitions>

    <!-- 신규 스트립 (D2에서 내용 채움) -->
    <Border x:Name="bdr_SemiCam" Grid.Row="0" Background="#CC2B2D33" BorderBrush="#FFB8D8FC" BorderThickness="1" CornerRadius="6" Margin="10,6,10,3">
        <!-- D2 내용 -->
    </Border>

    <!-- ↓↓↓ 기존 루트 Grid (RowDefinitions 20*/50*/10*) 를 여기에 그대로, Grid.Row="1" 만 추가 ↓↓↓ -->
    <Grid Grid.Row="1">
        <Grid.RowDefinitions>
            <RowDefinition Height="20*"/>
            <RowDefinition Height="50*"/>
            <RowDefinition Height="10*"/>
        </Grid.RowDefinitions>
        <!-- ... 기존 자식 전부 그대로 ... -->
    </Grid>
</Grid>
```
(주: 기존 자식 XAML은 한 줄도 수정하지 않는다. 외곽 `<Grid>`와 스트립 `<Border>`만 새로 감싼다.)

- [ ] **Step 2: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

- [ ] **Step 3: 동작 보존 확인(수동)**

반자동 페이지 진입 → 기존 포크 입력/작업선택/전송·초기화가 **이전과 동일하게** 렌더·동작하는지 확인(스트립만 위에 추가됨). 의심 XAML 영역 포함 현행 렌더 그대로인지 비교.

### Task D2: 스트립 내용 + 목적 셀 조회

**Files:**
- Modify: `PageSemiAuto.xaml` (bdr_SemiCam 내용), `PageSemiAuto.xaml.cs` (조회 로직)

- [ ] **Step 1: 스트립 내용 XAML**

`bdr_SemiCam` 안에 채움:
```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/><ColumnDefinition Width="*"/><ColumnDefinition Width="1.4*"/>
    </Grid.ColumnDefinitions>
    <Image x:Name="img_SemiRaw" Grid.Column="0" Stretch="Uniform" Margin="4"/>
    <Image x:Name="img_SemiCal" Grid.Column="1" Stretch="Uniform" Margin="4"/>
    <StackPanel Grid.Column="2" VerticalAlignment="Center" Margin="8,0">
        <Label x:Name="lbl_SemiCell" Content="목적 셀 -" Foreground="#FFB8D8FC" FontWeight="Bold" FontSize="14"/>
        <Label x:Name="lbl_SemiDev" Content="편차 -" Foreground="#FFADADAD" FontSize="13"/>
        <Border x:Name="bdr_SemiApplied" Background="#33000000" CornerRadius="10" Padding="8,2" HorizontalAlignment="Left" Margin="0,4,0,0">
            <Label x:Name="lbl_SemiApplied" Content="데이터 없음" Foreground="#FFADADAD" FontWeight="Bold" FontSize="12"/>
        </Border>
    </StackPanel>
</Grid>
```

- [ ] **Step 2: 조회 메서드(.cs)**

`PageSemiAuto.xaml.cs`에 추가(이미지 무락 로더는 별도 헬퍼로 동일 구현):
```csharp
private void RefreshSemiCamStrip()
{
    // 활성 포크의 목적(To) Bay/Lev (Fork1 우선; 미사용 시 Fork2)
    string bayTxt = gClass.str.SrmInfo[gClass.srmNum].bUse_fork1 ? Edit_Fk1ToBay.Text : Edit_Fk2ToBay.Text;
    string levTxt = gClass.str.SrmInfo[gClass.srmNum].bUse_fork1 ? Edit_Fk1ToLev.Text : Edit_Fk2ToLev.Text;
    if (!int.TryParse(bayTxt, out int bay) || !int.TryParse(levTxt, out int lev) || bay < 1 || lev < 1)
    {
        lbl_SemiCell.Content = "목적 셀 -"; lbl_SemiDev.Content = "편차 -";
        lbl_SemiApplied.Content = "데이터 없음"; lbl_SemiApplied.Foreground = Brushes.Gray;
        img_SemiRaw.Source = null; img_SemiCal.Source = null;
        return;
    }
    string baseDir = AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum;
    string stateIni = baseDir + "\\Teaching\\TeachingState.ini";
    string rackIni = baseDir + "\\RACK\\Rack.ini";
    string cellKey = $"R1-B{bay:D3}-L{lev:D2}";   // 반자동은 1열 기준 (row=1)

    string measured = cIniAccess.Read(stateIni, cellKey, "BayPos", "nowrite");
    if (string.IsNullOrEmpty(measured))
    {
        lbl_SemiCell.Content = $"목적 셀 1-{bay}-{lev}"; lbl_SemiDev.Content = "편차 -";
        lbl_SemiApplied.Content = "데이터 없음"; lbl_SemiApplied.Foreground = Brushes.Gray;
        img_SemiRaw.Source = null; img_SemiCal.Source = null;
        return;
    }
    string raw = cIniAccess.Read(stateIni, cellKey, "RawPath", "nowrite");
    string cal = cIniAccess.Read(stateIni, cellKey, "CalibratedPath", "nowrite");
    img_SemiRaw.Source = LoadSemiImage(raw);
    img_SemiCal.Source = LoadSemiImage(cal);

    int existBay = (gClass.str.SrmInfo[gClass.srmNum].cellBay != null && bay <= gClass.str.SrmInfo[gClass.srmNum].cellBay.Length)
        ? gClass.str.SrmInfo[gClass.srmNum].cellBay[bay - 1] : 0;
    int dev = int.TryParse(measured, out int m) ? m - existBay : 0;
    lbl_SemiCell.Content = $"목적 셀 1-{bay}-{lev}";
    lbl_SemiDev.Content = $"편차 {(dev >= 0 ? "+" : "")}{dev}mm";

    bool appliedBay = !string.IsNullOrEmpty(cIniAccess.Read(rackIni, "RACK_APPLIED_BAY", "BAY" + (bay - 1), "nowrite"));
    bool appliedLev = !string.IsNullOrEmpty(cIniAccess.Read(rackIni, "RACK_APPLIED_LEV", "LEV" + (lev - 1), "nowrite"));
    if (appliedBay && appliedLev) { lbl_SemiApplied.Content = "적용됨 ✓"; lbl_SemiApplied.Foreground = Brushes.LightGreen; }
    else { lbl_SemiApplied.Content = "미적용"; lbl_SemiApplied.Foreground = Brushes.Orange; }
}

private static System.Windows.Media.Imaging.BitmapImage LoadSemiImage(string path)
{
    if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
    var bmp = new System.Windows.Media.Imaging.BitmapImage();
    bmp.BeginInit();
    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
    bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
    bmp.UriSource = new Uri(path);
    bmp.EndInit(); bmp.Freeze();
    return bmp;
}
```

- [ ] **Step 3: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

### Task D3: 목적 셀 입력 시 스트립 갱신 연결

**Files:**
- Modify: `PageSemiAuto.xaml.cs` — `Page_Init()` 끝 + 목적지 선택(`Btn_FromTo_Select`) 후 호출

- [ ] **Step 1: 갱신 호출 추가**

`Page_Init()` 끝에 `RefreshSemiCamStrip();` 추가(페이지 진입 시 1회). 그리고 목적지 Bay/Lev가 확정되는 지점(NumberPad 입력 반영 후, 예: `Btn_FromTo_Select` 처리 완료 콜백 또는 텍스트 갱신 직후)에 `RefreshSemiCamStrip();` 추가.

- [ ] **Step 2: 빌드 검증**

Run: `$out = dotnet build gcp_Wpf.csproj -c Debug -nologo; $out | Select-String ": error "`
Expected: `: error` 없음

- [ ] **Step 3: 동작 확인(수동)**

반자동에서 목적 셀(베이/단) 입력 → 스트립에 해당 셀의 원본/보정 이미지·편차·적용여부 표시. 티칭 이력 없는 셀은 "데이터 없음". 기존 반자동 작업 동작 불변.

---

## Self-Review (작성자 체크)

**1. Spec 커버리지:**
- 스펙 §5 Module A → Task A1-A4 ✅ / Module B → B1-B3 ✅ / Module C → C1-C6 ✅ / Module D → D1-D3 ✅
- 스펙 §4 결정사항: 이미지=로컬로드(C1,C5,D2) ✅ / Mode1 원본+보정(C4,D2) ✅ / 로그인 시 탭노출(B1-B3) ✅ / 비전설정 이전(A) ✅ / 적용상태 0x95 ACK 시 Rack.ini(C3,C6) ✅ / 반영전 백업 재사용(C6) ✅
- 스펙 §9 선행항목: raw_path 절대/상대(→ C5 Step3 수동확인 시 경로 실값 확인) / 반자동 의심XAML(→ D1 Step3 렌더 검증, wrap으로 회피) / 저장 스키마(→ C2/C3에서 확정) ✅

**2. 플레이스홀더 스캔:** TODO/TBD 없음. 모든 코드 스텝에 실제 코드. (D3 Step1의 "목적지 확정 지점"은 기존 NumberPad 콜백 위치라 실행 시 정확 지점 확정 — 구현자 1줄 삽입.)

**3. 타입 일관성:** `WriteCellRangeAsync(int,int,int,int[])` (restore와 동일 시그니처) ✅ / `cIniAccess.Read(path,section,key,default)`·`Write(path,section,key,value)` ✅ / `gClass.str.SrmInfo[gClass.srmNum].cellBay/cellLev` (0-based, [bay-1]) ✅ / `CellKey`·`R{row}-B{bay:D3}-L{lev:D2}` 표기 C2/C5/D2 일치 ✅ / `MmBackupIniPath`(기존)·`MarkApplied`·`TeachingStatePath`·`RackIniPath` 일치 ✅

**주의(구현자):**
- `info`는 struct 값복사일 수 있음 — C6에서 `info.cellBay[i] = ...` 후 `gClass.str.SrmInfo[gClass.srmNum] = info;` 재대입 포함(배열은 참조라 사실상 즉시 반영되나 명시적 재대입으로 안전).
- C6 Step3·실기 게이트는 크레인 없이는 미검증. A/B/C(표시까지)/D는 빌드+수동으로 완결 가능.
- `lev` 측정값은 reviewRows에 Bay만 담으므로 C6에서 currentResults로 LevelPos 조회(struct FirstOrDefault — 매칭 없으면 LevelPos=0이므로 Success 매칭 필수, 코드 반영됨).
