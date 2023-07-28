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
using UnityEngine;

namespace zCubed.Flora 
{
    [CreateAssetMenu(fileName = "New GrassConfiguration", menuName = "Grass/Surface Material", order = 1)]
    public class GrassSurfaceMaterial : ScriptableObject
    {
        //
        // Fallback data
        //
        [HideInInspector]
        public Color surfaceColor = Color.magenta;

        [HideInInspector]
        public float surfaceDensity = 1.0F;

        [Header("Surface Data")]
        [Tooltip("The possible range of colors for grass on this surface (note: alpha = density)")]
        public Gradient surfaceColorDensityGradient;

        [Tooltip("How large can the grass be on this surface?")]
        public Vector3 minSize = new Vector3(0.01F, 0.01F, 0.01F);

        [Tooltip("How large can the grass be on this surface?")]
        public Vector3 maxSize = new Vector3(0.1F, 0.1F, 0.1F);

        [System.NonSerialized]
        public bool isDirty = false;

        private void OnValidate()
        {
            if (surfaceColorDensityGradient == null)
            {
                surfaceColorDensityGradient = new Gradient();
                surfaceColorDensityGradient.SetKeys(
                    new GradientColorKey[] 
                    {
                        new GradientColorKey(surfaceColor, 0),
                        new GradientColorKey(surfaceColor, 1)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(surfaceDensity, 0),
                        new GradientAlphaKey(surfaceDensity, 1)
                    }
                    );
            }

            isDirty = true;
        }
    }
}