# LicenseGeneratorTool 솔루션 분리 가이드

## 변경 사항

`LicenseGeneratorTool`이 `gcp_Wpf.sln`에서 분리되어 별도의 솔루션(`LicenseGeneratorTool.sln`)으로 독립되었습니다.

## 목적

- **라이센스 파일 호환성 보장**: `LicenseGeneratorTool`을 업데이트해도 기존에 생성한 라이센스 파일(`license.lic`)이 `gcp_Wpf`에서 정상적으로 동작합니다.
- **독립적인 빌드**: `LicenseGeneratorTool`을 `gcp_Wpf`와 별도로 빌드하고 배포할 수 있습니다.
- **보안 강화**: 관리자 전용 도구를 메인 애플리케이션과 분리하여 보안을 강화합니다.

## 구조

### 파일 공유 방식

각 프로젝트는 역할에 맞는 파일만 포함합니다:

**gcp_Wpf (검증용):**
- `HardwareIdGenerator.cs` - 하드웨어 ID 생성
- `LicenseData.cs` - 라이센스 데이터 구조
- `LicenseManager.cs` - 라이센스 검증
- `WindowLicenseGenerator.xaml` (선택사항) - UI

**LicenseGeneratorTool (생성용):**
- `HardwareIdGenerator.cs` - 하드웨어 ID 생성 (링크)
- `KeyGenerator.cs` - RSA 키 생성 (링크)
- `LicenseData.cs` - 라이센스 데이터 구조 (링크)
- `LicenseGenerator.cs` - 라이센스 생성 (링크)

이렇게 하면:
- ✅ 각 프로젝트가 필요한 파일만 포함
- ✅ 코드 중복 없음 (링크 사용)
- ✅ 동일한 네임스페이스(`gcp_Wpf.License`) 사용
- ✅ 라이센스 파일 형식 완전 호환
- ✅ 보안 강화 (gcp_Wpf에 생성 기능 없음)

### 프로젝트 파일 구조

```
gcp_type2/
├── gcp_Wpf.sln                    # 메인 애플리케이션 솔루션
├── gcp_Wpf.csproj
├── License/                        # 공유 라이센스 클래스
│   ├── HardwareIdGenerator.cs
│   ├── KeyGenerator.cs
│   ├── LicenseData.cs
│   ├── LicenseGenerator.cs
│   └── LicenseManager.cs
│
└── LicenseGeneratorTool/
    ├── LicenseGeneratorTool.sln   # 별도 솔루션 (새로 생성)
    ├── LicenseGeneratorTool.csproj # gcp_Wpf.csproj 참조 제거됨
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    └── App.xaml
```

## 빌드 방법

### LicenseGeneratorTool 빌드

```bash
# LicenseGeneratorTool 폴더로 이동
cd LicenseGeneratorTool

# 솔루션 빌드
dotnet build LicenseGeneratorTool.sln

# 또는 Visual Studio에서
# LicenseGeneratorTool.sln 열기 → 빌드
```

### gcp_Wpf 빌드

```bash
# 루트 폴더에서
dotnet build gcp_Wpf.sln

# 또는 Visual Studio에서
# gcp_Wpf.sln 열기 → 빌드
```

## 중요 사항

### 1. 라이센스 파일 호환성

`LicenseGeneratorTool`로 생성한 라이센스 파일은 `gcp_Wpf`에서 정상적으로 검증됩니다:

- ✅ 동일한 `HardwareIdGenerator` 사용
- ✅ 동일한 `LicenseGenerator` 사용
- ✅ 동일한 암호화 알고리즘 사용
- ✅ 동일한 네임스페이스 사용

### 2. Keys 폴더 위치

라이센스 생성 시 `Keys` 폴더는 **실행 파일과 같은 디렉토리**에 생성됩니다:

```
LicenseGeneratorTool 실행 위치/
├── LicenseGeneratorTool.exe
└── Keys/
    ├── public_key.xml
    └── private_key.xml  (라이센스 생성 후 삭제됨)
```

생성된 `license.lic` 파일과 `Keys/public_key.xml`을 `gcp_Wpf` 설치 폴더로 복사해야 합니다.

### 3. 폴더 이동 시 주의사항

`LicenseGeneratorTool` 폴더를 다른 위치로 이동할 경우:

- `LicenseGeneratorTool.csproj`의 링크 경로가 상대 경로(`..\License\`)를 사용하므로
- `gcp_Wpf` 프로젝트의 `License` 폴더와의 상대 위치를 유지해야 합니다

예를 들어:
```
프로젝트 루트/
├── gcp_Wpf/
│   └── License/
└── LicenseGeneratorTool/
    └── (License 폴더는 ..\gcp_Wpf\License\ 를 참조)
```

또는 링크 경로를 절대 경로로 변경하거나, `License` 폴더의 파일들을 `LicenseGeneratorTool` 프로젝트에 직접 복사할 수도 있습니다.

## 업데이트 시 주의사항

### gcp_Wpf 업데이트 시

- `LicenseGeneratorTool.exe`는 **배포하지 않습니다**
- `Keys` 폴더와 `license.lic` 파일은 **기존 것을 유지**합니다
- `gcp_Wpf.exe`와 관련 DLL만 업데이트합니다

### LicenseGeneratorTool 업데이트 시

- `LicenseGeneratorTool`만 별도로 빌드하고 배포합니다
- 기존 `Keys` 폴더의 키를 사용하면 기존 라이센스 파일과 호환됩니다
- 새로운 키를 생성하면 기존 라이센스 파일이 무효화됩니다

## 문제 해결

### 빌드 오류: "License 클래스를 찾을 수 없습니다"

- `LicenseGeneratorTool.csproj`의 링크 경로(`..\License\`)가 올바른지 확인
- `gcp_Wpf` 프로젝트의 `License` 폴더가 존재하는지 확인

### 라이센스 검증 실패

- `Keys/public_key.xml`이 `gcp_Wpf` 설치 폴더에 있는지 확인
- `license.lic` 파일이 올바른 하드웨어 ID로 생성되었는지 확인
- `LicenseGeneratorTool`과 `gcp_Wpf`가 동일한 `License` 클래스를 사용하는지 확인

