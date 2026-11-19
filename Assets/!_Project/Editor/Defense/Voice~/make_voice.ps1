# Records 디버's lines with the Windows Korean voice (Microsoft Heami) and gives them a small-robot sound with ffmpeg.
# Called by Tools/Develop/디버 음성 만들기 (GuideVoice.cs) with a job list of "key<TAB>voice<TAB>text" lines;
# voice is "guide" (디버) or "system" (the alert banner). Existing clips are kept, so only new lines are recorded.
# -Engine typecast reads 디버's lines with a Typecast voice (api.typecast.ai, key in TYPECAST_API_KEY) found by -VoiceName,
# or TYPECAST_VOICE_ID if set; the alert banner keeps the Windows voice. Typecast returns 44.1 kHz WAV, so its clips get
# their own filter (no pitch shift; pitch and speed are the voice's own).
# -Engine elevenlabs reads 디버's lines with the ElevenLabs voice ELEVENLABS_VOICE_ID (key in ELEVENLABS_API_KEY; MP3, same filter
# as Typecast). It replaces clips in place, only after the new one is made, and skips clips already tagged elevenlabs, so a
# cut-short run resumes; it stops while 300 characters of the month's quota are still left (lines not reached keep their clip).
param([Parameter(Mandatory)] [string] $Jobs, [Parameter(Mandatory)] [string] $Out,
      [ValidateSet('windows', 'typecast', 'elevenlabs')] [string] $Engine = 'windows', [string] $VoiceName = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$ffmpeg = (Get-Command ffmpeg).Source
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synth.SelectVoice('Microsoft Heami Desktop')
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
# 디버: a boy's voice from the woman's voice: pitch and formants down 6% (her 197 Hz to about 185 Hz), a little faster,
# more chest at 260 Hz and less breath at 7 kHz, with only a faint metallic echo left of the robot. The system alert: lower,
# band-limited like a speaker.
$filters = @{
    guide  = 'asetrate=22050*0.94,aresample=22050,atempo=1.12,equalizer=f=260:t=q:w=1:g=3,equalizer=f=7000:t=q:w=1:g=-4,aecho=0.8:0.3:7:0.12,highpass=f=90,silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse,loudnorm=I=-17:TP=-1.5,aresample=22050'
    system = 'asetrate=22050*0.95,aresample=22050,highpass=f=280,lowpass=f=3400,aecho=0.8:0.4:28:0.25,silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse,loudnorm=I=-17:TP=-1.5,aresample=22050'
}
$filters.typecast = 'highpass=f=90,silenceremove=start_periods=1:start_threshold=-45dB,areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse,loudnorm=I=-17:TP=-1.5,aresample=22050'
if ($Engine -eq 'typecast') {
    if (-not $env:TYPECAST_API_KEY) { throw 'TYPECAST_API_KEY is not set' }
    $headers = @{ 'X-API-KEY' = $env:TYPECAST_API_KEY }
    $voiceId = $env:TYPECAST_VOICE_ID
    if (-not $voiceId) {
        # PS 5.1 garbles Korean in a response without a charset, so the body is decoded as UTF-8 by hand.
        $response = Invoke-WebRequest 'https://api.typecast.ai/v3/voices?model=ssfm-v31' -Headers $headers -UseBasicParsing
        $voices = [Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray()) | ConvertFrom-Json
        if ($voices.voices) { $voices = $voices.voices }
        $match = @($voices | Where-Object { $_.voice_name.kor -eq $VoiceName -or $_.voice_name.eng -eq $VoiceName })
        if ($match.Count -eq 0) { throw "Typecast has no voice named '$VoiceName' for this key (set TYPECAST_VOICE_ID to choose one)" }
        $voiceId = $match[0].voice_id
    }
}
if ($Engine -eq 'elevenlabs') {
    if (-not $env:ELEVENLABS_API_KEY) { throw 'ELEVENLABS_API_KEY is not set' }
    $headers = @{ 'xi-api-key' = $env:ELEVENLABS_API_KEY }
    $voiceId = $env:ELEVENLABS_VOICE_ID
    $plan = Invoke-RestMethod 'https://api.elevenlabs.io/v1/user/subscription' -Headers $headers
    $left = $plan.character_limit - $plan.character_count
    $ffprobe = (Get-Command ffprobe).Source
}
$made = 0
foreach ($line in Get-Content -LiteralPath $Jobs -Encoding UTF8) {
    $key, $voice, $text = $line -split "`t", 3
    $ogg = Join-Path $Out "$key.ogg"
    $eleven = $Engine -eq 'elevenlabs' -and $voice -eq 'guide'
    if (Test-Path -LiteralPath $ogg) {
        if (-not $eleven) { continue }
        if ((& $ffprobe -v error -show_entries stream_tags=comment -of default=nw=1:nk=1 $ogg) -eq 'elevenlabs') { continue }
    }
    if ($eleven -and $left - $text.Length -lt 300) { "stopped at $key, $left characters left"; break }
    $wav = Join-Path ([IO.Path]::GetTempPath()) "$key.wav"
    $filter = $filters[$voice]
    if ($Engine -eq 'typecast' -and $voice -eq 'guide') {
        $body = @{ voice_id = $voiceId; text = $text; model = 'ssfm-v31'; language = 'kor'
                   prompt = @{ emotion_type = 'preset'; emotion_preset = 'normal' }; output = @{ audio_format = 'wav' } } | ConvertTo-Json -Depth 3
        # Sent as UTF-8 bytes: a string body loses Korean in PS 5.1.
        Invoke-RestMethod -Method Post -Uri 'https://api.typecast.ai/v1/text-to-speech' -Headers $headers `
            -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -OutFile $wav
        $filter = $filters.typecast
    }
    elseif ($eleven) {
        $wav = [IO.Path]::ChangeExtension($wav, '.mp3')
        $body = @{ text = $text; model_id = 'eleven_multilingual_v2'; language_code = 'ko' } | ConvertTo-Json
        Invoke-RestMethod -Method Post -Uri "https://api.elevenlabs.io/v1/text-to-speech/$voiceId`?output_format=mp3_44100_128" -Headers $headers `
            -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -OutFile $wav
        $left -= $text.Length
        $filter = $filters.typecast
    }
    else {
        $synth.Rate = if ($voice -eq 'system') { 1 } else { 3 }
        $synth.SetOutputToWaveFile($wav, $format); $synth.Speak($text); $synth.SetOutputToNull()
    }
    $tag = if ($eleven) { 'elevenlabs' } else { $Engine }
    # Encoded beside the clip first, so a failed encode never costs the old clip.
    & $ffmpeg -v error -y -i $wav -af $filter -ac 1 -c:a libvorbis -q:a 4 -metadata comment=$tag -f ogg "$ogg.new"
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed for $key" }
    Move-Item -LiteralPath "$ogg.new" $ogg -Force
    Remove-Item -LiteralPath $wav
    $made++
}
"recorded $made"
