# GameData 사용법

### 수치 바꾸기

1. `GameData/Award.md`(보상·코인)나 `GameData/Balance.md`(로봇·몬스터) 표의 숫자를 고칩니다.
   - 보상 표의 `start` 줄은 1-1 시작 재고, 나머지 줄은 **그 웨이브를 깼을 때** 보상창에서 받는 것입니다. 다음 웨이브 준비부터 씁니다.
   - 개수는 `slash() ×1`처럼 씁니다. `x`로 써도 적용할 때 `×`로 고쳐집니다.
   - `-`는 없음, 괄호 안 증분값 `(+1)`은 읽지 않습니다. 해금 열도 읽지 않습니다.
2. Unity에서 **Play**를 누르면 바뀐 표가 자동으로 적용됩니다. (Play 없이 적용하려면 메뉴바 **Tools → Develop → GameData 적용**)
3. Console에 **"GameData 적용"** 요약(웨이브별 클리어 보상)이 뜹니다. 경고·오류가 있으면 아래 표를 보세요.

⚠️ 적용하면 `StageOneCampaign.asset`이 바뀌어 스테이지 캐시가 **"캐시 오래됨"**이 됩니다. 수치를 정한 뒤 [스테이지 캐시](스테이지%20캐시%20사용법.md)를 다시 기록하세요.

---

## Console에 이런 문구가 뜨면

| 문구 | 뜻 | 할 일 |
| --- | --- | --- |
| **GameData 오류 · 모르는 블록/로봇/적** | 표의 이름을 못 읽음 (적용 안 됨, Play도 멈춤) | 줄 번호의 이름을 확인 (블록 이름 예: `slash()`, `while`, `변수 = 값`, `비교`) |
| **GameData 오류 · 숫자가 아님** | 숫자 칸에 글자가 있음 | 줄 번호의 칸을 숫자로 |
| **GameData 경고 · DPS / Level 2·3 피해** | 계산 열이 Level 1과 안 맞음 (적용은 됨) | Level 1을 고친 게 맞으면 계산 열도 맞춰 적기 |
| **GameData 경고 · 마지막 출현 / 처치 코인** | 출현 + 간격 × (수 − 1), 적 수 × 처치당과 다름 | 계산 열을 맞춰 적기 |
| **블록이 모자라 적용되지 않음** | 수업의 예제 코드에 쓸 블록이 그 웨이브 재고에 없음 | 앞 웨이브 보상에 그 블록 넣기 |

<details>
<summary>개발자 참고</summary>

- 코드: `Editor/Defense/GameDataTables.cs`(표 읽기, Unity 없음), `GameDataApply.cs`(메뉴·Play 전 적용, `Library/GameDataApplied.hash`), `TutorialSequenceBuilder.Campaign()`(asset 쓰기).
- 쓰는 곳: 수업별 `rewards`·`rewardRobots`·`startCoins`·`coinsPerKill`, 1-1의 `blocks`·`robots`(start 줄), 웨이브별 적 그룹, 로봇 asset의 Level 1 피해·간격·사거리. Level 2·3은 확인만 합니다.
- 보스 코어 피해 12, 길은 기존 값 유지. 검사: `dotnet run --project Tests/Defense/Defense.Check.csproj`.

</details>
