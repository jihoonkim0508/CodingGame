# 디버 목소리(남자아이) TTS 후보 비교

6차 백로그 67. 디버(작은 로봇 가이드)의 목소리를 남자아이(8~12세) 톤으로 미리 구울 때 고를 수 있는 서비스를 정리했다. 조사일은 2026-11-05이고 공식 문서·요금 페이지를 우선으로 봤다. 직접 확인하지 못한 칸은 **확인 필요**로 두었다. 샘플은 아무도 들어보지 않았다. 가격·약관은 자주 바뀌므로 결제·공개 배포 전에 다시 확인한다.

## 지금 TTS가 동작하는 방식

- 실시간 TTS가 아니라 **미리 구운 파일**이다. 게임은 인터넷이나 OS 음성 없이 파일만 재생한다.
- `Tools/Develop/디버 음성 만들기`(`Editor/Defense/GuideVoice.cs`)가 켜져 있는 모든 `TutorialSequence`의 대사를 모은다. 같은 문장은 한 번만 굽는다.
- 대사는 `GuideSpeech.Speakable`로 읽을 글자로 바꾼다. 태그·따옴표를 지우고 코드 이름을 한글 소리로 바꾼다(예: `slash()` → "슬래시", `<=` → "작거나 같다").
- `Editor/Defense/Voice~/make_voice.ps1`이 Windows 한국어 음성 **Microsoft Heami**(SAPI, 성인 여성)로 WAV를 만들고, ffmpeg 필터로 남자아이 쪽 소리로 바꾼다. 음높이·포먼트 6% 낮춤, 조금 빠르게, 260Hz 가슴 울림 +3dB, 7kHz 숨소리 -4dB, 옅은 금속 메아리. 시스템 경고 목소리는 대역을 좁힌 스피커 소리다.
- 결과는 `Resources/Voice/<대사 해시>.ogg`(MD5 앞 8바이트, 16자리)이다. 게임은 대사 문장의 해시로 파일을 찾아 재생한다. 그래서 대사를 고치면 다시 구울 때까지 그 줄은 소리가 없다. 이미 있는 파일은 다시 굽지 않고, 어떤 대사도 쓰지 않는 파일은 지운다.
- 다른 서비스의 목소리로 바꾸려면 그 서비스에서 받은 파일을 같은 해시 이름의 ogg로 넣으면 된다. 대사 목록(해시·화자·읽을 글자)은 메뉴가 `%TEMP%/guide_voice_jobs.txt`로 만든다. 이 목록을 그대로 외부 TTS API에 넘기는 스크립트를 붙이면 일괄로 바꿀 수 있다.

## 제외한 서비스

