# License 폴더 파일 구성 가이드

## 파일 역할 분류

### 공유 파일 (두 프로젝트 모두 사용)

#### HardwareIdGenerator.cs
- **용도**: 하드웨어 고유 ID 생성
- **사용처**: 
  - `gcp_Wpf`: 라이센스 검증 시 현재 하드웨어 ID 확인
  - `LicenseGeneratorTool`: 라이센스 생성 시 하드웨어 ID 확인

#### LicenseData.cs
- **용도**: 라이센스 데이터 구조 정의
- **사용처**: 
  - `gcp_Wpf`: 라이센스 검증 시 데이터 파싱
  - `LicenseGeneratorTool`: 라이센스 생성 시 데이터 구조 사용

---

### gcp_Wpf 전용 (검증용)

#### LicenseManager.cs
- **용도**: 라이센스 검증 및 관리
- **기능**:
  - 라이센스 파일 검증
  - RSA 서명 검증
  - AES 복호화
  - 하드웨어 ID 일치 확인
  - 만료일 검증
  - 시간 조작 검증
- **사용처**: `App.xaml.cs`에서 시작 시 자동 검증

#### WindowLicenseGenerator.xaml / .xaml.cs
- **용도**: 라이센스 생성 UI (선택사항)
- **참고**: 현재는 사용되지 않을 수 있음

---

### LicenseGeneratorTool 전용 (생성용)

#### KeyGenerator.cs
- **용도**: RSA 키 쌍 생성
- **기능**:
  - 공개키/개인키 생성
  - 키 파일 저장
  - 기존 키 백업
- **사용처**: `LicenseGeneratorTool`의 키 생성 버튼

#### LicenseGenerator.cs
- **용도**: 라이센스 파일 생성
- **기능**:
  - 라이센스 데이터 암호화
  - RSA 서명 생성
  - 라이센스 파일 저장
  - 개인키 자동 삭제 (선택)
- **사용처**: `LicenseGeneratorTool`의 라이센스 생성 버튼

---

## 프로젝트별 포함 파일

### gcp_Wpf.csproj
```xml
<!-- 포함 -->
- License/HardwareIdGenerator.cs
- License/LicenseData.cs
- License/LicenseManager.cs
- License/WindowLicenseGenerator.xaml (선택사항)

<!-- 제외 -->
- License/KeyGenerator.cs
- License/LicenseGenerator.cs
```

### LicenseGeneratorTool.csproj
```xml
<!-- 링크로 포함 -->
- ../License/HardwareIdGenerator.cs
- ../License/KeyGenerator.cs
- ../License/LicenseData.cs
- ../License/LicenseGenerator.cs

<!-- 제외 -->
- License/LicenseManager.cs (검증용이므로 불필요)
```

---

## 보안 고려사항

1. **gcp_Wpf에 생성 기능 없음**
   - `KeyGenerator.cs`와 `LicenseGenerator.cs`가 제외되어 있음
   - 일반 사용자가 라이센스를 생성할 수 없음

2. **LicenseGeneratorTool은 생성 기능만**
   - `LicenseManager.cs`가 포함되지 않음
   - 검증 기능은 없고 생성 기능만 제공

3. **공유 파일의 일관성**
   - `HardwareIdGenerator.cs`와 `LicenseData.cs`는 두 프로젝트가 동일한 버전 사용
   - 라이센스 파일 형식이 항상 호환됨

---

## 파일 추가 시 주의사항

새로운 License 관련 파일을 추가할 때:

1. **역할 확인**: 생성용인지 검증용인지 확인
2. **프로젝트 포함**: 적절한 프로젝트에만 포함
3. **네임스페이스**: `gcp_Wpf.License` 유지
4. **호환성**: 라이센스 파일 형식 변경 시 두 프로젝트 모두 업데이트 필요




