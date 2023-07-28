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
    [CanEditMultipleObjects]
    [CustomEditor(typeof(GrassObstacle))]
    public class GrassObstacleEditor : Editor
    {
        SerializedProperty displaceTexture;
        SerializedProperty displaceArea;
        SerializedProperty alignMode;
        SerializedProperty eulerOffset;

        private void OnEnable()
        {
            displaceTexture = serializedObject.FindProperty("displaceTexture");
            displaceArea = serializedObject.FindProperty("displaceArea");
            alignMode = serializedObject.FindProperty("alignMode");
            eulerOffset = serializedObject.FindProperty("eulerOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            foreach (Object target in targets) {
                if (target is GrassObstacle obstacle)
                {
                    if (obstacle.displaceTexture == null)
                    {
                        EditorGUILayout.HelpBox("Please assign a XYMS texture!", MessageType.Error);
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(obstacle.displaceTexture);

                        TextureImporter importer = TextureImporter.GetAtPath(path) as TextureImporter;

                        if (importer.sRGBTexture)
                        {
                            EditorGUILayout.HelpBox("XYMS textures should not be sRGB!", MessageType.Error);
                            if (GUILayout.Button("Fix XYMS texture"))
                            {
                                importer.sRGBTexture = false;

                                EditorUtility.SetDirty(importer);
                                importer.SaveAndReimport();
                            }
                        }
                    }
                }
            }

            EditorGUILayout.PropertyField(displaceTexture);
            EditorGUILayout.PropertyField(displaceArea);

            EditorGUILayout.PropertyField(alignMode);
            
            switch ((GrassObstacle.AlignMode)alignMode.enumValueIndex)
            {
                case GrassObstacle.AlignMode.FollowRotation:
                    EditorGUILayout.PropertyField(eulerOffset);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}