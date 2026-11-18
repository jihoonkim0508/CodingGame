using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PilotoStudio.ParticleEffects;

namespace PilotoStudio
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(ParticleSystem), typeof(ParticleSystemRenderer))]
    [AddComponentMenu("Piloto Studio/Distortion Grabber")]
    public class DistortionGrabber : MonoBehaviour
    {
        internal static readonly HashSet<DistortionGrabber> Active = new HashSet<DistortionGrabber>();

        [SerializeField] private ParticleEffectStage _stage = ParticleEffectStage.Scene;
        [SerializeField, Range(0, 3)] private int _compositionGroup;
        [SerializeField] private int _sortOrder;
        [SerializeField] private Transform _assignedCenter;

        private ParticleSystem _system;
        private ParticleSystemRenderer _renderer;
        private MaterialPropertyBlock _properties;
        [SerializeField, Tooltip("Fit the particle to each rendering camera.")] private bool _fillScreen;
        [SerializeField] private Camera _screenCamera;
        [SerializeField, Min(0.01f)] private float _distanceFromNearPlane = 0.1f;
        [SerializeField, Min(1)] private float _overscan = 1.02f;
        private ParticleSystem.Particle[] _particles;
        private ParticleSystem.Particle[] _originalParticles;
        private Material _configuredMaterial;
        private readonly Dictionary<Camera, Mesh> _drawMeshes = new Dictionary<Camera, Mesh>();
        private readonly List<Camera> _expiredCameras = new List<Camera>();
        private readonly Plane[] _diagnosticPlanes = new Plane[6];
        private int _lastCaptureFrame = -1;
        private bool _warned;

        private bool UsesEffectStreams => _renderer != null && _renderer.sharedMaterial != null
            && (_renderer.sharedMaterial.HasProperty("_EffectMap") || IsAnimeImpact)
            && _renderer.sharedMaterial.HasProperty("_CaptureStage");

        private bool IsAnimeImpact => _renderer != null && _renderer.sharedMaterial != null
            && _renderer.sharedMaterial.HasProperty("_Threshold");

        // Original Warp relies on the pipeline's scene color and native transparent sorting.
        internal bool UsesLegacyWarp => _renderer != null && _renderer.sharedMaterial != null
            && _renderer.sharedMaterial.shader != null
            && _renderer.sharedMaterial.shader.name == "Piloto Studio/Piloto Warp";

        public ParticleEffectStage Stage => _stage;
        public int CompositionGroup => _compositionGroup;
        public int SortOrder => _sortOrder;
        public ParticleSystemRenderer Renderer => _renderer;
        public ParticleSystem System => _system;
        public Matrix4x4 DrawMatrix
        {
            get
            {
                // BakeMesh(false) and the native Center stream retain simulation-space coordinates.
                var main = _system.main;
                if (main.simulationSpace == ParticleSystemSimulationSpace.World) return Matrix4x4.identity;
                Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Custom
                    ? main.customSimulationSpace : transform;
                return space != null ? space.localToWorldMatrix : Matrix4x4.identity;
            }
        }

        // UV.xy + AgePercent.z + StableRandomX.w fill TEXCOORD0 exactly.
        // Custom1 occupies TEXCOORD1. Native center + vertex ID fill TEXCOORD2.
        public static readonly ParticleSystemVertexStream[] StreamProfile =
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Tangent, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent,
            ParticleSystemVertexStream.StableRandomX, ParticleSystemVertexStream.Custom1XYZW,
            ParticleSystemVertexStream.Center, ParticleSystemVertexStream.VertexID
        };

        private void OnEnable()
        {
            _system = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();
            _properties = new MaterialPropertyBlock();
            Active.Add(this);
            PilotoSceneCapture.EnsureInitialized();
            ApplyStreamProfile();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (!PilotoSceneCapture.HasEmitters) PilotoSceneCapture.Release();
            foreach (var mesh in _drawMeshes.Values)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            _drawMeshes.Clear();
            if (_renderer == null || _properties == null || !UsesEffectStreams) return;
            _renderer.GetPropertyBlock(_properties);
            _properties.SetFloat("_PilotoParticleDataBound", 0);
            _renderer.SetPropertyBlock(_properties);
        }

        public void Configure(ParticleEffectStage stage, int compositionGroup = 0)
        {
            _stage = stage;
            _compositionGroup = Mathf.Clamp(compositionGroup, 0, 3);
            ApplyStreamProfile();
        }

        [ContextMenu("Apply Piloto Particle Streams")]
        public void ApplyStreamProfile()
        {
            if (_system == null) _system = GetComponent<ParticleSystem>();
            if (_renderer == null) _renderer = GetComponent<ParticleSystemRenderer>();
            _configuredMaterial = _renderer.sharedMaterial;
            if (UsesLegacyWarp) return;
            if (UsesEffectStreams)
            {
                _renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>(StreamProfile));
                EnsureCustomData();
            }
            var main = _system.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        private void EnsureCustomData()
        {
            var data = _system.customData;
            // Preserve authored vector curves; initialize only missing channels.
            bool vector = data.GetMode(ParticleSystemCustomData.Custom1) == ParticleSystemCustomDataMode.Vector;
            int existing = vector ? data.GetVectorComponentCount(ParticleSystemCustomData.Custom1) : 0;
            bool anime = IsAnimeImpact;
            if (!data.enabled || existing != 4)
            {
                data.enabled = true;
                data.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
                data.SetVectorComponentCount(ParticleSystemCustomData.Custom1, 4);
                for (int i = existing; i < 4; i++)
                    data.SetVector(ParticleSystemCustomData.Custom1, i,
                        new ParticleSystem.MinMaxCurve(i < 3 && !(anime && i == 1) ? 1 : 0));
            }
#if UNITY_EDITOR
            var serialized = new UnityEditor.SerializedObject(_system);
            string[] labels = { "Distortion", anime ? "Flip Black / White" : "RGB Split",
                anime ? "Impact Strength" : "Blur / Impact", "Animation Offset" };
            for (int i = 0; i < labels.Length; i++)
                serialized.FindProperty("CustomDataModule.vectorLabel0_" + i).stringValue = labels[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        private void LateUpdate()
        {
            if (!UsesLegacyWarp && _fillScreen) Fit(_screenCamera != null ? _screenCamera : Camera.main);
            UpdateParticleData();
            if (UsesLegacyWarp) return;
            if (Application.IsPlaying(gameObject) && !_warned && _system.particleCount > 0 && Time.frameCount > 30
                && Time.frameCount - _lastCaptureFrame > 30)
            {
                var camera = Camera.main;
                if (camera == null) return;
                GeometryUtility.CalculateFrustumPlanes(camera, _diagnosticPlanes);
                if (!GeometryUtility.TestPlanesAABB(_diagnosticPlanes, _renderer.bounds)) return;
                Debug.LogWarning("Piloto particle effect has not been drawn by shared capture. "
                    + "Check the camera culling mask, capture stage, and pipeline package version (URP/HDRP 14).", this);
                _warned = true;
            }
        }

        internal void MarkDrawn()
        {
            _lastCaptureFrame = Time.frameCount;
            _warned = false;
        }

        internal Mesh PrepareGeometry(Camera camera)
        {
            if (!_drawMeshes.TryGetValue(camera, out var mesh))
            {
                _expiredCameras.Clear();
                foreach (var pair in _drawMeshes)
                {
                    if (pair.Key != null) continue;
                    if (Application.isPlaying) Destroy(pair.Value);
                    else DestroyImmediate(pair.Value);
                    _expiredCameras.Add(pair.Key);
                }
                foreach (var expired in _expiredCameras) _drawMeshes.Remove(expired);
                mesh = new Mesh { name = "Piloto particle draw geometry", hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                _drawMeshes.Add(camera, mesh);
            }
            // Refresh geometry while the capture service suppresses the ordinary renderer.
            if (_fillScreen) BakeForCamera(mesh, _renderer, camera);
            else _renderer.BakeMesh(mesh, camera, false);
            if (mesh.vertexCount > 0) mesh.RecalculateBounds();
            return mesh;
        }

        internal Mesh DrawGeometry(Camera camera) => _drawMeshes[camera];

        internal void UpdateParticleData()
        {
            if (_system == null || _renderer == null) return;
            if (_configuredMaterial != _renderer.sharedMaterial) ApplyStreamProfile();
            if (!UsesEffectStreams) return;
            _renderer.GetPropertyBlock(_properties);
            _properties.SetFloat("_PilotoParticleDataBound", 1);
            _properties.SetFloat("_CaptureStage", (int)_stage);
            if (_assignedCenter != null)
            {
                _properties.SetFloat("_CenterMode", 1);
                _properties.SetVector("_WorldCenter", _assignedCenter.position);
            }
            _renderer.SetPropertyBlock(_properties);
        }
        private void BakeForCamera(Mesh mesh, ParticleSystemRenderer renderer, Camera camera)
        {
            if (_system == null) _system = GetComponent<ParticleSystem>();
            int capacity = _system.main.maxParticles;
            if (_originalParticles == null || _originalParticles.Length < capacity)
                _originalParticles = new ParticleSystem.Particle[capacity];
            int count = _system.GetParticles(_originalParticles);
            try
            {
                Fit(camera);
                renderer.BakeMesh(mesh, camera, false);
            }
            finally { _system.SetParticles(_originalParticles, count); }
        }

        private void Fit(Camera camera)
        {
            if (camera == null || _system == null) return;
            float distance = camera.nearClipPlane + _distanceFromNearPlane;
            float height = camera.orthographic ? camera.orthographicSize * 2
                : 2 * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            Vector3 size = new Vector3(height * camera.aspect, height, 1) * _overscan;
            var main = _system.main;
            Vector3 center = camera.transform.position + camera.transform.forward * distance;
            if (main.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                Transform simulation = main.simulationSpace == ParticleSystemSimulationSpace.Custom
                    ? main.customSimulationSpace : transform;
                if (simulation != null) center = simulation.InverseTransformPoint(center);
            }
            if (_particles == null || _particles.Length < main.maxParticles)
                _particles = new ParticleSystem.Particle[main.maxParticles];
            int count = _system.GetParticles(_particles);
            for (int i = 0; i < count; i++)
            {
                _particles[i].position = center;
                _particles[i].startSize3D = size;
            }
            _system.SetParticles(_particles, count);
        }
    }
}

#region Shared capture and Built-in pipeline
namespace PilotoStudio.ParticleEffects
{
    using System;

    public enum ParticleEffectStage { Scene = 2, FinalInk = 3, Foreground = 4 }

    /// <summary>Shared screen effects and the original Warp's Built-in scene-color capture.</summary>
    public static class PilotoSceneCapture
    {
        public static event Action<ScriptableRenderContext, Camera> PrepareSrpCamera;
        public static event Action<ScriptableRenderContext, List<Camera>> PrepareSrpContext;
        public static event Action ReleaseResources;
        private static bool _initialized;
        private static readonly Dictionary<Camera, PilotoParticleBuiltinCamera> _builtin =
            new Dictionary<Camera, PilotoParticleBuiltinCamera>();
        private static readonly Dictionary<Camera, PilotoWarpBuiltinCamera> _legacyWarp =
            new Dictionary<Camera, PilotoWarpBuiltinCamera>();
        private static readonly List<Camera> _expiredBuiltin = new List<Camera>();
        private static RenderPipelineAsset _pipeline;
        private static readonly Dictionary<Renderer, bool> _rendererStates = new Dictionary<Renderer, bool>();
        private static readonly Stack<bool> _srpScopes = new Stack<bool>();
        private static readonly HashSet<Camera> _builtinScopes = new HashSet<Camera>();
        private static int _suppressionDepth;

        public static bool HasEmitters => PilotoParticleRendering.ActiveEffectLayers != 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _pipeline = GraphicsSettings.currentRenderPipeline;
            Camera.onPreCull += PrepareBuiltin;
            Camera.onPostRender += FinishBuiltin;
            RenderPipelineManager.beginContextRendering += BeginContext;
            RenderPipelineManager.endContextRendering += EndContext;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            Application.quitting += Release;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeEditor()
        {
            EnsureInitialized();
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
            UnityEditor.EditorApplication.quitting += Release;
        }
#endif

        public static int LayersForCamera(Camera camera)
        {
            int visible = camera.cullingMask;
            return visible & PilotoParticleRendering.ActiveEffectLayers;
        }

        private static void CheckPipeline()
        {
            var current = GraphicsSettings.currentRenderPipeline;
            if (_pipeline == current) return;
            Release();
            _pipeline = current;
        }

        private static void BeginContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            CheckPipeline();
            bool active = HasEmitters;
            _srpScopes.Push(active);
            if (!active) return;
            BeginSuppression();
            PrepareSrpContext?.Invoke(context, cameras);
        }

        private static void EndContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (_srpScopes.Count != 0 && _srpScopes.Pop()) EndSuppression();
        }

        private static void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            CheckPipeline();
            if (!HasEmitters || !PilotoParticleRendering.SupportsCamera(camera, true)) return;
            PilotoParticleRendering.RefreshParticleData();
            PrepareSrpCamera?.Invoke(context, camera);
        }

        private static void PrepareBuiltin(Camera camera)
        {
            CheckPipeline();
            if (GraphicsSettings.currentRenderPipeline != null
                || !PilotoParticleRendering.SupportsCamera(camera, true)) return;
            PrepareLegacyWarp(camera);
            int layers = LayersForCamera(camera);
            if (layers == 0)
            {
                if (_builtin.TryGetValue(camera, out var unused)) { unused.Dispose(); _builtin.Remove(camera); }
                return;
            }
            _expiredBuiltin.Clear();
            foreach (var pair in _builtin)
            {
                if (pair.Key != null) continue;
                pair.Value.Dispose();
                _expiredBuiltin.Add(pair.Key);
            }
            foreach (var expired in _expiredBuiltin) _builtin.Remove(expired);
            if (_builtinScopes.Add(camera)) BeginSuppression();
            if (!_builtin.TryGetValue(camera, out var adapter))
            {
                adapter = new PilotoParticleBuiltinCamera(camera);
                _builtin[camera] = adapter;
            }
            adapter.PrepareForRendering();
        }

        private static void PrepareLegacyWarp(Camera camera)
        {
            _expiredBuiltin.Clear();
            foreach (var pair in _legacyWarp)
            {
                if (pair.Key != null) continue;
                pair.Value.Dispose();
                _expiredBuiltin.Add(pair.Key);
            }
            foreach (var expired in _expiredBuiltin) _legacyWarp.Remove(expired);

            bool required = false;
            foreach (var emitter in DistortionGrabber.Active)
            {
                if (emitter == null || !emitter.isActiveAndEnabled || !emitter.UsesLegacyWarp) continue;
                var renderer = emitter.Renderer;
                if (!renderer.enabled || renderer.forceRenderingOff
                    || (camera.cullingMask & (1 << emitter.gameObject.layer)) == 0
                    || !emitter.System.IsAlive(true)) continue;
                required = true;
                break;
            }
            if (!required)
            {
                if (_legacyWarp.TryGetValue(camera, out var unused)) { unused.Dispose(); _legacyWarp.Remove(camera); }
                return;
            }
            if (!_legacyWarp.TryGetValue(camera, out var adapter))
            {
                adapter = new PilotoWarpBuiltinCamera(camera);
                _legacyWarp.Add(camera, adapter);
            }
            adapter.PrepareForRendering();
        }

        private static void FinishBuiltin(Camera camera)
        {
            if (_builtinScopes.Remove(camera)) EndSuppression();
        }

        public static bool IsRenderingDisabled(Renderer renderer)
            => _rendererStates.TryGetValue(renderer, out bool original) ? original : renderer.forceRenderingOff;

        private static void BeginSuppression()
        {
            _suppressionDepth++;
            foreach (var emitter in DistortionGrabber.Active)
            {
                if (emitter == null || emitter.Renderer == null || emitter.UsesLegacyWarp) continue;
                var renderer = emitter.Renderer;
                if (!_rendererStates.ContainsKey(renderer)) _rendererStates.Add(renderer, renderer.forceRenderingOff);
                renderer.forceRenderingOff = true;
            }
        }

        private static void EndSuppression()
        {
            if (_suppressionDepth > 0) _suppressionDepth--;
            if (_suppressionDepth != 0) return;
            foreach (var pair in _rendererStates)
                if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            _rendererStates.Clear();
        }

        public static void Release()
        {
            _suppressionDepth = 0;
            EndSuppression();
            _srpScopes.Clear();
            _builtinScopes.Clear();
            foreach (var adapter in _builtin.Values)
            {
                adapter.Dispose();
            }
            _builtin.Clear();
            foreach (var adapter in _legacyWarp.Values) adapter.Dispose();
            _legacyWarp.Clear();
            ReleaseResources?.Invoke();
        }
    }
    /// <summary>Shared draw ordering and resource contract for the three camera integrations.</summary>
    public sealed class PilotoParticleRendering
    {
        private readonly Plane[] _planes = new Plane[6];
        private readonly List<DistortionGrabber> _draws = new List<DistortionGrabber>();
        private readonly HashSet<int> _warnedMaterials = new HashSet<int>();
        private MaterialPropertyBlock _properties;
        private Camera _camera;

        public int Count => _draws.Count;

        public static int ActiveEffectLayers
        {
            get
            {
                int layers = 0;
                foreach (var emitter in DistortionGrabber.Active)
                    if (emitter != null && emitter.isActiveAndEnabled && !emitter.UsesLegacyWarp)
                        layers |= 1 << emitter.gameObject.layer;
                return layers;
            }
        }

        public static void RefreshParticleData()
        {
            foreach (var emitter in DistortionGrabber.Active)
                if (emitter != null) emitter.UpdateParticleData();
        }

        public static bool HasStage(ParticleEffectStage stage, int layers)
        {
            foreach (var emitter in DistortionGrabber.Active)
                if (emitter != null && emitter.isActiveAndEnabled && !emitter.UsesLegacyWarp && emitter.Stage == stage
                    && (layers & (1 << emitter.gameObject.layer)) != 0) return true;
            return false;
        }

        public void Collect(Camera camera, ParticleEffectStage stage, LayerMask effectLayers)
        {
            _camera = camera;
            _draws.Clear();
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            foreach (var emitter in DistortionGrabber.Active)
            {
                if (emitter == null || !emitter.isActiveAndEnabled || emitter.UsesLegacyWarp || emitter.Stage != stage) continue;
#if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView &&
                    (UnityEditor.SceneVisibilityManager.instance.IsHidden(emitter.gameObject)
                     || (UnityEditor.Tools.visibleLayers & (1 << emitter.gameObject.layer)) == 0
                     || UnityEditor.SceneManagement.StageUtility.GetStageHandle(emitter.gameObject)
                        != UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle())) continue;
#endif
                var renderer = emitter.Renderer;
                if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null
                    || PilotoSceneCapture.IsRenderingDisabled(renderer)) continue;
                if ((effectLayers.value & (1 << emitter.gameObject.layer)) == 0) continue;
                var mesh = emitter.PrepareGeometry(camera);
                if (mesh.vertexCount == 0) continue;
                Matrix4x4 matrix = emitter.DrawMatrix;
                Vector3 extents = mesh.bounds.extents;
                Vector3 x = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
                Vector3 y = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
                Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
                var bounds = new Bounds(matrix.MultiplyPoint3x4(mesh.bounds.center), new Vector3(
                    Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2);
                if (!GeometryUtility.TestPlanesAABB(_planes, bounds)) continue;
                _draws.Add(emitter);
            }
            _draws.Sort(Compare);
        }

        private int Compare(DistortionGrabber a, DistortionGrabber b)
        {
            int order = a.CompositionGroup.CompareTo(b.CompositionGroup);
            if (order != 0) return order;
            order = SortingLayer.GetLayerValueFromID(a.Renderer.sortingLayerID)
                .CompareTo(SortingLayer.GetLayerValueFromID(b.Renderer.sortingLayerID));
            if (order != 0) return order;
            order = a.SortOrder.CompareTo(b.SortOrder);
            if (order != 0) return order;
            order = a.Renderer.sortingOrder.CompareTo(b.Renderer.sortingOrder);
            if (order != 0) return order;
            // Explicit renderer ordering; each ParticleSystem retains its own particle sort mode.
            float da = Vector3.Dot(a.Renderer.bounds.center - _camera.transform.position, _camera.transform.forward);
            float db = Vector3.Dot(b.Renderer.bounds.center - _camera.transform.position, _camera.transform.forward);
            order = db.CompareTo(da);
            return order != 0 ? order : a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        public void DrawGroup(CommandBuffer commands, int group, string passName)
        {
            if (_properties == null) _properties = new MaterialPropertyBlock();
            foreach (var emitter in _draws)
            {
                if (emitter.CompositionGroup != group) continue;
                var material = emitter.Renderer.sharedMaterial;
                int pass = material.FindPass(passName);
                if (pass < 0)
                {
                    if (_warnedMaterials.Add(material.GetInstanceID()))
                        Debug.LogWarning($"Particle material '{material.name}' has no '{passName}' pass. "
                            + "Check that its Shader Graph target imported for the active pipeline.", material);
                    continue;
                }
                emitter.Renderer.GetPropertyBlock(_properties);
                commands.DrawMesh(emitter.DrawGeometry(_camera), emitter.DrawMatrix,
                    material, 0, pass, _properties);
                emitter.MarkDrawn();
            }
        }

        public bool HasGroup(int group)
        {
            foreach (var emitter in _draws)
                if (emitter.CompositionGroup == group) return true;
            return false;
        }

        public bool NeedsFilteredCapture(int group = -1)
        {
            foreach (var emitter in _draws)
                if ((group < 0 || emitter.CompositionGroup == group)
                    && emitter.Renderer.sharedMaterial.HasProperty("_BlurWidthPixels")) return true;
            return false;
        }

        public static void BindCaptureMips(CommandBuffer commands, int width, int height, bool filtered)
        {
            commands.SetGlobalFloat("_PilotoSceneMaxMip", filtered
                ? Mathf.Floor(Mathf.Log(Mathf.Max(width, height), 2)) : 0);
        }

        public static bool SupportsCamera(Camera camera, bool sceneView)
        {
            return camera != null && !camera.stereoEnabled &&
                (camera.cameraType == CameraType.Game || (sceneView && camera.cameraType == CameraType.SceneView));
        }

        public static void Bind(CommandBuffer commands, ParticleEffectStage stage,
            Matrix4x4 gpuViewProjection, int width, int height, Vector4 colorScaleBias)
        {
            commands.SetGlobalMatrix("_PilotoViewProjection", gpuViewProjection);
            commands.SetGlobalVector("_PilotoSceneTexelSize", new Vector4(1f / Mathf.Max(1, width),
                1f / Mathf.Max(1, height), width, height));
            commands.SetGlobalVector("_PilotoSceneScaleBias", colorScaleBias);
            commands.SetGlobalFloat("_PilotoDrawStage", (int)stage);
            commands.SetGlobalFloat("_PilotoCaptureAvailable", 1);
        }

        public static void End(CommandBuffer commands)
        {
            commands.SetGlobalFloat("_PilotoDrawStage", 0);
            commands.SetGlobalFloat("_PilotoCaptureAvailable", 0);
        }
    }

    internal sealed class PilotoWarpBuiltinCamera : IDisposable
    {
        private static readonly int WARP_COPY = Shader.PropertyToID("_PilotoLegacyWarpCopy");
        private static readonly int OPAQUE_TEXTURE = Shader.PropertyToID("_CameraOpaqueTexture");
        private readonly Camera _camera;
        private readonly CommandBuffer _capture;
        private readonly CommandBuffer _cleanup;

        public PilotoWarpBuiltinCamera(Camera camera)
        {
            _camera = camera;
            _capture = new CommandBuffer { name = "Piloto Warp opaque scene" };
            _cleanup = new CommandBuffer { name = "Piloto Warp capture cleanup" };
            _cleanup.ReleaseTemporaryRT(WARP_COPY);
            _camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _capture);
            _camera.AddCommandBuffer(CameraEvent.AfterEverything, _cleanup);
            _camera.depthTextureMode |= DepthTextureMode.Depth;
        }

        public void PrepareForRendering()
        {
            _capture.Clear();
            // Match the original grabber's bilinear, default-format capture before transparents.
            _capture.GetTemporaryRT(WARP_COPY, Mathf.Max(1, _camera.pixelWidth),
                Mathf.Max(1, _camera.pixelHeight), 0, FilterMode.Bilinear);
            _capture.Blit(BuiltinRenderTextureType.CurrentActive, WARP_COPY);
            _capture.SetGlobalTexture(OPAQUE_TEXTURE, WARP_COPY);
        }

        public void Dispose()
        {
            if (_camera != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _capture);
                _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _cleanup);
            }
            _capture.Release();
            _cleanup.Release();
        }
    }

    internal sealed class PilotoParticleBuiltinCamera : IDisposable
    {
        private Camera _camera;
        private CommandBuffer _scene;
        private CommandBuffer _ink;
        private readonly PilotoParticleRendering _draws = new PilotoParticleRendering();
        private bool _oldForceTexture;
        private static readonly int SCENE_COPY = Shader.PropertyToID("_PilotoBuiltinSceneCopy");
        public PilotoParticleBuiltinCamera(Camera camera)
        {
            _camera = camera;
            if (GraphicsSettings.currentRenderPipeline != null || _scene != null) return;
            _oldForceTexture = _camera.forceIntoRenderTexture;
            _camera.forceIntoRenderTexture = true;
            _scene = new CommandBuffer { name = "Piloto transparent scene effects" };
            _ink = new CommandBuffer { name = "Piloto final ink" };
            _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _scene);
            _camera.AddCommandBuffer(CameraEvent.AfterImageEffects, _ink);
        }

        public void PrepareForRendering()
        {
            if (_scene == null) return;
            _scene.Clear(); _ink.Clear();
            if (GraphicsSettings.currentRenderPipeline != null || !PilotoParticleRendering.SupportsCamera(_camera, true)) return;
            PilotoParticleRendering.RefreshParticleData();
            Record(_scene, ParticleEffectStage.Scene, true);
            Record(_scene, ParticleEffectStage.Foreground, false);
            Record(_ink, ParticleEffectStage.FinalInk, true);
        }

        private void Record(CommandBuffer commands, ParticleEffectStage stage, bool capture)
        {
            _draws.Collect(_camera, stage, PilotoSceneCapture.LayersForCamera(_camera));
            if (_draws.Count == 0) return;
            int width = Mathf.Max(1, _camera.pixelWidth);
            int height = Mathf.Max(1, _camera.pixelHeight);
            var format = _camera.allowHDR && stage != ParticleEffectStage.FinalInk
                ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            bool filtered = capture && _draws.NeedsFilteredCapture();
            if (filtered) format = RenderTextureFormat.ARGBHalf;
            if (capture)
            {
                var descriptor = new RenderTextureDescriptor(width, height, format, 0)
                {
                    msaaSamples = 1, useMipMap = filtered, autoGenerateMips = false
                };
                commands.GetTemporaryRT(SCENE_COPY, descriptor, filtered ? FilterMode.Trilinear : FilterMode.Bilinear);
            }
            var vp = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, true) * _camera.worldToCameraMatrix;
            PilotoParticleRendering.Bind(commands, stage, vp, _camera.pixelWidth, _camera.pixelHeight,
                new Vector4(1, 1, 0, 0));
            for (int group = 0; group <= 3; group++)
            {
                if (!_draws.HasGroup(group)) continue;
                if (capture)
                {
                    // Blit resolves the source into a single-sample texture; never sample CameraTarget while writing it.
                    commands.Blit(BuiltinRenderTextureType.CameraTarget, SCENE_COPY);
                    bool filterGroup = _draws.NeedsFilteredCapture(group);
                    if (filterGroup) commands.GenerateMips(new RenderTargetIdentifier(SCENE_COPY));
                    PilotoParticleRendering.BindCaptureMips(commands, width, height, filterGroup);
                    commands.SetGlobalTexture("_PilotoSceneTexture", SCENE_COPY);
                }
                commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                commands.SetViewport(_camera.pixelRect);
                _draws.DrawGroup(commands, group, "Pass");
            }
            PilotoParticleRendering.End(commands);
            if (capture) commands.ReleaseTemporaryRT(SCENE_COPY);
        }

        public void Dispose()
        {
            if (_camera != null && _scene != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _scene);
                _camera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, _ink);
                _camera.forceIntoRenderTexture = _oldForceTexture;
            }
            _scene?.Release(); _ink?.Release();
            _scene = null; _ink = null;
        }
    }
}
#endregion

