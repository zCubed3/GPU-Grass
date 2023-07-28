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
using UnityEditor;

namespace zCubed.Flora
{
    [CustomEditor(typeof(GrassSurface))]
    public class GrassSurfaceEditor : Editor
    {
        private GrassRenderer[] renderers = null;
        private bool advanced = false;

        protected GrassConfiguration targetConfig = null;

        SerializedProperty materialIndex;
        SerializedProperty surfaceRenderer;
        SerializedProperty alternativeShader;

        private void OnEnable()
        {
            materialIndex = serializedObject.FindProperty("materialIndex");
            surfaceRenderer = serializedObject.FindProperty("surfaceRenderer");
            alternativeShader = serializedObject.FindProperty("alternativeShader");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (target is GrassSurface surface)
            {
                if (renderers == null)
                    renderers = FindObjectsOfType<GrassRenderer>();

                if (renderers.Length == 0 && targetConfig == null)
                {
                    EditorGUILayout.HelpBox("No Grass Renderers were found in the active scene! Please manually assign a grass configuration!", MessageType.Error);
                    targetConfig = EditorGUILayout.ObjectField("Grass Config", targetConfig, typeof(GrassConfiguration), true) as GrassConfiguration;
                    return;
                }

                GrassRenderer renderer = null;
                if (renderers.Length >= 1)
                {
                    if (renderers.Length > 1)
                    {
                        if (GUILayout.Button("Refresh Renderer List"))
                        {
                            renderers = null;
                            return;
                        }

                        GUILayout.Label("Choose a reference renderer:");
                        int i = 0;
                        foreach (GrassRenderer pick in renderers)
                            if (GUILayout.Button($"{i++}: {pick.gameObject.name}"))
                                renderer = pick;

                        GUILayout.Space(10);
                    }
                    else
                        renderer = renderers[0];
                }

                if (renderer == null && targetConfig == null)
                {
                    EditorGUILayout.HelpBox("Please select a renderer!", MessageType.Error);
                    return;
                }

                if (renderer != null)
                {
                    if (renderer.config == null)
                    {
                        EditorGUILayout.HelpBox("Selected renderer does not have a config assigned to it!", MessageType.Error);
                        return;
                    }
                    else
                        targetConfig = renderer.config;
                }

                if (surface.surfaceRenderer == null)
                {
                    EditorGUILayout.HelpBox("Please assign a renderer to this surface!", MessageType.Error);
                    EditorGUILayout.PropertyField(surfaceRenderer);
                    return;
                }

                EditorGUILayout.LabelField("Materials:", EditorStyles.boldLabel);

                string selectedMaterial = "";

                bool wasBlocker = materialIndex.intValue < 0;
                bool isBlocker = GUILayout.Toggle(wasBlocker, "Blocker?");

                if (wasBlocker != isBlocker)
                {
                    int m = (isBlocker ? -1 : 0);

                    materialIndex.intValue = m;
                    surface.materialIndex = m;

                    surface.OnDisable();
                    surface.OnEnable();

                    if (renderer != null)
                    {
                        renderer.UpdateCamera(true);
                        renderer.PlaceGrass(true, true);
                    }
                }

                using (new EditorGUI.DisabledGroupScope(isBlocker))
                {
                    int m = 0;
                    foreach (GrassSurfaceMaterial material in targetConfig.surfaceMaterials)
                    {
                        if (GUILayout.Button(material.name))
                        {
                            materialIndex.intValue = m;
                            surface.materialIndex = m;

                            surface.OnDisable();
                            surface.OnEnable();

                            if (renderer != null)
                            {
                                renderer.UpdateCamera(true);
                                renderer.PlaceGrass(true, true);
                            }
                        }

                        if (m == materialIndex.intValue)
                            selectedMaterial = material.name;

                        m++;
                    }
                }

                if (string.IsNullOrEmpty(selectedMaterial))
                    EditorGUILayout.LabelField("Is Blocker!");
                else
                    EditorGUILayout.LabelField($"Current Material = '{selectedMaterial}'");

                if ((advanced = EditorGUILayout.Foldout(advanced, "Advanced Settings")))
                {
                    EditorGUILayout.PropertyField(alternativeShader);
                    EditorGUILayout.PropertyField(surfaceRenderer);

                    base.OnInspectorGUI();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}