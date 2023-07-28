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

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace zCubed.Flora
{
    [ExecuteAlways]
    public class GrassObstacle : MonoBehaviour
    {
        public enum AlignMode
        {
            AlwaysPointUp,
            FollowRotation
        }

        public Texture2D displaceTexture;
        public Vector2 displaceArea = new Vector2(0.5F, 0.5F);

        public AlignMode alignMode = AlignMode.AlwaysPointUp;
        public Vector3 eulerOffset = new Vector3(0, 0, 0);

        [System.NonSerialized]
        public Material obstacleMaterial;

        public virtual void CreateMaterial()
        {
            obstacleMaterial = new Material(Shader.Find("zCubed/Grass/Obstacles/Default"));
        }

        public virtual void UpdateMaterial()
        {
            obstacleMaterial.SetTexture("_MainTex", displaceTexture);
        }

        public virtual Matrix4x4 GetTRS()
        {
            Quaternion quat = Quaternion.identity;
            switch (alignMode) {
                case AlignMode.AlwaysPointUp:
                    quat = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                    break;

                case AlignMode.FollowRotation:
                    quat = transform.rotation * Quaternion.Euler(eulerOffset);
                    break;
            }

            Vector3 scale = new Vector3(displaceArea.x, 1, displaceArea.y);
            
            return Matrix4x4.TRS(transform.position, quat, scale);
        }

        public void OnEnable()
        {
            CreateMaterial();
            UpdateMaterial();

            GrassRenderer.Obstacles.Add(this);
        }

        public void OnDisable()
        {
            GrassRenderer.Obstacles.Remove(this);
        }

        private void OnValidate()
        {
            CreateMaterial();
            UpdateMaterial();
        }

#if UNITY_EDITOR
        public void OnDrawGizmosSelected()
        {
            Matrix4x4 trs = GetTRS();

            Gizmos.matrix = trs;

            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 0, 1) * 2);

            obstacleMaterial.SetPass(1);
            Graphics.DrawMeshNow(GrassRenderer.QuadMesh, trs, 1);
        }
#endif
    }
}