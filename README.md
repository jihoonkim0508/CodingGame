# CodingGame

Unity 기반 코딩 게임 프로젝트입니다. 프로토타입 개발은 `develop`에서 진행합니다.

## 브랜치 운영

- `main`: GitHub 기본 브랜치입니다. 검토가 끝난 안정 버전을 관리합니다. 현재 프로젝트 파일은 초기 기준 상태이며, 프로토타입은 `develop`에서 받으세요.
- `develop`: `main`의 이력을 포함하는 프로토타입 개발 브랜치입니다. 게임 기능과 실험 작업을 통합합니다.
- 기능 작업은 `develop`에서 작업용 브랜치를 만들고, `develop`을 대상으로 PR을 올립니다. 병합 후 작업용 브랜치는 삭제합니다.
- `develop`의 내용을 `main`에 반영할 때도 PR로 변경 내용을 확인합니다. 병합 여부는 저장소 소유자가 결정합니다.

`origin`은 GitHub 원격 저장소의 기본 이름입니다. `origin/develop`은 원격의 `develop`을 가리키며, 별도의 브랜치 이름이나 접근 제한이 아닙니다. `main`과 `develop`은 각각 독립된 브랜치 이름입니다.

## 팀원이 develop 받기

### 처음 받는 경우

Git과 Git LFS를 설치한 뒤 다음 명령을 실행합니다.

```bash
git lfs install
git clone --branch develop https://github.com/jihoonkim0508/CodingGame.git
cd CodingGame
git lfs pull
```

Unity Hub에서 내려받은 `CodingGame` 폴더를 추가하고 **Unity 6000.3.21f1**로 엽니다. 정확한 에디터 버전은 `ProjectSettings/ProjectVersion.txt`에 기록되어 있습니다.

### 이미 clone한 경우

로컬에서 수정 중인 파일은 먼저 작업용 브랜치에 커밋하거나 별도로 보관하세요. 저장소 폴더에서 다음 명령을 실행합니다.

```bash
git fetch origin --prune
git switch develop
git branch --set-upstream-to=origin/develop develop
git pull --ff-only
git lfs pull
```

로컬 `develop`이 없으면 `git switch develop`이 원격 브랜치를 찾아 자동으로 생성합니다. 자동으로 찾지 못하면 해당 줄 대신 `git switch --track origin/develop`을 실행하세요.

이후 `develop`의 최신 내용만 받을 때는 다음 명령을 사용합니다.

```bash
git switch develop
git pull --ff-only
git lfs pull
```

### 기능 작업과 PR

최신 원격 개발 브랜치에서 작업용 브랜치를 만듭니다. `feature/my-task`는 본인 작업 이름으로 바꾸세요.

```bash
git fetch origin
git switch -c feature/my-task origin/develop
# Unity에서 작업한 뒤 변경 파일을 확인하고 필요한 파일만 git add 및 git commit합니다.
git push -u origin feature/my-task
```

GitHub에서 **base: develop**, **compare: feature/my-task**로 PR을 생성하고 `@jihoonkim0508`의 리뷰를 요청합니다.

## 리뷰와 병합 권한

- `main`, `develop` 모두 팀원의 직접 push를 막고 PR을 요구합니다.
- 모든 파일의 CODEOWNER는 `@jihoonkim0508`입니다. 팀원 PR은 해당 소유자의 승인 없이는 병합할 수 없습니다.
- 승인 후 새로운 변경을 push하면 기존 승인이 해제되므로 다시 리뷰를 받아야 합니다.
- 저장소 소유자는 관리자 예외로 병합할 수 있습니다. 본인이 작성한 PR은 GitHub에서 스스로 승인할 수 없으므로 관리자 병합 기능을 사용합니다.
- 보호 규칙은 clone, fetch, pull을 제한하지 않습니다.

## pull이 안 될 때

- `git remote -v`로 `origin`이 `https://github.com/jihoonkim0508/CodingGame.git`인지 확인하세요.
- `git branch -vv`로 현재 브랜치가 `develop`이고 추적 대상이 `origin/develop`인지 확인하세요.
- `main`에서 pull하면 `develop`의 프로토타입 변경이 들어오지 않습니다. 먼저 `develop`으로 전환하세요.
- 로컬 수정 파일 때문에 전환이 막히면 작업을 보관한 뒤 다시 시도하세요.
- `Not possible to fast-forward`가 나오면 로컬과 원격 이력이 갈라진 상태입니다. `reset --hard`나 강제 push 대신 오류 메시지와 `git status`를 저장소 소유자에게 전달하세요.
- 이미지나 폰트가 포인터 텍스트로 보이거나 LFS 오류가 나면 Git LFS 설치 상태와 `git lfs pull` 결과를 확인하세요.
