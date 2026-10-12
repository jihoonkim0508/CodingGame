# 명령어 블록 → Python 3

Defense 씬의 코드 편집기(`RobotCodeEditor` 프리팹)가 쓰는 블록 트리 시스템이다.
구성 요소와 책임은 [Docs/구조.md](../../../../Docs/구조.md)를 본다.

- `BlockProgram.cs`: `BlockKind`, `CodeBlock`(인자·본문 트리), 비교 연산자·숫자 정규화 헬퍼
- `PythonTreeCompiler.cs`: 트리 → Python 함수 문자열
- `Command*.cs`: 편집기 패널, 드롭 영역, 블록 UI. 블록 프리팹은 `Assets/!_Project/UI/CommandBlocks`

## 검증

```powershell
rtk proxy dotnet run --project Tests/BlockCoding/BlockCoding.Check.csproj
python -I Tests/BlockCoding/check_tree_python.py
```
