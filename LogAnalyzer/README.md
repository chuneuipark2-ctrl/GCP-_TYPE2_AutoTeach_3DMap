# SRM 로그 분석기 (LogAnalyzer)

`gcp_Wpf`의 `commClass/udpClientClass.cs`에서 저장하는 SRM 로그 형식을 파싱하여 **TX(SEND)** 와 **RX(RECV)** 를 쌍으로 보여주는 WPF 프로그램입니다.

## 로그 형식 (udpClientClass 기준)

- 한 줄: `HH:mm:ss:fff ` + 메시지
- TX: `SEND: XXXX / [HEX데이터]`  (예: `SEND: 0030 / 16161616...`)
- RX: `RECV: CMD(XXXX) / [HEX데이터]`  (예: `RECV: CMD(8030) / 16161616...`)

## 기능

1. **파일 열기**: `.log` / `.txt` 파일을 선택해 전체 로그 로드
2. **붙여넣기 분석**: 텍스트 상자에 로그 일부를 붙여넣고 [붙여넣기 분석] 클릭
3. **TX-RX 쌍 표시**: 각 TX에 대해 그 다음에 오는 RX를 한 쌍으로 표시 (왼쪽 TX, 오른쪽 RX)
4. **이전/다음**: [◀ 이전 (PREV)] / [다음 (NEXT) ▶] 로 긴 로그에서 다음/이전 시간의 TX-RX 쌍으로 이동

## 실행 방법

- Visual Studio에서 `LogAnalyzer.sln` 열기 → F5 실행
- 또는 `LogAnalyzer\bin\Debug\net6.0-windows\LogAnalyzer.exe` 실행

## 폴더 분리

이 솔루션은 나중에 별도 경로로 옮겨 사용할 수 있도록 의존성 없이 구성되어 있습니다.  
`LogAnalyzer` 폴더 전체를 원하는 위치로 복사한 뒤 해당 경로에서 솔루션을 열면 됩니다.