- 수퍼톤 플레이: 2026-08-31 서비스 종료([공지](https://www.supertone.ai/ko/sunset)).
- KT AI 보이스 스튜디오: 2025년 종료([Vrew 공지](https://docs.channel.io/vrew-kr/ko/articles/%EA%B3%B5%EC%A7%80-%EC%9D%BC%EB%B6%80-AI-%EB%AA%A9%EC%86%8C%EB%A6%AC%EC%9D%98-%EC%A0%9C%EA%B3%B5%EC%9D%B4-20250414%EC%9B%94-%EB%82%AE-12%EC%8B%9C%EB%B6%80%EB%A1%9C-%EC%A2%85%EB%A3%8C%EB%90%98%EC%97%88%EC%8A%B5%EB%8B%88%EB%8B%A4-222198ec)).
- F5-TTS: 공식 한국어 없음, 가중치 CC-BY-NC([GitHub](https://github.com/SWivid/F5-TTS)).
- Voicemod: 한국어 아동 TTS 없음.

## 비교표

| 서비스 | 남자아이 목소리 | 내보내기 | 무료/가격 | 라이선스 | 조절 | 링크 |
|---|---|---|---|---|---|---|
| **타입캐스트** | **있음: 준우**(밝고 높은 장난꾸러기 톤). 다른 아동 캐릭터(호빈이·하영이·태지·머루·토비)의 성별은 확인 필요 | 웹 다운로드, API는 WAV(32kHz)·MP3 | 최초 3,000크레딧(약 5분) 무료. 베이직 월 ₩9,900에 30,000크레딧. 다운로드할 때만 차감 | 무료는 출처 표기 필수, 베이직부터 상업 이용. 정책에 '생성 음성을 낱개 파일·사운드 라이브러리로 쓰는 것 금지'와 '콘텐츠 제작 프로그램 통합은 별도 계약' 조항이 있어 게임 탑재는 **서면 문의** 필요 | 감정 7종, 속도. 피치는 확인 필요 | [준우](https://typecast.ai/kr/learn/ai-boy-voice-junwoo/), [요금](https://typecast.ai/kr/pricing), [정책](https://typecast.ai/policy?lang=en), [API](https://typecast.ai/docs/ko/overview) |
| **네이버 CLOVA Voice API** | **있음: 하준**(`nhajun`, 아동 남), 멍멍이(`nwoof`, 아동 남) | MP3, WAV(8~48kHz) | 종량제, 원 단가는 콘솔에서 확인 필요 | 네이버클라우드 상품은 출처 표기 없이 상업 이용 가능하다고 안내(Voice API 약관은 확인 필요) | pitch −5~5, speed −5~10. 하준은 감정 미지원 | [API 문서](https://api.ncloud-docs.com/docs/ai-naver-clovavoice-ttspremium) |
| 클로바더빙(무료 웹·앱) | 어린이 목소리 있음, 하준 포함 여부 확인 필요 | 구간·전체 다운로드 | 무료 월 15,000자, 다운로드 월 20회(타사 블로그 기준) | 무료는 출처 표기, 수익 없는 채널만. 상업은 유료 CLOVA Dubbing | 속도 | [NCP 개요](https://guide.ncloud-docs.com/docs/clovadubbing-overview) |
| Vrew | 한국어 목소리 366개, 남자아이 이름 확인 필요 | 오디오만 내보내기 확인 필요 | 플랜별, 금액 확인 필요 | 상업·비상업 가능 | 확인 필요 | [상업 이용 안내](https://docs.channel.io/vrew-kr/ko/articles/%EC%83%81%EC%97%85-%EC%9D%B4%EC%9A%A9-AI-%EB%AA%A9%EC%86%8C%EB%A6%AC%EB%A5%BC-%EC%83%81%EC%97%85%EC%A0%81%EC%9D%B8-%EC%9A%A9%EB%8F%84%EB%A1%9C-%EC%82%AC%EC%9A%A9%ED%95%A0-%EC%88%98-%EC%9E%88%EC%9D%84%EA%B9%8C%EC%9A%94-addbf954) |
| ElevenLabs | 사실상 없음. 아동·아동처럼 들리는 목소리는 보이스 라이브러리 등록 금지. Voice Design 허용 여부 확인 필요 | MP3 | Free 월 10k 크레딧, Starter 월 $6 | Free는 상업 불가. 기본 보이스 2026-12-31 만료 | Voice Design | [아동 정책](https://elevenlabs.io/docs/help-center/product/voices/voice-library/can-childrens-or-child-like-voices-be-added-to-the-voice-library), [요금](https://elevenlabs.io/pricing) |
| Azure TTS | 없음(ko-KR 16개 모두 성인) | 확인 필요 | F0 무료 월 50만 자 | 확인 필요 | SSML 피치·속도 | [언어 지원](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts) |
| Google Cloud TTS | 없음(ko-KR 41개 모두 성인) | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | [보이스 목록](https://docs.cloud.google.com/text-to-speech/docs/list-voices-and-types) |
| Amazon Polly | 없음(Seoyeon·Jihye·Hyunduk 성인) | 확인 필요 | 확인 필요 | 확인 필요 | SSML | [보이스 목록](https://docs.aws.amazon.com/polly/latest/dg/available-voices.html) |
| OpenAI TTS | 없음(성인 13개, 영어 위주) | MP3·WAV 등 | 확인 필요 | AI 음성 고지 의무. gpt-4o-mini-tts·tts-1은 2027-01-06 종료 예정 | 지시문 | [가이드](https://developers.openai.com/api/docs/guides/text-to-speech) |
| 카카오 음성 합성 | 확인 필요(문서 로그인·권한 신청) | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | [문서](https://developers.kakao.com/docs/in/voice/common) |
| **Qwen3-TTS VoiceDesign**(로컬) | 설명문("10살 남자아이")으로 목소리 생성. 공식 예시는 17세 남자가 가장 어림, 직접 테스트 필요 | WAV | 무료(GPU 권장) | Apache-2.0, 상업 가능 | 설명문으로 음색·감정·속도·피치, Base 모델 복제로 고정 | [GitHub](https://github.com/QwenLM/Qwen3-TTS) |
| GPT-SoVITS(로컬) | 기본 목소리 없음, 참조 음성 필요(5초~1분) | WAV | 무료 | MIT, 참조 음성 권리는 따로 | 참조 음성을 따라감 | [GitHub](https://github.com/RVC-Boss/GPT-SoVITS) |
| MeloTTS(로컬) | 없음 | WAV | 무료 | MIT | 속도 | [GitHub](https://github.com/myshell-ai/MeloTTS) |
| Coqui XTTS-v2(로컬) | 없음 | WAV | 무료 | 비상업 전용, Coqui 폐업 | 확인 필요 | [모델](https://huggingface.co/coqui/XTTS-v2) |
| Edge-TTS | 없음(Azure 성인 목소리) | MP3 | 무료 | 비공식, MS 온라인 호출, 상업 근거 없음 | rate·pitch·volume | [GitHub](https://github.com/rany2/edge-tts) |

## 추천 3곳

1. **타입캐스트 '준우'**: 공식적으로 '남자아이'로 소개된 유일한 캐릭터. 감정 7종으로 칭찬·힌트·위로를 나눌 수 있고, API·C# SDK로 150여 줄을 한 번에 뽑을 수 있다. 150줄 × 평균 30자 ≈ 4,500자라 베이직 1개월(₩9,900)이면 충분하다(1글자 = 1크레딧 가정). 공개 배포 전에 게임 탑재 허용을 서면으로 받아야 한다.
2. **네이버 CLOVA Voice API '하준'**: 화자 목록에 아동(남)으로 명시. pitch·speed를 숫자로 고정해 톤이 흔들리지 않는다. 무료 클로바더빙에서 하준을 먼저 들어보고 API로 일괄 생성하는 순서를 권한다. 감정 파라미터가 없고 단가는 콘솔에서 확인해야 한다.
3. **Qwen3-TTS VoiceDesign(로컬)**: 비용 0원, Apache-2.0이라 라이선스가 가장 깔끔하다. 설명문으로 만든 목소리를 Base 모델로 복제해 고정한다. GPU·파이썬이 필요하고 한국어 아동 품질은 보장되지 않아 30분쯤 시험해 보고 정한다. 로봇이라 어색한 부분은 지금처럼 로봇 효과로 가릴 수 있다.

성인 목소리 음높이만 올리는 방식(지금 방식)은 부자연스러워서 위 셋보다 뒤에 둔다.

## 고를 때 체크할 점

- **게임 사용 라이선스**: 캡스톤 시연(비상업)과 공개 배포를 따로 따진다. 무료 플랜은 대부분 출처 표기 또는 비상업 전용이다. 공개할 계획이면 유료 기간에 전체 대사를 받고 약관·결제 내역을 보관하고 크레딧 화면에 출처를 적는다.
- **150여 줄의 일관성**: 캐릭터·스타일·속도·피치를 고정해 한 번에 일괄 생성한다. 모델이 바뀌면 나중에 추가한 줄만 다르게 들릴 수 있으니 모델 버전을 기록한다.
- **일괄 내보내기**: 웹에서 수동으로 받으면 실수가 많다. `guide_voice_jobs.txt`(해시·화자·글자)를 API 스크립트에 넣어 `<해시>.ogg`로 받으면 게임 코드는 그대로 쓴다.
- **포맷**: 모노, 44.1kHz 이상으로 받아 음량을 맞추고 앞뒤 무음을 자른다(지금 `make_voice.ps1`이 하는 일).
- **아동 목소리 윤리**: 실제 아이 녹음을 복제하려면 보호자 동의가 필요하다. 복제형 도구는 팀원 본인 녹음으로만 쓴다.
