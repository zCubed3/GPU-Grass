/*
 * MIT License
 * 
 * Copyright (c) 2023 zCubed3
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
*/

using System.Collections;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace zCubed.Flora
{
    // Define platform configuration settings in a GrassConfiguration object and assign it onto this

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GrassRenderer : MonoBehaviour
    {
        public struct GrassInstance
        {
            public static readonly int Size = sizeof(float) * 36;

            public Matrix4x4 model;
            public Matrix4x4 modelInverse;
            public Vector4 color;
        }

        public enum EncoderPrecision
        {
            Half, Float
        }

        public enum ColorMode
        {
            EncodedColor,
            RandomHSV
        }

        protected class TimeSample
        {
            public TimeSample(string name)
            {
                this.name = name;

                stopwatch = new Stopwatch();
                stopwatch?.Start();
            }

            public void Stop()
            {
                stopwatch?.Stop();
                elapsedMS = stopwatch.ElapsedMilliseconds;
                elapsedNS = (stopwatch.ElapsedTicks / Stopwatch.Frequency) * 1000000000;
            }

            protected Stopwatch stopwatch;

            public string name = "Unknown";
            public long elapsedNS = 0;
            public long elapsedMS = 0;
        }

        [Header("Data")]
        [Tooltip("The grass system configuration")]
        public GrassConfiguration config;

        [Header("Presentation")]
        [Tooltip("Mesh used for rendering the grass patches")]
        [FormerlySerializedAs("mesh")]
        public Mesh patchMesh;

        [Tooltip("Material used for rendering the grass patches")]
        [FormerlySerializedAs("material")]
        public Material patchMaterial;

        [Tooltip("How are colors determined?")]
        public ColorMode colorMode = ColorMode.EncodedColor;

        [Tooltip("The transform that is used for culling / fading grass")]
        public Transform cullTransform;

        [Header("GPU Placement Info")]
        [FormerlySerializedAs("cellDepthMapSize")]
        [Tooltip("The resolution of the combined depth+normals map used for GPU placement")]
        public int cellDepthNormalsResolution = 256; // TODO: Move this to GrassConfiguration?

        [Tooltip("The resolution of the obstacle map used for GPU distortion")]
        public int cellObstaclesResolution = 256;

        [Tooltip("The resolution of the biome color map used for GPU placement")]
        public int cellBiomeColorsResolution = 256;

        [Tooltip("How high up is the encoder? (from 0)")]
        public float cellEncoderHeight = 10.0F;

        [Tooltip("Near plane of the encoder's camera (usually set above the highest point of terrain)")]
        [FormerlySerializedAs("cellEncoderNearPlane")]
        public float cellEncoderNearPlane = 0.01F;

        [Tooltip("Far plane of the encoder's camera (usually set below the lowest point of terrain)")]
        [FormerlySerializedAs("cellEncoderFarPlane")]
        public float cellEncoderFarPlane = 100.0F;

        [Tooltip("How offset is the encoder from the cull transform?")]
        public Vector3 cullEncoderOffset = Vector3.zero;

        [Tooltip("How offset is the the cull transform in patch shaders?")]
        public Vector3 cullShaderOffset = Vector3.zero;

        [Header("Lag Compensation")]
        [Tooltip("Minimum FPS before lag compensation takes effect")]
        public float lagMinFPS = 45;

        [Tooltip("The minimum we start jittering placement to compensate for lag")]
        public float lagCompMin = 0.00F;

        [Tooltip("The maximum we start jittering placement to compensate for lag")]
        public float lagCompMax = 0.05F;

        [Tooltip("How precise are the encoder textures? (Note: Float might be slow on mobile!)")]
        public EncoderPrecision encoderPrecision = EncoderPrecision.Half;

        [Header("Debug")]
        [Tooltip("Show renderer debug window in the corner?")]
        public bool showDebugWindow = false;

        [Tooltip("Show gizmos on selected only?")]
        public bool doGizmosOnSelected = true;

        [Tooltip("Pauses the encoder ofc!")]
        public bool pauseEncoder = false;

        [Tooltip("Enables profiling features")]
        public bool measureExecutionTime = false;

        //
        // General rendering data
        //
        [System.NonSerialized]
        protected ComputeBuffer grassBuffer;

        [System.NonSerialized]
        protected ComputeBuffer grassIndirectArgs;

        protected uint[] indirectArgs = { 0, 0, 0, 0, 0 };

        [System.NonSerialized]
        protected CommandBuffer gpuDataCmdBuffer = null;

        public static Mesh QuadMesh { get; protected set; }

        //
        // Displacement
        //
        public static List<GrassObstacle> Obstacles = new List<GrassObstacle>();

        [System.NonSerialized]
        protected RenderTexture obstacleMap;

        //
        // Encoding
        //
        public static List<GrassSurface> Surfaces = new List<GrassSurface>();

        [System.NonSerialized]
        protected Texture2D lastLUT = null;

        [System.NonSerialized]
        protected ComputeShader placerCS = null;

        [System.NonSerialized]
        protected Shader encodePS = null;

        [System.NonSerialized]
        protected GameObject encodeGO;

        [System.NonSerialized]
        protected Camera encodeCamera;

        [System.NonSerialized]
        protected RenderTexture encodeGrassMap;

        [System.NonSerialized]
        protected RenderTexture encodeBiomeMap;

        [System.NonSerialized]
        protected int kernelIndex = -1;

        [System.NonSerialized]
        protected int kernelWidth = 512;

        [System.NonSerialized]
        protected bool firstFrame = true;

        protected float? _uniqueSeed = null;

        protected float UniqueSeed
        {
            get
            {
                if (_uniqueSeed == null)
                    _uniqueSeed = Random.Range(0F, 100F);

                return _uniqueSeed.Value;
            }
        }

        //
        // Window stuff
        //
        protected int? _uniqueID = null;

        protected int UniqueID
        {
            get
            {
                if (_uniqueID == null)
                    _uniqueID = Random.Range(int.MinValue, int.MaxValue);

                return _uniqueID.Value;
            }
        }

        [System.NonSerialized]
        protected Rect windowRect = Rect.zero;

        [System.NonSerialized]
        protected List<TimeSample> executionTimes = new List<TimeSample>();

        //
        // Static data
        //
        public static GrassRenderer Singleton { get; protected set; }


        protected RenderTextureFormat GetFormat()
        {
            switch (encoderPrecision)
            {
                case EncoderPrecision.Float:
                    return RenderTextureFormat.ARGBFloat;

                default:
                    return RenderTextureFormat.ARGBHalf;
            }
        }

        public void VerifyData()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // https://forum.unity.com/threads/modify-always-included-shaders-with-pre-processor.509479/
                // Validate all the shaders we require are included
                GraphicsSettings graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
                SerializedObject serializedObject = new SerializedObject(graphicsSettingsObj);
                SerializedProperty arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

                // Collect our required shaders first
                List<Shader> requiredShaders = new List<Shader>();

                foreach (GrassSurface surface in Surfaces)
                    if (!requiredShaders.Contains(surface.EncodeShader))
                        requiredShaders.Add(surface.EncodeShader);

                foreach (GrassObstacle obstacle in Obstacles)
                    if (!requiredShaders.Contains(obstacle.obstacleMaterial.shader))
                        requiredShaders.Add(obstacle.obstacleMaterial.shader);

                // Make sure they exist
                for (int s = 0; s < arrayProp.arraySize; s++)
                {
                    SerializedProperty elem = arrayProp.GetArrayElementAtIndex(s);

                    Shader shader = elem.objectReferenceValue as Shader;

                    if (requiredShaders.Contains(shader))
                        requiredShaders.Remove(shader);
                }

                // If not, ask the user if they want to submit it
                if (requiredShaders.Count > 0)
                {
                    string message = "The following shaders were missing from always included";

                    foreach (Shader shader in requiredShaders)
                        message += $"; {shader.name}";

                    if (EditorUtility.DisplayDialog("Force Include Shaders?", message, "Add", "Cancel"))
                    {
                        foreach (Shader shader in requiredShaders)
                        {
                            int index = arrayProp.arraySize;
                            arrayProp.InsertArrayElementAtIndex(index);

                            SerializedProperty elem = arrayProp.GetArrayElementAtIndex(index);
                            elem.objectReferenceValue = shader;
                        }

                        serializedObject.ApplyModifiedProperties();
                        AssetDatabase.SaveAssets();
                    }
                }
            }
#endif

            if (QuadMesh == null)
            {
                QuadMesh = new Mesh();

                QuadMesh.vertices = new Vector3[]
                {
                    new Vector3(-1, 0, -1),
                    new Vector3(-1, 0, 1),
                    new Vector3(1, 0, -1),
                    new Vector3(1, 0, 1)
                };

                QuadMesh.uv = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 0),
                    new Vector2(1, 1)
                };

                QuadMesh.triangles = new int[] { 0, 1, 2, 2, 1, 3 };

                QuadMesh.RecalculateNormals();
                QuadMesh.RecalculateTangents();
                QuadMesh.Optimize();
            }

            if (grassBuffer != null)
            {
                if (grassBuffer.count != config.GrassCount)
                {
                    grassBuffer.Release();
                    grassBuffer = null;
                }
            }

            if (gpuDataCmdBuffer == null)
            {
                gpuDataCmdBuffer = new CommandBuffer();
            }

            if (placerCS == null)
            {
                placerCS = Resources.Load<ComputeShader>("GrassPlacer");
            }

            if (encodePS == null)
            {
                encodePS = Shader.Find("zCubed/Grass/Encoders/Default");
            }

            if (obstacleMap == null)
            {
                obstacleMap = new RenderTexture(cellObstaclesResolution, cellObstaclesResolution, 24, GetFormat(), RenderTextureReadWrite.Linear);
                obstacleMap.Create();
            }

            if (encodeGrassMap == null)
            {
                encodeGrassMap = new RenderTexture(cellDepthNormalsResolution, cellDepthNormalsResolution, 0, GetFormat(), RenderTextureReadWrite.Linear);
                encodeGrassMap.Create();
            }

            if (encodeBiomeMap == null)
            {
                encodeBiomeMap = new RenderTexture(cellBiomeColorsResolution, cellBiomeColorsResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                encodeBiomeMap.Create();
            }

            if (encodeGO == null)
            {
                encodeGO = new GameObject("--[[GRASS SYSTEM ENCODER]]--");
                encodeGO.hideFlags = HideFlags.HideAndDontSave;

                encodeGO.transform.eulerAngles = Vector3.right * 90;

                encodeCamera = encodeGO.AddComponent<Camera>();
                encodeCamera.orthographic = true;
                encodeCamera.orthographicSize = config.CullDistance;
                encodeCamera.nearClipPlane = cellEncoderNearPlane;
                encodeCamera.farClipPlane = cellEncoderFarPlane;
                encodeCamera.targetTexture = encodeGrassMap;
                encodeCamera.clearFlags = CameraClearFlags.Nothing;
                encodeCamera.cullingMask = 0;
                encodeCamera.useOcclusionCulling = false;
            }

            if (grassIndirectArgs == null)
            {
#if UNITY_EDITOR && GRASS_LOGGING
                Debug.Log("Allocating IndirectArguments buffer!");
#endif

                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
                    grassIndirectArgs = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments, ComputeBufferMode.SubUpdates);
                else
                    grassIndirectArgs = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            }

            if (grassBuffer == null)
            {
#if UNITY_EDITOR && GRASS_LOGGING
                Debug.Log("Allocating Grass StructuredBuffer!");
#endif

                grassBuffer = new ComputeBuffer(config.GrassCount, GrassInstance.Size, ComputeBufferType.Structured);
            }
        }

        public void Cleanup()
        {
#if GRASS_LOGGING
            print("Cleaning up grass!");
#endif

            if (gpuDataCmdBuffer != null)
            {
                gpuDataCmdBuffer.Release();
                gpuDataCmdBuffer = null;
            }

            if (obstacleMap != null)
            {
                obstacleMap.Release();
                obstacleMap = null;
            }

            if (encodeGrassMap != null)
            {
                encodeGrassMap.Release();
                encodeGrassMap = null;
            }

            if (encodeGO != null)
            {
                if (Application.isPlaying)
                    Destroy(encodeGO);
                else
                    DestroyImmediate(encodeGO);

                encodeGO = null;
                encodeCamera = null;
            }

            if (grassIndirectArgs != null)
            {
                grassIndirectArgs.Release();
                grassIndirectArgs = null;
            }

            if (grassBuffer != null)
            {
                grassBuffer.Release();
                grassBuffer = null;
            }
        }

        public void UpdateIndirectArgs()
        {
#if UNITY_EDITOR && GRASS_LOGGING
            Debug.Log("Updating IndexedIndirect arguments...");
#endif

            VerifyData();

            indirectArgs[0] = patchMesh.GetIndexCount(0);
            indirectArgs[1] = (uint)config.GrassCount;
            indirectArgs[2] = patchMesh.GetIndexStart(0);
            indirectArgs[3] = patchMesh.GetBaseVertex(0);

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
            {
                var nativeArray = grassIndirectArgs.BeginWrite<uint>(0, 5);

                for (int a = 0; a < 5; a++)
                    nativeArray[a] = indirectArgs[a];

                grassIndirectArgs.EndWrite<uint>(5);
            }
            else
                grassIndirectArgs.SetData(indirectArgs);
        }

        public void UpdateCamera(bool forceRender = false, bool record = true)
        {
            encodeCamera.stereoTargetEye = StereoTargetEyeMask.None;
            encodeCamera.orthographic = true;
            encodeCamera.orthographicSize = config.CullDistance;
            encodeCamera.nearClipPlane = cellEncoderNearPlane;
            encodeCamera.farClipPlane = cellEncoderFarPlane;

            if (record)
            {
                encodeCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, gpuDataCmdBuffer);

                gpuDataCmdBuffer.Clear();

                RenderTargetIdentifier[] mrtColors = new RenderTargetIdentifier[]
                {
                    obstacleMap.colorBuffer,
                    encodeGrassMap.colorBuffer,
                    encodeBiomeMap.colorBuffer
                };

                RenderTargetIdentifier mrtDepth = obstacleMap.depthBuffer;

                gpuDataCmdBuffer.SetRenderTarget(mrtColors, mrtDepth);
                gpuDataCmdBuffer.ClearRenderTarget(RTClearFlags.Color | RTClearFlags.Depth, new Color(0, 0, 0, 1), 0, 0);

                foreach (GrassObstacle obstacle in Obstacles)
                {
                    if (obstacle == null)
                        continue;

                    if (obstacle.displaceTexture == null)
                        continue;

                    Matrix4x4 mat = obstacle.GetTRS();
                    gpuDataCmdBuffer.DrawMesh(QuadMesh, mat, obstacle.obstacleMaterial, 0, 0);
                }

                foreach (GrassSurface surface in Surfaces)
                {
                    if (surface == null)
                        continue;

                    if (surface.surfaceRenderer == null || surface.encodeMaterial == null)
                        continue;

                    int pass = surface.materialIndex >= 0 ? 0 : 1;
                    gpuDataCmdBuffer.DrawRenderer(surface.surfaceRenderer, surface.encodeMaterial, 0, pass);
                }

                Vector4 grassState = Vector4.zero;
                grassState.x = 0;
                grassState.y = config.surfaceMaterials.Count - 1;
                grassState.z = config.lutGradientResolution - 1;
                grassState.w = config.GrassCount;

                gpuDataCmdBuffer.SetGlobalVector("_GrassState", grassState);
                gpuDataCmdBuffer.SetGlobalTexture("_SurfaceMaterialLUT", config.SurfaceMaterialLUT);

                foreach (GrassSurface surface in Surfaces)
                {
                    if (surface == null)
                        continue;

                    if (surface.surfaceRenderer == null || surface.encodeMaterial == null)
                        continue;

                    if (surface.materialIndex < 0)
                        continue;

                    int pass = 2;
                    gpuDataCmdBuffer.DrawRenderer(surface.surfaceRenderer, surface.encodeMaterial, 0, pass);
                }

                encodeCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, gpuDataCmdBuffer);
            }

            if (forceRender)
                encodeCamera.Render();
        }

        public void PlaceGrass(bool initialPlace, bool canRender = true)
        {
            if (pauseEncoder)
                return;

            Profiler.BeginSample("GPU Grass Placement Setup");

            TimeSample sample = null;
            if (measureExecutionTime)
                sample = new TimeSample("GPU Grass Placement Setup");

            // Time is seeded (to prevent randomness errors)
            // Prevent spamming the user's eyes if we keep placing initially
            float time = UniqueSeed;

            initialPlace = initialPlace || lastLUT != config.SurfaceMaterialLUT;

            if (!initialPlace)
                time += Time.time;

            // Ensure we have an image
            if (initialPlace && canRender)
                encodeCamera.Render();

            Vector4 cullRefPack = cullTransform.TransformPoint(cullEncoderOffset);
            cullRefPack.w = config.CullDistance;

            Vector4 grassEncodeInfo = Vector4.zero;
            if (encodeGO != null)
                grassEncodeInfo = new Vector4(cellEncoderNearPlane, cellEncoderFarPlane, encodeGO.transform.position.y, time);

            Vector4 grassState = Vector4.zero;
            grassState.x = initialPlace ? 1.0F : 0.0F;
            grassState.y = config.surfaceMaterials.Count - 1;
            grassState.z = config.lutGradientResolution - 1;
            grassState.w = config.GrassCount;

            Vector4 grassPerfInfo = Vector4.zero;
            grassPerfInfo.x = Time.deltaTime;
            grassPerfInfo.y = 1.0F / lagMinFPS;
            grassPerfInfo.z = lagCompMin;
            grassPerfInfo.w = lagCompMax;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
                placerCS.EnableKeyword("UNITY_REVERSED_Z");
            else
                placerCS.DisableKeyword("UNITY_REVERSED_Z");

            switch (colorMode)
            {
                case ColorMode.EncodedColor:
                    placerCS.EnableKeyword("PLACER_ENCODED_COLOR");
                    placerCS.DisableKeyword("PLACER_RANDOM_HSV");
                    break;

                case ColorMode.RandomHSV:
                    placerCS.DisableKeyword("PLACER_ENCODED_COLOR");
                    placerCS.EnableKeyword("PLACER_RANDOM_HSV");
                    break;

            }

            if (kernelIndex == -1)
            {
                kernelIndex = placerCS.FindKernel("CSMain");
                placerCS.GetKernelThreadGroupSizes(kernelIndex, out uint width, out uint _, out uint _);

                kernelWidth = (int)width;
            }

            if (kernelIndex == -1)
            {
                Debug.LogError("Kernel not found!");
                return;
            }

            placerCS.SetVector("_GrassPerfInfo", grassPerfInfo);
            placerCS.SetVector("_GrassEncodeInfo", grassEncodeInfo);
            placerCS.SetVector("_GrassInstanceCullRef", cullRefPack);
            placerCS.SetVector("_GrassState", grassState);

            placerCS.SetBuffer(kernelIndex, "_GrassInstanceInfos", grassBuffer);
            placerCS.SetTexture(kernelIndex, "_EncodedGrassMap", encodeGrassMap);
            placerCS.SetTexture(kernelIndex, "_GrassBiomeMap", encodeBiomeMap);
            placerCS.SetTexture(kernelIndex, "_SurfaceMaterialLUT", config.SurfaceMaterialLUT);

            lastLUT = config.SurfaceMaterialLUT;

            Profiler.EndSample();

            if (measureExecutionTime) 
            {
                sample.Stop();
                executionTimes.Add(sample);
                sample = new TimeSample("GPU Grass Placement Dispatch");
            }

            Profiler.BeginSample("GPU Grass Placement Dispatch");

            int numThreads = (config.GrassCount + kernelWidth - 1) / kernelWidth;
            placerCS.Dispatch(kernelIndex, numThreads, 1, 1);

            Profiler.EndSample();

            if (measureExecutionTime)
            {
                sample.Stop();
                executionTimes.Add(sample);
            }
        }

        public void OnEnable()
        {
            // Singleton
            if (Singleton == null)
                Singleton = this;
            else
            {
                Debug.LogError("Only one grass renderer can exist at once! Expect weird behavior until you reduce the count to 1");
            }

            VerifyData();

            firstFrame = true;
            UpdateIndirectArgs();

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += OnDisable;
#endif

            UpdateCamera(false, true);
        }

        public void LateUpdate()
        {
            if (measureExecutionTime)
                executionTimes.Clear();

            encodeGO.transform.position = Vector3.up * cellEncoderHeight;

            if (cullTransform != null)
            {
                Vector3 offset = cullTransform.TransformPoint(cullEncoderOffset);
                encodeGO.transform.position += new Vector3(offset.x, 0, offset.z);
            }

            if (config == null)
            {
#if GRASS_LOGGING
                Debug.LogError("Config was null!");
#endif
                return;
            }

            if (config.GrassCount <= 0 || indirectArgs[1] <= 0)
            {
#if GRASS_LOGGING
                Debug.LogError("GrassCount was 0!");
#endif
                return;
            }

            if (grassIndirectArgs == null)
            {
#if GRASS_LOGGING
                Debug.LogError("GrassIndirectArgs was null!");
#endif
                return;
            }

            if (grassBuffer == null)
            {
#if GRASS_LOGGING
                Debug.LogError("GrassBuffer was null!");
#endif
                return;
            }

            if (patchMesh == null)
            {
#if GRASS_LOGGING
                Debug.LogError("Mesh can't be null!");
#endif
                return;
            }

            if (patchMaterial == null)
            {
#if GRASS_LOGGING
                Debug.LogError("Material can't be null!");
#endif
                return;
            }

            if (cullTransform == null)
            {
#if GRASS_LOGGING
                Debug.LogError("Please assign a cull transform!");
#endif
                return;
            }

            UpdateCamera(false, true);

            if (indirectArgs[1] != config.GrassCount)
                UpdateIndirectArgs();

            VerifyData();
            PlaceGrass(firstFrame);

            Vector4 cullRefPack = cullTransform.TransformPoint(cullShaderOffset);
            cullRefPack.w = config.CullDistance;

            Bounds cullBounds = new Bounds(cullTransform.position, Vector3.one * config.CullDistance * 2F);

            Vector4 grassParams = Vector4.zero;
            grassParams.x = grassBuffer.count;

            Shader.SetGlobalVector("_GrassInstanceCullRef", cullRefPack);
            Shader.SetGlobalVector("_GrassInstanceEncodeRef", encodeGO.transform.position);
            Shader.SetGlobalVector("_GrassInstanceBounds", grassParams);
            Shader.SetGlobalBuffer("_GrassInstanceInfos", grassBuffer);
            Shader.SetGlobalTexture("_GrassObstacleMap", obstacleMap);

            Vector4 grassState = Vector4.zero;
            grassState.x = 0;
            grassState.y = config.surfaceMaterials.Count - 1;
            grassState.z = config.lutGradientResolution - 1;
            grassState.w = config.GrassCount;

            Shader.SetGlobalVector("_GrassState", grassState);
            Shader.SetGlobalTexture("_SurfaceMaterialLUT", config.SurfaceMaterialLUT);

            Graphics.DrawMeshInstancedIndirect(
                patchMesh, 
                0, 
                patchMaterial, 
                cullBounds, 
                grassIndirectArgs, 
                0, 
                null, 
                config.ShadowCastingMode, 
                config.RecieveShadows,
                0, 
                null,
                LightProbeUsage.Off
            );

            firstFrame = false;
        }

        public void OnDisable()
        {
            if (Singleton == this)
                Singleton = null;

            Cleanup();

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnDisable;
#endif
        }

        public void OnValidate()
        {
            UpdateIndirectArgs();
            PlaceGrass(true, false);
        }

        private void OnGUI()
        {
            if (showDebugWindow)
                windowRect = GUILayout.Window(UniqueID, windowRect, DoWindow, $"Grass: {gameObject.name}");
        }

        private void DoWindow(int id)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUIStyle customBox = GUI.skin.box;

                GUILayout.Box(new GUIContent(config.SurfaceMaterialLUT, "Surface Material LUT"), customBox);
                GUILayout.Box(new GUIContent(encodeGrassMap, "Encoded DepthNormals"), customBox);
                GUILayout.Box(new GUIContent(obstacleMap, "Obstacles"), customBox);
                GUILayout.Box(new GUIContent(encodeBiomeMap, "Biomes"), customBox);
            }

            if (GUILayout.Button("Reinitialize"))
            {
                windowRect = Rect.zero;
                OnValidate();
            }

            foreach (var execution in executionTimes)
                GUILayout.Label($"{execution.name} took {execution.elapsedMS}ms ({execution.elapsedNS}ns)");

            //GUILayout.Label($"RGBAHalf Supported? {SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf)}");
            //GUILayout.Label($"RGBAFloat Supported? {SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat)}");
            //GUILayout.Label($"RGBA32 Supported? {SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)}");

            GUI.DragWindow();
        }

