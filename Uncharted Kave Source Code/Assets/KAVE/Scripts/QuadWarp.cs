﻿using System.Collections.Generic;
using UnityEngine;

namespace VRKave
{
    [ExecuteInEditMode]
    public class QuadWarp : MonoBehaviour
    {

        public Material _mat;
        public int DisplayIndex;

        public List<Texture> _tex = new List<Texture>();
        public Vector2[] _uvs = new[] {new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)};
        public List<Vector2[]> _vertices = new List<Vector2[]>();

        Matrix4x4 CalcHomography(Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Vector2 bottomLeft)
        {
            var sx = (topLeft.x - topRight.x) + (bottomRight.x - bottomLeft.x);
            var sy = (topLeft.y - topRight.y) + (bottomRight.y - bottomLeft.y);

            var dx1 = topRight.x - bottomRight.x;
            var dx2 = bottomLeft.x - bottomRight.x;
            var dy1 = topRight.y - bottomRight.y;
            var dy2 = bottomLeft.y - bottomRight.y;

            var z = (dx1 * dy2) - (dy1 * dx2);
            var g = ((sx * dy2) - (sy * dx2)) / z;
            var h = ((sy * dx1) - (sx * dy1)) / z;

            var system = new[]
            {
                topRight.x - topLeft.x + g * topRight.x,
                bottomLeft.x - topLeft.x + h * bottomLeft.x,
                topLeft.x,
                topRight.y - topLeft.y + g * topRight.y,
                bottomLeft.y - topLeft.y + h * bottomLeft.y,
                topLeft.y,
                g,
                h,
            };

            var mtx = Matrix4x4.identity;
            mtx.m00 = system[0];
            mtx.m01 = system[1];
            mtx.m02 = system[2];
            mtx.m10 = system[3];
            mtx.m11 = system[4];
            mtx.m12 = system[5];
            mtx.m20 = system[6];
            mtx.m21 = system[7];
            mtx.m22 = 1f;

            return mtx;
        }

        public void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var homographyUV = CalcHomography(_uvs[0], _uvs[3], _uvs[2], _uvs[1]);
            Graphics.SetRenderTarget(destination);
            GL.Clear(true, true, Color.clear);

            for (int surfaceIndex = 0; surfaceIndex < _tex.Count; surfaceIndex++)
            {
                GL.PushMatrix();
                GL.LoadOrtho();
                var homographyVtx = CalcHomography(_vertices[surfaceIndex][0], _vertices[surfaceIndex][3],
                    _vertices[surfaceIndex][2], _vertices[surfaceIndex][1]);
                var homography = homographyUV * homographyVtx.inverse;
                _mat.mainTexture = _tex[surfaceIndex];
                _mat.SetMatrix("_Homography", homography);
                _mat.SetPass(0);

#if !UNITY_EDITOR
            var rectPixel =
new Rect(0f, 0f, Display.displays[DisplayIndex].renderingWidth, Display.displays[DisplayIndex].renderingHeight);
#else
                var rectPixel = new Rect(0f, 0f, Screen.width, Screen.height);
#endif

                GL.Viewport(rectPixel);

                GL.Begin(GL.QUADS);

                for (var i = 0; i < 4; ++i)
                {
                    GL.Vertex(_vertices[surfaceIndex][i]);
                }

                GL.End();
                GL.PopMatrix();
            }
        }
    }
}