#if PILOTO_HAS_URP
namespace PilotoStudio.ParticleEffects.Universal
{
    using UnityEngine.Rendering.Universal;

    internal static class PilotoParticleUrpCapture
    {
        private static readonly Dictionary<Camera, EffectPass[]> _cameras = new Dictionary<Camera, EffectPass[]>();
        private static readonly List<Camera> _expired = new List<Camera>();
        private static bool _initialized;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            PilotoSceneCapture.EnsureInitialized();
            PilotoSceneCapture.PrepareSrpCamera += Prepare;
            PilotoSceneCapture.ReleaseResources += Release;
        }

        private static void Prepare(ScriptableRenderContext context, Camera camera)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)) return;
            _expired.Clear();
            foreach (var pair in _cameras)
            {
                if (pair.Key != null) continue;
                foreach (var pass in pair.Value) pass.Dispose();
                _expired.Add(pair.Key);
            }
            foreach (var expired in _expired) _cameras.Remove(expired);
            int layers = PilotoSceneCapture.LayersForCamera(camera);
            if (layers == 0) return;
            var additional = camera.GetComponent<UniversalAdditionalCameraData>();
            ScriptableRenderer renderer = additional != null ? additional.scriptableRenderer : asset.scriptableRenderer;
            if (!(renderer is UniversalRenderer)) return;
            if (!_cameras.TryGetValue(camera, out var passes))
            {
                passes = new[]
                {
                    new EffectPass(ParticleEffectStage.Scene, (RenderPassEvent)501),
                    new EffectPass(ParticleEffectStage.Foreground, (RenderPassEvent)502),
                    // URP14 Scene View disables final AA. Event 600 retains the post-process intermediate.
                    new EffectPass(ParticleEffectStage.FinalInk, camera.cameraType == CameraType.SceneView
                        ? RenderPassEvent.AfterRenderingPostProcessing : (RenderPassEvent)1002)
                };
                _cameras.Add(camera, passes);
            }
            // Called before renderer setup. ConfigureInput(Color) requests an intermediate even with zero features.
            foreach (var pass in passes)
            {
                if (!PilotoParticleRendering.HasStage(pass.Stage, layers)) continue;
                pass.ConfigureSettings(layers, 1);
                renderer.EnqueuePass(pass);
            }
        }

        private static void Release()
        {
            foreach (var passes in _cameras.Values)
                foreach (var pass in passes) pass.Dispose();
            _cameras.Clear();
        }

        private sealed class EffectPass : ScriptableRenderPass
        {
            private readonly ParticleEffectStage _stage;
            private readonly PilotoParticleRendering _draws = new PilotoParticleRendering();
            private LayerMask _layers;
            private int _downsample;
            private RTHandle _copy, _resolved;
            private bool _warnedFinalTarget, _warnedIntermediate;
            public ParticleEffectStage Stage => _stage;

            public EffectPass(ParticleEffectStage stage, RenderPassEvent injection)
            {
                _stage = stage;
                renderPassEvent = injection;
                bool needsColor = stage == ParticleEffectStage.Scene || stage == ParticleEffectStage.FinalInk;
                ConfigureInput(needsColor ? ScriptableRenderPassInput.Color : ScriptableRenderPassInput.None);
            }

            public void ConfigureSettings(LayerMask layers, int downsample)
            {
                _layers = layers;
                _downsample = Mathf.Max(1, downsample);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var data = renderingData.cameraData;
                var camera = data.camera;
                if (!data.resolveFinalTarget) return;
                _draws.Collect(camera, _stage, _layers);
                if (_draws.Count == 0) { Dispose(); return; }
                bool final = _stage == ParticleEffectStage.FinalInk;
                bool finalOutput = final && !data.isSceneViewCamera;
                bool capture = final || _stage == ParticleEffectStage.Scene;
                bool filtered = capture && _draws.NeedsFilteredCapture();
                var renderer = data.renderer;
                RTHandle source = renderer.cameraColorTargetHandle;
                RTHandle finalHandle = null;
                if (finalOutput)
                {
                    if (data.targetTexture == null)
                    {
                        if (!_warnedFinalTarget)
                            Debug.LogWarning("Piloto Final Ink needs a camera output RenderTexture so it can sample "
                                + "the resolved image after final AA. Assign the camera output to a RenderTexture and display it with your existing output UI.", camera);
                        _warnedFinalTarget = true;
                        return;
                    }
                    // The Texture overload borrows the output. The RenderTexture overload owns and destroys it on Release.
                    finalHandle = RTHandles.Alloc((Texture)data.targetTexture);
                    source = finalHandle;
                }
                RenderTexture sourceTexture = finalOutput ? data.targetTexture : source.rt;
                if (capture && sourceTexture == null)
                {
                    if (!_warnedIntermediate)
                        Debug.LogWarning("Piloto shared capture could not obtain a readable camera color texture. Use the Universal Renderer (URP 14).", camera);
                    _warnedIntermediate = true;
                    finalHandle?.Release();
                    return;
                }
                var commands = CommandBufferPool.Get("Piloto " + _stage);
                try
                {
                    var descriptor = finalOutput ? data.targetTexture.descriptor : data.cameraTargetDescriptor;
                    int fullWidth = descriptor.width, fullHeight = descriptor.height;
                    descriptor.depthBufferBits = 0;
                    descriptor.msaaSamples = 1;
                    descriptor.bindMS = false;
                    descriptor.useMipMap = false;
                    descriptor.autoGenerateMips = false;
                    if (capture)
                    {
                        if (sourceTexture.antiAliasing > 1)
                        {
                            RenderingUtils.ReAllocateIfNeeded(ref _resolved, descriptor, FilterMode.Bilinear,
                                TextureWrapMode.Clamp, name: "Piloto resolved scene");
                        }
                        descriptor.width = Mathf.Max(1, fullWidth / _downsample);
                        descriptor.height = Mathf.Max(1, fullHeight / _downsample);
                        descriptor.useMipMap = filtered;
                        descriptor.mipCount = filtered ? Texture.GenerateAllMips : 1;
                        if (filtered) descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                        RenderingUtils.ReAllocateIfNeeded(ref _copy, descriptor, filtered ? FilterMode.Trilinear : FilterMode.Bilinear,
                            TextureWrapMode.Clamp, name: "Piloto scene snapshot");
                        // The first copy is recorded below so each composition group sees its predecessor.
                    }
                    var vp = data.GetGPUProjectionMatrix() * data.GetViewMatrix();
                    PilotoParticleRendering.Bind(commands, _stage, vp, fullWidth, fullHeight,
                        new Vector4(1, 1, 0, 0));
                    for (int group = 0; group <= 3; group++)
                    {
                        if (!_draws.HasGroup(group)) continue;
                        if (capture)
                        {
                            RTHandle resolved = source;
                            if (sourceTexture.antiAliasing > 1)
                            {
                                commands.ResolveAntiAliasedSurface(sourceTexture, _resolved.rt);
                                resolved = _resolved;
                            }
                            Blitter.BlitCameraTexture(commands, resolved, _copy, 0, true);
                            bool filterGroup = _draws.NeedsFilteredCapture(group);
                            if (filterGroup) commands.GenerateMips(_copy.rt);
                            PilotoParticleRendering.BindCaptureMips(commands, descriptor.width, descriptor.height, filterGroup);
                            commands.SetGlobalTexture("_PilotoSceneTexture", _copy.nameID);
                        }
                        if (final)
                        {
                            // Final ink uses the output's depth attachment if provided; screen-filling ink ignores it.
                            CoreUtils.SetRenderTarget(commands, source, ClearFlag.None);
                        }
                        else
                        {
                            CoreUtils.SetRenderTarget(commands, source, renderer.cameraDepthTargetHandle, ClearFlag.None);
                        }
                        _draws.DrawGroup(commands, group, "Universal Forward");
                    }
                    PilotoParticleRendering.End(commands);
                    context.ExecuteCommandBuffer(commands);
                }
                finally
                {
                    CommandBufferPool.Release(commands);
                    finalHandle?.Release();
                }
            }

            public void Dispose()
            {
                _copy?.Release(); _resolved?.Release();
                _copy = null; _resolved = null;
            }
        }
    }
}
#endif

