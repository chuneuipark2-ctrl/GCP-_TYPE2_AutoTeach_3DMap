# 라이선스 생성기 도구 (LicenseGeneratorTool)

## 개요
이 프로젝트는 **관리자 전용** 라이선스 생성 도구입니다. 
**절대 배포하지 마세요!** 

## 특징

1. **별도 솔루션**: 메인 프로그램(`gcp_Wpf`)과 분리된 독립 솔루션
2. **통합 기능**: 키 생성, 하드웨어 ID 확인, 라이선스 생성 모두 포함
3. **라이센스 호환성**: 생성한 라이센스 파일이 `gcp_Wpf`에서 정상 동작

## 사용 방법

### 1. 빌드
```bash
# Visual Studio에서 빌드
# 또는 명령줄에서
dotnet build LicenseGeneratorTool/LicenseGeneratorTool.csproj -c Release
```

### 2. 배포 (관리자에게만)
- 빌드된 `LicenseGeneratorTool.exe`를 관리자에게만 제공
- **절대 일반 사용자나 배포 패키지에 포함하지 마세요!**

### 3. 실행
1. `LicenseGeneratorTool.exe` 실행
2. RSA 키 생성 (최초 1회)
3. 하드웨어 ID 확인
4. 라이선스 정보 입력
5. 라이선스 파일 생성
6. 생성된 `license.lic` 파일과 `Keys/public_key.xml`을 `gcp_Wpf` 설치 폴더로 복사

## 보안

### gcp_Wpf의 자동 삭제 기능
- `gcp_Wpf` 실행 시 `LicenseGeneratorTool.exe`가 있으면 자동으로 삭제됩니다
- 이는 보안을 위한 조치로, 일반 사용자가 라이센스를 생성하는 것을 방지합니다

## 파일 구조

```
LicenseGeneratorTool/
├── App.xaml                    # 애플리케이션 정의
├── App.xaml.cs                 # 애플리케이션 로직
├── MainWindow.xaml             # 메인 UI
├── MainWindow.xaml.cs          # UI 로직
├── LicenseGeneratorTool.csproj # 프로젝트 파일
├── LicenseGeneratorTool.sln    # 별도 솔루션 파일
└── README.md                   # 이 파일
```

### 공유 파일 (링크)
- `../License/HardwareIdGenerator.cs`
- `../License/KeyGenerator.cs`
- `../License/LicenseData.cs`
- `../License/LicenseGenerator.cs`

## 주의사항

1. **절대 배포 금지**: 이 도구는 관리자 전용입니다.
2. **gcp_Wpf 자동 삭제**: `gcp_Wpf` 실행 시 `LicenseGeneratorTool.exe`가 자동으로 삭제됩니다.
3. **개인키 보안**: 라이선스 생성 후 개인키가 자동으로 삭제됩니다 (기본 설정).
4. **키 백업**: 키를 다시 생성하기 전에 반드시 백업하세요.
5. **업데이트 시**: 업데이트 패키지에 `LicenseGeneratorTool.exe`를 포함시킬 수 있습니다. `gcp_Wpf`가 실행되면 자동으로 삭제됩니다.

## 빌드 및 배포

### 개발 환경에서 빌드
```bash
dotnet build LicenseGeneratorTool/LicenseGeneratorTool.csproj -c Release
```

### 출력 위치
- `LicenseGeneratorTool/bin/Release/net6.0-windows/LicenseGeneratorTool.exe`

### 배포 시
1. Release 빌드 생성
2. 업데이트 패키지에 `LicenseGeneratorTool.exe` 포함 가능
3. 관리자가 실행하여 라이선스 생성
4. `gcp_Wpf` 실행 시 자동으로 삭제됨

## 문제 해결

### gcp_Wpf가 LicenseGeneratorTool을 삭제하지 않는 경우
- `gcp_Wpf`가 실행 중일 때는 삭제가 지연될 수 있습니다.
- 다음 실행 시 삭제됩니다.

### 키 생성 오류
- `System.Management` 패키지가 설치되어 있는지 확인하세요.
- 관리자 권한으로 실행해보세요.

