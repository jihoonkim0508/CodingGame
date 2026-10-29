using UnityEngine;

namespace CodingGame.TitleScreen
{
    public sealed class TitleTurretMotion : MonoBehaviour
    {
        public TitlePresentation presentation;
        public RectTransform gun;
        public TitleMuzzleFlash flash;
        public Vector2 home;
        public float shotInterval = 6.4f;
        public float aimDegrees = 2.5f;
        public float recoilDistance = 6;
        [Range(1, 4)] public int burstCount = 3;
        [Min(.1f)] public float burstSpacing = .16f;
        public float MotionTime { get; private set; }
        public float AimAngle { get; private set; }
        public float Recoil { get; private set; }
        public float ShotPhase => Mathf.Repeat(MotionTime + 2.1f, shotInterval);
        void Update()
        {
            if (!presentation || !presentation.IsReady) return;
            MotionTime += Time.unscaledDeltaTime;
            AimAngle = Mathf.Sin(MotionTime * .7f) * aimDegrees;
            float shot = ShotPhase;
            Recoil = 0;
            float flashIntensity = 0;
            for(int i=0;i<burstCount;i++)
            {
                float age=shot-i*burstSpacing;
                if(age>=0&&age<.25f)Recoil=Mathf.Max(Recoil,recoilDistance*Mathf.Pow(1-age/.25f,2));
                if(age>=0&&age<.09f)flashIntensity=Mathf.Max(flashIntensity,1-age/.09f);
            }
            gun.localRotation = Quaternion.Euler(0, 0, AimAngle);
            gun.anchoredPosition = home + Vector2.left * Recoil;
            if (flash && !Mathf.Approximately(flash.Intensity, flashIntensity))
            { flash.Intensity = flashIntensity; flash.SetVerticesDirty(); }
        }
    }
}
