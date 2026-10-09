# 블랙카드 도우미

마비노기 경매장 조회, 패스트핑 및 분배 계산기입니다. 음악버프 기능은 비활성화되었으며 기존 OCR 코드는 주석으로 보존되어 있습니다.

## 실행

GitHub Releases의 `BlackCardHelper-…-win-x64.zip`을 전체 압축 해제한 뒤 `블랙카드 도우미.exe`를 실행하세요. .NET, 음악 및 암호화된 공통 경매장 API 키가 포함됩니다.

프로그램 시작 시 새 버전을 자동 확인합니다. 업데이트 탭에서 최신버전을 확인하고 안내에 동의하면 다운로드·SHA256 검증·교체·재실행을 진행합니다. 일반 사용자에게 GitHub 로그인은 필요 없습니다.

## 새 버전 배포

1. 코드를 수정합니다.
2. `Scripts/Set-Version.ps1 -Version 0.1.2-beta.1`처럼 버전을 올립니다.
3. GitHub Desktop에서 변경 내용을 커밋하고 `Push origin`을 누릅니다.
4. GitHub Actions가 테스트와 Windows x64 퍼블리시를 수행하고 새 버전의 Release를 자동 생성합니다.

같은 버전이 이미 배포되었으면 기존 Release를 덮어쓰지 않습니다. 반드시 새 버전으로 올려야 합니다. Actions 실패 시 Release는 생성되지 않습니다.

## 개발

`dotnet build SmokeTests/SmokeTests.csproj -c Release -o TestOutput`

`dotnet TestOutput/SmokeTests.dll --ci`

`dotnet TestOutput/SmokeTests.dll --updater-fixture`

전체 데스크톱/음악 테스트는 소리 장치가 있는 Windows PC에서 `dotnet TestOutput/SmokeTests.dll`로 실행합니다.

`Scripts/Publish-Release.ps1`은 로컬 배포 ZIP과 SHA256 파일을 만듭니다.


## 채집 시간과 알람
공식 [모바일 홈페이지](https://m.mabinogi.nexon.com/m/main/index.asp)의 `erinn_time()`과 동일하게 현실 36분 주기, 24초 보정 및 초 반올림을 적용합니다. 자정의 음수 나머지는 정상 범위로 보정합니다. 시간표는 제공된 19종 희귀 채집물 기준입니다. 채집 알람 전체 스위치와 품목 체크, 별도 볼륨을 저장합니다. 시작 시각을 통과할 때만 한 번 알리며, 실행/토글/절전 복귀 시 지난 시작을 재생하지 않습니다. PC 시계가 정확해야 합니다.
경매장 공통 API 키는 암호화 리소스로 어셈블리에 내장합니다. 외부 키 파일 또는 Windows 계정에 의존하지 않습니다.


BETA 0.1.5: 상단 에린 AM/PM 시계는 기능창을 접어도 유지됩니다. 채집물별 시작/종료 시각은 에린 AM/PM으로, 타이머는 남은 현실 시간으로 표시합니다.
설정 탭에서 백그라운드 전환, Windows 로그인 시 자동 실행, 시작할 때 백그라운드 실행, 닫으면 트레이 전환, 항상 위 표시를 선택합니다. 트레이 아이콘을 두 번 클릭하면 열리며 우클릭 메뉴에서 종료합니다.
경매장 검색은 공백/숨은 문자를 정리하고 이름 조건 오류 시 키워드로 재조회합니다. 넥슨 API의 데이터 준비/점검/키 오류를 구분하여 표시합니다.

