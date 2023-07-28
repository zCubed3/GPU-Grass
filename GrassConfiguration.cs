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
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace zCubed.Flora
{
    [CreateAssetMenu(fileName = "New GrassConfiguration", menuName = "Grass/System Configuration", order = 1)]
    public class GrassConfiguration : ScriptableObject
    {
        [System.Serializable]
        public class PlatformConfiguration
        {
            public List<RuntimePlatform> targetPlatforms;
            public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
            public bool recieveShadows = false;

            public int grassCount = 1000;
            public float cullDistance = 5.0F;
        }

        [Header("Platform Configuration")]
        [Tooltip("The fallback config if there is no unique config for a given platform")]
        public PlatformConfiguration defaultConfig;

        [Tooltip("Unique configurations for specific platforms (eg. Android)")]
        public List<PlatformConfiguration> uniqueConfigs;

        [Header("Surface Materials")]
        public List<GrassSurfaceMaterial> surfaceMaterials;

        [Tooltip("How detailed is the LUT gradient?")]
        public int lutGradientResolution = 64;

        [Tooltip("Is the LUT in sRGB (Gamma) colorspace?")]
        public bool lutIsSRGB = false;

        //
        // Getters
        //
        [System.NonSerialized] // Required because Unity tries to default this!
        protected PlatformConfiguration _currentConfig = null;

        public PlatformConfiguration CurrentConfig
        {
            get
            {
                if (_currentConfig == null)
                {
                    try
                    {
#if UNITY_EDITOR
                        RuntimePlatform currentPlatform;

                        switch (EditorUserBuildSettings.activeBuildTarget)
                        {
                            default:
                                currentPlatform = Application.platform;
                                break;

                            case BuildTarget.Android:
                                currentPlatform = RuntimePlatform.Android;
                                break;

                        }
#else
                        RuntimePlatform currentPlatform = Application.platform;
#endif
                        _currentConfig = uniqueConfigs.First((cfg) => cfg.targetPlatforms.Contains(currentPlatform));
                    }
                    catch
                    {
                        _currentConfig = defaultConfig;
                    }
                }

                return _currentConfig;
            }
        }

        public float CullDistance => CurrentConfig.cullDistance;
        public int GrassCount => CurrentConfig.grassCount;
        public ShadowCastingMode ShadowCastingMode => CurrentConfig.shadowCastingMode;
        public bool RecieveShadows => CurrentConfig.recieveShadows;

        [System.NonSerialized]
        protected Texture2D _surfaceMaterialLUT = null;

        // TODO: Allow expanding the surface LUT at runtime?
        public Texture2D SurfaceMaterialLUT
        {
            get
            {
                if (_surfaceMaterialLUT == null)
                    CreateLUT();

                bool needRegen = false;
                foreach (GrassSurfaceMaterial mat in surfaceMaterials)
                {
                    if (mat == null)
                        continue;

                    if (mat.isDirty)
                    {
                        mat.isDirty = false;
                        needRegen = true;
                    }
                }

                if (needRegen)
                    CreateLUT();

                return _surfaceMaterialLUT;
            }
        }

        protected void CreateLUT()
        {
            _surfaceMaterialLUT = new Texture2D(surfaceMaterials.Count, 2 + lutGradientResolution, TextureFormat.RGBAHalf, false);

            for (int m = 0; surfaceMaterials.Count > m; m++)
            {
                GrassSurfaceMaterial material = surfaceMaterials[m];

                if (material == null)
                    material = new GrassSurfaceMaterial();

                _surfaceMaterialLUT.SetPixel(m, 0, (Vector4)material.minSize);
                _surfaceMaterialLUT.SetPixel(m, 1, (Vector4)material.maxSize);

                float halfTexel = 0.5F / lutGradientResolution;
                for (int t = 0; t < lutGradientResolution; t++) {
                    float v = ((float)t / lutGradientResolution) + halfTexel;

                    Color encoded = material.surfaceColorDensityGradient.Evaluate(v);

                    if (!lutIsSRGB)
                        encoded = encoded.linear;

                    _surfaceMaterialLUT.SetPixel(m, 2 + t, encoded);
                }
            }

            _surfaceMaterialLUT.Apply();

            // TODO: Proper singletons
            Shader.SetGlobalTexture("_GlobalSurfaceLUT", _surfaceMaterialLUT);
        }

        protected void OnValidate()
        {
            if (lutGradientResolution < 8)
            {
                Debug.Log("LUT Gradient resolution must be 8 pixels tall at minimum");
                lutGradientResolution = 8;
            }

            _currentConfig = null;
            _surfaceMaterialLUT = null;
        }
    }
}