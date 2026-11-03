# Mouse Studio

다섯 번호의 마우스 위치와 자동화 프리셋을 관리하는 Windows 데스크톱 프로그램입니다.

## 다운로드

[최신 릴리스](https://github.com/glasscola/MouseStudio/releases/latest)에서 **MouseStudio-v1.0.0-win-x64.zip**을 내려받고 압축을 푼 다음, **app/MouseStudio.exe**를 실행하세요. Windows 64비트용이며 .NET 실행 환경이 포함되어 있습니다. ZIP 내부에서 바로 실행하지 말고 폴더 전체를 먼저 압축 해제하세요.

**[한국어 PDF 사용설명서](docs/Mouse-Studio-User-Guide-ko.pdf)** · [PDF 바로 다운로드](https://github.com/glasscola/MouseStudio/releases/latest/download/Mouse-Studio-User-Guide-ko.pdf)

사용설명서는 PDF로 제공합니다. ZIP에도 같은 PDF가 포함됩니다.

## 주요 기능

- 1~5번 수동 전환과 번호별 위치 기억
- 마우스마다 프리셋을 따로 연결하고 동시 실행
- 클릭, 더블 클릭, 우클릭, 가운데 클릭, 이동, 드래그, 누르기, 스크롤, 딜레이
- 동작별 실행 전·후 대기, 지정 횟수·무한 반복, 일시 정지·재개
- F9 마우스 녹화와 편집
- 프리셋 저장·검색·불러오기·이름 변경·복제·삭제
- 색상·숫자·기본형 포인터와 번호별 대기 위치 표시
- 저장한 위치 또는 각 마우스 위치 기준으로 실행

**매크로 탭에서 저장 → 마우스 설정에서 프리셋 선택 → 실행** 순서로 사용합니다. 한 번호를 실행한 상태에서 다른 번호를 추가 실행할 수 있습니다.

| 단축키 | 동작 |
|---|---|
| Alt+1~5 | 해당 번호를 수동으로 사용 |
| Ctrl+Alt+1~5 | 연결된 프리셋 실행·중단 |
| F9 | 녹화 시작·종료 |
| F8 | 자동 실행 전체 중단 |

## 입력 방식과 보관

수동 조작은 실제 Windows 커서를 번호별 위치로 전환합니다. 시스템 커서를 다섯 개 생성하는 방식은 아닙니다. 백그라운드 자동화는 대상 창에 마우스 메시지를 보내므로, 대상 프로그램이 이를 지원해야 합니다. 모든 앱·게임에서 자동화가 동작하는 것은 아닙니다.

개인 프리셋과 마우스별 연결은 `app`의 상위 폴더에 있는 `프리셋.json`에 보관됩니다. 업데이트할 때 이 파일을 보존하세요. 재실행 시 연결만 복원하며 매크로는 자동 실행하지 않습니다. 개인 설정은 저장소와 공식 배포 ZIP에 포함하지 않습니다.

## 소스에서 빌드

Windows와 .NET 10 SDK가 필요합니다.

```powershell
dotnet restore src/MouseStudio/MouseStudio.csproj --configfile NuGet.Config
dotnet build src/MouseStudio/MouseStudio.csproj -c Release --no-restore
```

배포 ZIP과 자체 동작 검증 결과는 다음 명령으로 만듭니다.

```powershell
./scripts/Publish-Windows.ps1
```

스크립트는 `artifacts` 아래에 실행 환경을 포함한 Windows x64 ZIP, PDF, SHA-256 체크섬과 검증 결과를 만듭니다. 검증은 별도 수신 프로세스에서 클릭·이동·드래그·반복·다섯 작업의 병렬 실행·프리셋 보관을 확인합니다. 게임 전체의 호환성 검증은 포함하지 않습니다.
