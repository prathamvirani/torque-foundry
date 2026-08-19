using System.Collections.Generic;
using UnityEngine;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    /// <summary>
    /// Disposable presentation-mesh helpers used by the Engine Lab visual-fidelity pass.
    /// Dimensions are Unity metres. These meshes never contribute authoritative state.
    /// </summary>
    internal static class ProceduralEngineMeshFactory
    {
        public static Mesh CreateBeveledBox(string meshName, Vector3 size, float bevel)
        {
            float hx = Mathf.Max(0.0005f, size.x * 0.5f);
            float hy = Mathf.Max(0.0005f, size.y * 0.5f);
            float hz = Mathf.Max(0.0005f, size.z * 0.5f);
            float b = Mathf.Clamp(bevel, 0.0001f, Mathf.Min(hx, Mathf.Min(hy, hz)) * 0.48f);

            var vertices = new List<Vector3>(96);
            var triangles = new List<int>(180);

            AddQuad(vertices, triangles,
                new Vector3(hx, -hy + b, -hz + b), new Vector3(hx, hy - b, -hz + b),
                new Vector3(hx, hy - b, hz - b), new Vector3(hx, -hy + b, hz - b), Vector3.right);
            AddQuad(vertices, triangles,
                new Vector3(-hx, -hy + b, hz - b), new Vector3(-hx, hy - b, hz - b),
                new Vector3(-hx, hy - b, -hz + b), new Vector3(-hx, -hy + b, -hz + b), Vector3.left);
            AddQuad(vertices, triangles,
                new Vector3(-hx + b, hy, -hz + b), new Vector3(-hx + b, hy, hz - b),
                new Vector3(hx - b, hy, hz - b), new Vector3(hx - b, hy, -hz + b), Vector3.up);
            AddQuad(vertices, triangles,
                new Vector3(-hx + b, -hy, hz - b), new Vector3(-hx + b, -hy, -hz + b),
                new Vector3(hx - b, -hy, -hz + b), new Vector3(hx - b, -hy, hz - b), Vector3.down);
            AddQuad(vertices, triangles,
                new Vector3(-hx + b, -hy + b, hz), new Vector3(hx - b, -hy + b, hz),
                new Vector3(hx - b, hy - b, hz), new Vector3(-hx + b, hy - b, hz), Vector3.forward);
            AddQuad(vertices, triangles,
                new Vector3(hx - b, -hy + b, -hz), new Vector3(-hx + b, -hy + b, -hz),
                new Vector3(-hx + b, hy - b, -hz), new Vector3(hx - b, hy - b, -hz), Vector3.back);

            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddQuad(vertices, triangles,
                    new Vector3(-hx + b, sy * hy, sz * (hz - b)),
                    new Vector3(hx - b, sy * hy, sz * (hz - b)),
                    new Vector3(hx - b, sy * (hy - b), sz * hz),
                    new Vector3(-hx + b, sy * (hy - b), sz * hz),
                    new Vector3(0f, sy, sz).normalized);
            }

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddQuad(vertices, triangles,
                    new Vector3(sx * hx, -hy + b, sz * (hz - b)),
                    new Vector3(sx * hx, hy - b, sz * (hz - b)),
                    new Vector3(sx * (hx - b), hy - b, sz * hz),
                    new Vector3(sx * (hx - b), -hy + b, sz * hz),
                    new Vector3(sx, 0f, sz).normalized);
            }

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                AddQuad(vertices, triangles,
                    new Vector3(sx * hx, sy * (hy - b), -hz + b),
                    new Vector3(sx * hx, sy * (hy - b), hz - b),
                    new Vector3(sx * (hx - b), sy * hy, hz - b),
                    new Vector3(sx * (hx - b), sy * hy, -hz + b),
                    new Vector3(sx, sy, 0f).normalized);
            }

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddTriangle(vertices, triangles,
                    new Vector3(sx * hx, sy * (hy - b), sz * (hz - b)),
                    new Vector3(sx * (hx - b), sy * hy, sz * (hz - b)),
                    new Vector3(sx * (hx - b), sy * (hy - b), sz * hz),
                    new Vector3(sx, sy, sz).normalized);
            }

            return BuildMesh(meshName, vertices, triangles);
        }

        public static Mesh CreateRingAlongX(
            string meshName,
            float innerRadius,
            float outerRadius,
            float thickness,
            int segments = 32)
        {
            innerRadius = Mathf.Max(0.0001f, innerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.0001f, outerRadius);
            thickness = Mathf.Max(0.0001f, thickness);
            segments = Mathf.Max(8, segments);

            var vertices = new List<Vector3>(segments * 16);
            var triangles = new List<int>(segments * 24);
            float half = thickness * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 o0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerRadius;
                Vector2 o1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerRadius;
                Vector2 n0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
                Vector2 n1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;

                AddQuad(vertices, triangles,
                    new Vector3(-half, o0.x, o0.y), new Vector3(half, o0.x, o0.y),
                    new Vector3(half, o1.x, o1.y), new Vector3(-half, o1.x, o1.y),
                    new Vector3(0f, Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f)));
                AddQuad(vertices, triangles,
                    new Vector3(-half, n1.x, n1.y), new Vector3(half, n1.x, n1.y),
                    new Vector3(half, n0.x, n0.y), new Vector3(-half, n0.x, n0.y),
                    new Vector3(0f, -Mathf.Cos((a0 + a1) * 0.5f), -Mathf.Sin((a0 + a1) * 0.5f)));
                AddQuad(vertices, triangles,
                    new Vector3(half, n0.x, n0.y), new Vector3(half, n1.x, n1.y),
                    new Vector3(half, o1.x, o1.y), new Vector3(half, o0.x, o0.y), Vector3.right);
                AddQuad(vertices, triangles,
                    new Vector3(-half, o0.x, o0.y), new Vector3(-half, o1.x, o1.y),
                    new Vector3(-half, n1.x, n1.y), new Vector3(-half, n0.x, n0.y), Vector3.left);
            }

            return BuildMesh(meshName, vertices, triangles);
        }

        public static Mesh CreateTubeSectorAlongY(
            string meshName,
            float innerRadius,
            float outerRadius,
            float height,
            float startAngleDeg,
            float arcAngleDeg,
            int segments = 36)
        {
            innerRadius = Mathf.Max(0.0001f, innerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.0001f, outerRadius);
            height = Mathf.Max(0.0001f, height);
            arcAngleDeg = Mathf.Clamp(arcAngleDeg, 10f, 360f);
            segments = Mathf.Max(4, Mathf.CeilToInt(segments * arcAngleDeg / 360f));

            var vertices = new List<Vector3>(segments * 16 + 16);
            var triangles = new List<int>(segments * 24 + 24);
            float half = height * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float a0 = (startAngleDeg + arcAngleDeg * i / segments) * Mathf.Deg2Rad;
                float a1 = (startAngleDeg + arcAngleDeg * (i + 1) / segments) * Mathf.Deg2Rad;
                Vector2 o0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerRadius;
                Vector2 o1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerRadius;
                Vector2 n0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
                Vector2 n1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;

                AddQuad(vertices, triangles,
                    new Vector3(o0.x, -half, o0.y), new Vector3(o1.x, -half, o1.y),
                    new Vector3(o1.x, half, o1.y), new Vector3(o0.x, half, o0.y),
                    new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f)));
                AddQuad(vertices, triangles,
                    new Vector3(n1.x, -half, n1.y), new Vector3(n0.x, -half, n0.y),
                    new Vector3(n0.x, half, n0.y), new Vector3(n1.x, half, n1.y),
                    new Vector3(-Mathf.Cos((a0 + a1) * 0.5f), 0f, -Mathf.Sin((a0 + a1) * 0.5f)));
                AddQuad(vertices, triangles,
                    new Vector3(n0.x, half, n0.y), new Vector3(o0.x, half, o0.y),
                    new Vector3(o1.x, half, o1.y), new Vector3(n1.x, half, n1.y), Vector3.up);
                AddQuad(vertices, triangles,
                    new Vector3(n1.x, -half, n1.y), new Vector3(o1.x, -half, o1.y),
                    new Vector3(o0.x, -half, o0.y), new Vector3(n0.x, -half, n0.y), Vector3.down);
            }

            if (arcAngleDeg < 359.9f)
            {
                AddTubeSectorEnd(vertices, triangles, innerRadius, outerRadius, half, startAngleDeg * Mathf.Deg2Rad, -1f);
                AddTubeSectorEnd(vertices, triangles, innerRadius, outerRadius, half,
                    (startAngleDeg + arcAngleDeg) * Mathf.Deg2Rad, 1f);
            }

            return BuildMesh(meshName, vertices, triangles);
        }

        public static Mesh CreateExtrudedProfileAlongX(
            string meshName,
            IReadOnlyList<Vector2> yzProfile,
            float thickness)
        {
            int count = yzProfile.Count;
            if (count < 3) return CreateBeveledBox(meshName, Vector3.one * 0.001f, 0.0001f);

            float half = Mathf.Max(0.0001f, thickness * 0.5f);
            var vertices = new List<Vector3>(count * 6);
            var triangles = new List<int>(count * 12);

            for (int i = 1; i < count - 1; i++)
            {
                AddTriangle(vertices, triangles,
                    new Vector3(half, yzProfile[0].x, yzProfile[0].y),
                    new Vector3(half, yzProfile[i].x, yzProfile[i].y),
                    new Vector3(half, yzProfile[i + 1].x, yzProfile[i + 1].y), Vector3.right);
                AddTriangle(vertices, triangles,
                    new Vector3(-half, yzProfile[0].x, yzProfile[0].y),
                    new Vector3(-half, yzProfile[i + 1].x, yzProfile[i + 1].y),
                    new Vector3(-half, yzProfile[i].x, yzProfile[i].y), Vector3.left);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector2 a = yzProfile[i];
                Vector2 b = yzProfile[next];
                Vector2 edge = b - a;
                Vector3 expected = new Vector3(0f, edge.y, -edge.x).normalized;
                AddQuad(vertices, triangles,
                    new Vector3(-half, a.x, a.y), new Vector3(-half, b.x, b.y),
                    new Vector3(half, b.x, b.y), new Vector3(half, a.x, a.y), expected);
            }

            return BuildMesh(meshName, vertices, triangles);
        }

        private static void AddTubeSectorEnd(
            List<Vector3> vertices,
            List<int> triangles,
            float innerRadius,
            float outerRadius,
            float halfHeight,
            float angle,
            float normalSign)
        {
            Vector2 inner = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * innerRadius;
            Vector2 outer = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * outerRadius;
            Vector3 tangentNormal = new Vector3(-Mathf.Sin(angle) * normalSign, 0f, Mathf.Cos(angle) * normalSign);
            AddQuad(vertices, triangles,
                new Vector3(inner.x, -halfHeight, inner.y), new Vector3(outer.x, -halfHeight, outer.y),
                new Vector3(outer.x, halfHeight, outer.y), new Vector3(inner.x, halfHeight, inner.y), tangentNormal);
        }

        private static Mesh BuildMesh(string meshName, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 expectedNormal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedNormal) < 0f)
            {
                Vector3 swap = b;
                b = d;
                d = swap;
            }

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AddTriangle(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 expectedNormal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), expectedNormal) < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
            }

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
