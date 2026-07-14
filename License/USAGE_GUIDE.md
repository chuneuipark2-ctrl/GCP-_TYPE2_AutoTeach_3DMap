# 라이선싱 시스템 사용 가이드 (간단 버전)

## 빠른 시작

### 1단계: RSA 키 생성 (최초 1회만)

프로그램 어딘가에 임시로 다음 코드를 추가하여 키를 생성합니다:

```csharp
// 예: 버튼 클릭 이벤트나 임시 메서드
KeyGenerator.GenerateAndSaveKeys();
```

실행하면 `Keys` 폴더에 다음 파일이 생성됩니다:
- `public_key.xml` - 프로그램에 포함하여 배포
- `private_key.xml` - 관리자만 보관 (절대 배포 금지!)

**⚠️ 경고**: 
- 기존 키가 있으면 경고 메시지가 표시됩니다.
- 새로운 키를 생성하면 **기존에 생성된 모든 라이선스가 무효화**됩니다.
- 키를 다시 생성하기 전에 반드시 기존 키를 백업하세요.
- 프로그램은 자동으로 백업 옵션을 제공합니다.

### 2단계: 라이선스 생성

#### 방법 1: UI 사용
```csharp
WindowLicenseGenerator window = new WindowLicenseGenerator();
window.ShowDialog();
```
- UI에서 "라이선스 생성 후 개인키 파일 자동 삭제" 옵션이 기본으로 체크되어 있습니다.
- 라이선스 생성 후 자동으로 `private_key.xml`이 삭제됩니다.

#### 방법 2: 코드에서 직접
```csharp
// 1. 고객의 하드웨어 ID 확인
string hardwareId = LicenseManager.GetCurrentHardwareId();
// 또는 고객에게 물어보기

// 2. 라이선스 생성 (개인키 자동 삭제)
LicenseGenerator.GenerateLicenseFile(
    hardwareId: "고객의_하드웨어_ID",
    expiryDate: new DateTime(2025, 12, 31), // 또는 null (무제한)
    customerName: "고객명",
    deletePrivateKeyAfterGeneration: true  // 기본값: true (보안 권장)
);
```

**중요**: 라이선스 생성 후 `private_key.xml`이 자동으로 삭제됩니다. 
- 이는 보안을 위한 조치입니다.
- 개인키가 유출되면 누구나 라이선스를 생성할 수 있으므로, 라이선스 생성 후 반드시 삭제하거나 안전한 곳에 백업하세요.

### 3단계: 라이선스 배포

생성된 `license.lic` 파일을 고객의 프로그램 설치 폴더에 복사합니다.

```
C:\Program Files\YourApp\
├── YourApp.exe
├── license.lic          ← 여기에 복사
└── Keys\
    └── public_key.xml   ← 프로그램과 함께 배포
```

## 파일 저장 위치 요약

### 프로그램 실행 시 읽는 파일:
1. **라이선스 파일**: `[프로그램 실행 경로]/license.lic`
2. **공개키 파일**: `[프로그램 실행 경로]/Keys/public_key.xml`

### 관리자가 보관하는 파일:
1. **개인키 파일**: `Keys/private_key.xml` (절대 배포 금지!)

## 작동 원리

1. **프로그램 시작** → `App.xaml.cs`에서 자동으로 라이선스 검증
2. **하드웨어 ID 확인** → 현재 PC의 고유 정보 수집
3. **라이선스 파일 검증**:
   - 파일 존재 확인
   - RSA 서명 검증 (위조 방지)
   - AES 복호화 (하드웨어 ID 기반 키 사용)
   - 하드웨어 ID 일치 확인
   - 만료일 확인
4. **검증 실패** → 프로그램 종료
5. **검증 성공** → 정상 실행

## 문제 해결

### "라이선스 파일을 찾을 수 없습니다"
→ `license.lic` 파일이 프로그램 실행 파일과 같은 폴더에 있는지 확인

### "라이선스가 이 컴퓨터에 등록되지 않았습니다"
→ 하드웨어가 변경되었거나 잘못된 하드웨어 ID로 라이선스를 생성했을 수 있습니다.
→ 새로운 하드웨어 ID로 라이선스를 재생성해야 합니다.

### "공개키 파일을 찾을 수 없습니다"
→ `Keys/public_key.xml` 파일이 프로그램과 함께 배포되었는지 확인

## 주의사항

- 개인키(`private_key.xml`)는 절대 배포하지 마세요!
- 공개키(`public_key.xml`)는 프로그램과 함께 배포해야 합니다.
- 하드웨어 변경 시 새로운 라이선스가 필요합니다.

