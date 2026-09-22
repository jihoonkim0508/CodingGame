using System;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed class DefenseSelectionCamera : MonoBehaviour
    {
        [SerializeField] Camera battleCamera;
        [SerializeField] float selectionYaw = 25, selectionPitch = 48, selectionDistance = 10;
        [SerializeField] float transitionSeconds = .3f;
        Vector3 homePosition, fromPosition, destination;
        Quaternion homeRotation, fromRotation, rotation;
        Rect homeRect, fromRect, viewport;
        float homeFov, fromFov, fov, elapsed;
        bool initialized;
        void Initialize()
        {
            if (initialized) return;
            if (!battleCamera) throw new InvalidOperationException("선택 카메라 참조를 연결하세요.");
            homePosition = battleCamera.transform.position; homeRotation = battleCamera.transform.rotation;
            homeRect = battleCamera.rect; homeFov = battleCamera.fieldOfView; initialized = true;
            destination = homePosition; rotation = homeRotation; viewport = homeRect; fov = homeFov; elapsed = transitionSeconds;
        }
        public void Focus(Vector3 robot)
        {
            Initialize(); Begin();
            rotation = Quaternion.Euler(selectionPitch, selectionYaw, 0);
            destination = robot + Vector3.up * .7f - rotation * Vector3.forward * selectionDistance;
            viewport = new Rect(0, .4f, .25f, .6f); fov = 36;
        }
        public void Restore()
        {
            if (!initialized) return;
            Begin(); destination = homePosition; rotation = homeRotation; viewport = homeRect; fov = homeFov;
        }
        void Begin()
        {
            fromPosition = battleCamera.transform.position; fromRotation = battleCamera.transform.rotation;
            fromRect = battleCamera.rect; fromFov = battleCamera.fieldOfView; elapsed = 0;
        }
        void LateUpdate()
        {
            Initialize(); if (elapsed >= transitionSeconds) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, transitionSeconds)));
            battleCamera.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, destination, t), Quaternion.Slerp(fromRotation, rotation, t));
            battleCamera.rect = new Rect(Mathf.Lerp(fromRect.x, viewport.x, t), Mathf.Lerp(fromRect.y, viewport.y, t),
                Mathf.Lerp(fromRect.width, viewport.width, t), Mathf.Lerp(fromRect.height, viewport.height, t));
            battleCamera.fieldOfView = Mathf.Lerp(fromFov, fov, t);
        }
    }
}