#if UNITY_EDITOR
        private void DoGizmos()
        {
            Vector3 cullOrigin = Vector3.up * cellEncoderHeight;

            if (cullTransform != null)
            {
                Vector3 offset = cullTransform.TransformPoint(cullEncoderOffset);
                cullOrigin += new Vector3(offset.x, 0, offset.z);
            }

            Vector3 size = new Vector3(encodeCamera.orthographicSize, 0, encodeCamera.orthographicSize) * 2;
            size.y = cellEncoderFarPlane - cellEncoderNearPlane;

            Gizmos.color = Color.cyan;

            Gizmos.DrawWireCube(cullOrigin - new Vector3(0, cellEncoderNearPlane + (size.y / 2), 0), size);

            Gizmos.DrawWireSphere(cullOrigin, 0.1F);

            Handles.color = Color.green;

            Handles.DrawWireDisc(cullOrigin, Vector3.up, config.CullDistance);
            Handles.Label(cullOrigin, "Rendered Region");

            Handles.color = Color.white;
            Gizmos.color = Color.white;

            Vector3 cullDisc = cullTransform.TransformPoint(cullEncoderOffset);
            Vector3 shaderDisc = cullTransform.TransformPoint(cullShaderOffset);

            cullDisc.y = cullTransform.position.y;
            shaderDisc.y = cullTransform.position.y;

            Handles.color = Color.red;
            Handles.DrawWireDisc(cullDisc, Vector3.up, config.CullDistance);
            Handles.Label(cullDisc, "Encode Region");

            Handles.color = Color.yellow;
            Handles.DrawWireDisc(shaderDisc, Vector3.up, config.CullDistance);
            Handles.Label(shaderDisc, "Fade Region");
        }

        private void OnDrawGizmosSelected()
        {
            if (doGizmosOnSelected)
                DoGizmos();
        }

        private void OnDrawGizmos()
        {
            if (!doGizmosOnSelected)
                DoGizmos();
        }
#endif
    }
}