#if PILOTO_HAS_HDRP
namespace PilotoStudio.ParticleEffects.HighDefinition
{
    using UnityEngine.Rendering.HighDefinition;
    using UnityEngine.Experimental.Rendering;

    internal static class PilotoParticleHdrpCapture
    {
        private static readonly List<GameObject> _volumes = new List<GameObject>();
        private static bool _initialized;
        public static bool Active => _volumes.Count != 0 && _volumes[0] != null;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            PilotoSceneCapture.EnsureInitialized();
            PilotoSceneCapture.PrepareSrpContext += Prepare;
            PilotoSceneCapture.ReleaseResources += Release;
        }

        private static void Prepare(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset) || Active) return;
            Release();
            Add(CustomPassInjectionPoint.BeforePostProcess, ParticleEffectStage.Scene, ParticleEffectStage.Foreground);
            Add(CustomPassInjectionPoint.AfterPostProcess, ParticleEffectStage.FinalInk);
        }

        private static void Add(CustomPassInjectionPoint injection, params ParticleEffectStage[] stages)
        {
            var host = new GameObject("Piloto shared capture") { hideFlags = HideFlags.HideAndDontSave };
            if (Application.isPlaying) Object.DontDestroyOnLoad(host);
            var volume = host.AddComponent<CustomPassVolume>();
            volume.isGlobal = true;
            volume.injectionPoint = injection;
            foreach (var stage in stages)
            {
                var pass = new PilotoParticleCustomPass();
                pass.Configure(stage);
                volume.customPasses.Add(pass);
            }
            _volumes.Add(host);
        }

        private static void Release()
        {
            foreach (var host in _volumes)
            {
                if (host == null) continue;
                host.SetActive(false);
                if (Application.isPlaying) Object.Destroy(host);
                else Object.DestroyImmediate(host);
            }
            _volumes.Clear();
        }
    }
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true,
        sourceNamespace: "PilotoStudio.ParticleEffects.HighDefinition",
        sourceAssembly: "Piloto.ParticleEffects.HDRP", sourceClassName: "PilotoParticleCustomPass")]
    internal sealed class PilotoParticleCustomPass : CustomPass
    {
        [SerializeField] private ParticleEffectStage _stage = ParticleEffectStage.Scene;
        private readonly PilotoParticleRendering _draws = new PilotoParticleRendering();
        private RTHandle _copy, _resolved;

        public void Configure(ParticleEffectStage stage)
        {
            _stage = stage;
            name = "Piloto " + stage;
            targetColorBuffer = TargetBuffer.Camera;
            targetDepthBuffer = TargetBuffer.Camera;
        }

        protected override void Execute(CustomPassContext ctx)
        {
            var camera = ctx.hdCamera.camera;
            if (!PilotoParticleRendering.SupportsCamera(camera, true)) return;
            int layers = PilotoSceneCapture.LayersForCamera(camera);
            _draws.Collect(camera, _stage, layers);
            if (_draws.Count == 0) { ReleaseBuffers(); return; }
            bool capture = _stage == ParticleEffectStage.Scene || _stage == ParticleEffectStage.FinalInk;
            bool filtered = capture && _draws.NeedsFilteredCapture();
            var source = ctx.cameraColorBuffer;
            int width = _stage == ParticleEffectStage.FinalInk
                ? Mathf.RoundToInt(ctx.hdCamera.postProcessScreenSize.x) : ctx.hdCamera.actualWidth;
            int height = _stage == ParticleEffectStage.FinalInk
                ? Mathf.RoundToInt(ctx.hdCamera.postProcessScreenSize.y) : ctx.hdCamera.actualHeight;
            var format = filtered ? GraphicsFormat.R16G16B16A16_SFloat : source.rt.graphicsFormat;
            if (capture)
            {
                Ensure(ref _copy, Mathf.Max(1, width),
                    Mathf.Max(1, height), format, "Piloto HDRP snapshot", filtered);
                if (source.rt.antiAliasing > 1)
                    Ensure(ref _resolved, source.rt.width, source.rt.height, source.rt.graphicsFormat, "Piloto HDRP resolve");
            }
            // HDRP's view matrix may be camera-relative. Build an absolute-world VP using
            // the exact jittered GPU projection supplied by this HDCamera.
            Matrix4x4 vp = ctx.hdCamera.mainViewConstants.projMatrix * camera.worldToCameraMatrix;
            PilotoParticleRendering.Bind(ctx.cmd, _stage, vp, width, height,
                new Vector4(1, 1, 0, 0));
            for (int group = 0; group <= 3; group++)
            {
                if (!_draws.HasGroup(group)) continue;
                if (capture)
                {
                    if (source.rt.antiAliasing > 1)
                    {
                        ctx.cmd.ResolveAntiAliasedSurface(source.rt, _resolved.rt);
                        CustomPassUtils.Copy(ctx, _resolved, _copy,
                            new Vector4((float)width / _resolved.rt.width, (float)height / _resolved.rt.height, 0, 0),
                            new Vector4(1, 1, 0, 0));
                    }
                    else CustomPassUtils.Copy(ctx, source, _copy);
                    bool filterGroup = _draws.NeedsFilteredCapture(group);
                    if (filterGroup) ctx.cmd.GenerateMips(_copy.rt);
                    PilotoParticleRendering.BindCaptureMips(ctx.cmd, _copy.rt.width, _copy.rt.height, filterGroup);
                    ctx.cmd.SetGlobalTexture("_PilotoSceneTexture", _copy.nameID);
                }
                CoreUtils.SetRenderTarget(ctx.cmd, source, ctx.cameraDepthBuffer, ClearFlag.None);
                _draws.DrawGroup(ctx.cmd, group, "ForwardOnly");
            }
            PilotoParticleRendering.End(ctx.cmd);
        }

        private static void Ensure(ref RTHandle handle, int width, int height, GraphicsFormat format, string bufferName, bool filtered = false)
        {
            if (handle != null && handle.rt.width == width && handle.rt.height == height
                && handle.rt.graphicsFormat == format && handle.rt.useMipMap == filtered) return;
            handle?.Release();
            // HDRP uses array-backed TEXTURE2D_X even for a mono camera on desktop APIs.
            handle = RTHandles.Alloc(width, height, slices: TextureXR.slices, dimension: TextureXR.dimension,
                colorFormat: format, filterMode: filtered ? FilterMode.Trilinear : FilterMode.Bilinear,
                useMipMap: filtered, autoGenerateMips: false,
                wrapMode: TextureWrapMode.Clamp, name: bufferName);
        }

        private void ReleaseBuffers()
        {
            _copy?.Release(); _resolved?.Release();
            _copy = null; _resolved = null;
        }

        protected override void Cleanup() => ReleaseBuffers();
    }
}
#endif
