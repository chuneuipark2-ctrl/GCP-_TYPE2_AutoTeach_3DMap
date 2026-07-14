# Host 패킷 분석기 (HostAnalyzer)

tcpServerClass(Host/WCS 통신) 로그를 분석하는 WPF 도구입니다. UDP 로그 분석기(LogAnalyzer)와 동일한 방식으로 동작합니다.

## 로그 형식 (tcpServerClass SaveLogFile)

- **WCS (수신)**: `WCS: (포트번호)hex...` — 호스트(WCS)가 GCP로 보낸 요청
- **GCP (송신)**: `GCP: hex...` — GCP가 호스트로 보낸 응답

## 패킷 구조 (tcpServerClass Rx_DataCheck / Tx_SetData)

- **SYN**: 0x16 x 4
- **EQID**: 1바이트 (장비 ID)
- **ReqType**: 1바이트 (RX: 0/1, TX: 0x80/0x81)
- **Data**: Word 배열 (WCSFROM 100워드 또는 WCSTO 200/100워드)
- **CRC**: 2바이트 BigEndian (CRC-16 CCITT)
- **ETX**: 0xF5

## 사용 방법

1. Host 로그 파일 열기 또는 텍스트 붙여넣기
2. "붙여넣기 분석"으로 WCS/GCP 쌍 자동 구성
3. 이전/다음으로 쌍 이동하며 WCS 수신·GCP 송신 상세(파싱·해석) 확인

## 프로젝트 구조

- **Models**: LogEntry, HostParsedPacket, TxRxPair
- **Services**: HostLogParser, HostPacketParser, HostDataInterpreter, Crc16Helper
- **MainWindow**: 파일 열기/붙여넣기, TX·RX 상세 표시, 이전/다음 네비게이션

빌드: `dotnet build`  
실행: `dotnet run` 또는 HostAnalyzer.exe
