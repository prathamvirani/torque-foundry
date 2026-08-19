using System.Collections.Generic;
using UnityEngine;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    internal readonly struct ProfileLoftRing
    {
        public ProfileLoftRing(float axisXM, IReadOnlyList<Vector2> yzProfile)
        {
            AxisXM = axisXM;
            YzProfile = yzProfile;
        }

        public float AxisXM { get; }
        public IReadOnlyList<Vector2> YzProfile { get; }
    }

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

        public static Vector2[] CreateRoundedRectangleProfile(
            float height,
            float depth,
            float cornerRadius,
            int cornerSegments = 5)
        {
            float halfHeight = Mathf.Max(0.0005f, height * 0.5f);
            float halfDepth = Mathf.Max(0.0005f, depth * 0.5f);
            float radius = Mathf.Clamp(cornerRadius, 0.0001f, Mathf.Min(halfHeight, halfDepth) * 0.98f);
            cornerSegments = Mathf.Max(2, cornerSegments);
            var profile = new List<Vector2>(cornerSegments * 4);

            AddProfileArc(profile, new Vector2(halfHeight - radius, halfDepth - radius), radius, 0f, 90f, cornerSegments);
            AddProfileArc(profile, new Vector2(-halfHeight + radius, halfDepth - radius), radius, 90f, 180f, cornerSegments);
            AddProfileArc(profile, new Vector2(-halfHeight + radius, -halfDepth + radius), radius, 180f, 270f, cornerSegments);
            AddProfileArc(profile, new Vector2(halfHeight - radius, -halfDepth + radius), radius, 270f, 360f, cornerSegments);
            EnsureCounterClockwise(profile);
            return profile.ToArray();
        }

        public static Vector2[] CreateEllipseProfile(float height, float depth, int segments = 32)
        {
            segments = Mathf.Max(8, segments);
            float halfHeight = Mathf.Max(0.0005f, height * 0.5f);
            float halfDepth = Mathf.Max(0.0005f, depth * 0.5f);
            var profile = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                profile[i] = new Vector2(Mathf.Cos(angle) * halfHeight, Mathf.Sin(angle) * halfDepth);
            }
            return profile;
        }

        public static Vector2[] CreateRoundedPolygonProfile(
            IReadOnlyList<Vector2> controlPoints,
            int smoothingIterations = 2)
        {
            if (controlPoints == null || controlPoints.Count < 3)
                return CreateRoundedRectangleProfile(0.001f, 0.001f, 0.0001f, 2);

            var current = new List<Vector2>(controlPoints);
            EnsureCounterClockwise(current);
            smoothingIterations = Mathf.Clamp(smoothingIterations, 0, 4);
            for (int iteration = 0; iteration < smoothingIterations; iteration++)
            {
                var next = new List<Vector2>(current.Count * 2);
                for (int i = 0; i < current.Count; i++)
                {
                    Vector2 a = current[i];
                    Vector2 b = current[(i + 1) % current.Count];
                    next.Add(Vector2.Lerp(a, b, 0.25f));
                    next.Add(Vector2.Lerp(a, b, 0.75f));
                }
                current = next;
            }
            return current.ToArray();
        }

        public static Vector2[] TransformProfile(
            IReadOnlyList<Vector2> profile,
            Vector2 scale,
            Vector2 offset)
        {
            var transformed = new Vector2[profile.Count];
            for (int i = 0; i < profile.Count; i++)
                transformed[i] = Vector2.Scale(profile[i], scale) + offset;
            return transformed;
        }

        public static Mesh CreateProfileLoftAlongX(
            string meshName,
            IReadOnlyList<ProfileLoftRing> rings,
            bool capEnds = true)
        {
            if (rings == null || rings.Count < 2 || rings[0].YzProfile == null || rings[0].YzProfile.Count < 3)
                return CreateBeveledBox(meshName, Vector3.one * 0.001f, 0.0001f);

            int profileCount = rings[0].YzProfile.Count;
            for (int ring = 1; ring < rings.Count; ring++)
                if (rings[ring].YzProfile == null || rings[ring].YzProfile.Count != profileCount)
                    return CreateBeveledBox(meshName, Vector3.one * 0.001f, 0.0001f);

            var vertices = new List<Vector3>(rings.Count * profileCount + (capEnds ? profileCount * 2 + 2 : 0));
            var triangles = new List<int>((rings.Count - 1) * profileCount * 6 + (capEnds ? profileCount * 6 : 0));

            for (int ring = 0; ring < rings.Count; ring++)
            for (int point = 0; point < profileCount; point++)
            {
                Vector2 yz = rings[ring].YzProfile[point];
                vertices.Add(new Vector3(rings[ring].AxisXM, yz.x, yz.y));
            }

            for (int ring = 0; ring < rings.Count - 1; ring++)
            for (int point = 0; point < profileCount; point++)
            {
                int nextPoint = (point + 1) % profileCount;
                int a = ring * profileCount + point;
                int b = (ring + 1) * profileCount + point;
                int c = (ring + 1) * profileCount + nextPoint;
                int d = ring * profileCount + nextPoint;
                triangles.Add(a);
                triangles.Add(d);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }

            if (capEnds)
            {
                AddLoftCap(vertices, triangles, rings[0], false);
                AddLoftCap(vertices, triangles, rings[rings.Count - 1], true);
            }

            return BuildSmoothMesh(meshName, vertices, triangles);
        }

        public static Mesh CreateTubeAlongPath(
            string meshName,
            IReadOnlyList<Vector3> path,
            float radius,
            int radialSegments = 12,
            bool capEnds = true,
            bool closed = false,
            float secondaryRadius = -1f)
        {
            if (path == null || path.Count < 2)
                return CreateBeveledBox(meshName, Vector3.one * 0.001f, 0.0001f);
            radius = Mathf.Max(0.0001f, radius);
            secondaryRadius = secondaryRadius <= 0f ? radius : Mathf.Max(0.0001f, secondaryRadius);
            radialSegments = Mathf.Max(6, radialSegments);
            int pathCount = path.Count;
            var vertices = new List<Vector3>(pathCount * radialSegments + 2);
            var triangles = new List<int>((pathCount - 1 + (closed ? 1 : 0)) * radialSegments * 6);

            Vector3 previousNormal = Vector3.zero;
            for (int point = 0; point < pathCount; point++)
            {
                Vector3 previous = path[point == 0 ? (closed ? pathCount - 1 : 0) : point - 1];
                Vector3 next = path[point == pathCount - 1 ? (closed ? 0 : pathCount - 1) : point + 1];
                Vector3 tangent = (next - previous).normalized;
                Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.right)) < 0.9f
                    ? Vector3.right
                    : Vector3.up;
                Vector3 normal = Vector3.Cross(tangent, reference).normalized;
                if (point > 0 && Vector3.Dot(normal, previousNormal) < 0f) normal = -normal;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                previousNormal = normal;

                for (int segment = 0; segment < radialSegments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / radialSegments;
                    vertices.Add(path[point]
                                 + normal * (Mathf.Cos(angle) * radius)
                                 + binormal * (Mathf.Sin(angle) * secondaryRadius));
                }
            }

            int pathSegments = closed ? pathCount : pathCount - 1;
            for (int point = 0; point < pathSegments; point++)
            {
                int nextPoint = (point + 1) % pathCount;
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    int nextSegment = (segment + 1) % radialSegments;
                    int a = point * radialSegments + segment;
                    int b = nextPoint * radialSegments + segment;
                    int c = nextPoint * radialSegments + nextSegment;
                    int d = point * radialSegments + nextSegment;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            if (capEnds && !closed)
            {
                AddPathCap(vertices, triangles, 0, radialSegments, false);
                AddPathCap(vertices, triangles, (pathCount - 1) * radialSegments, radialSegments, true);
            }

            return BuildSmoothMesh(meshName, vertices, triangles);
        }

        private static void AddProfileArc(
            List<Vector2> profile,
            Vector2 centre,
            float radius,
            float startAngleDeg,
            float endAngleDeg,
            int segments)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                float t = segment / (float)segments;
                float angle = Mathf.Lerp(startAngleDeg, endAngleDeg, t) * Mathf.Deg2Rad;
                profile.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private static void EnsureCounterClockwise(List<Vector2> profile)
        {
            float twiceArea = 0f;
            for (int i = 0; i < profile.Count; i++)
            {
                Vector2 a = profile[i];
                Vector2 b = profile[(i + 1) % profile.Count];
                twiceArea += a.x * b.y - b.x * a.y;
            }
            if (twiceArea < 0f) profile.Reverse();
        }

        private static void AddLoftCap(
            List<Vector3> vertices,
            List<int> triangles,
            ProfileLoftRing ring,
            bool positiveX)
        {
            int centreIndex = vertices.Count;
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < ring.YzProfile.Count; i++) centre += ring.YzProfile[i];
            centre /= ring.YzProfile.Count;
            vertices.Add(new Vector3(ring.AxisXM, centre.x, centre.y));
            int profileStart = vertices.Count;
            for (int i = 0; i < ring.YzProfile.Count; i++)
            {
                Vector2 yz = ring.YzProfile[i];
                vertices.Add(new Vector3(ring.AxisXM, yz.x, yz.y));
            }
            for (int i = 0; i < ring.YzProfile.Count; i++)
            {
                int next = (i + 1) % ring.YzProfile.Count;
                triangles.Add(centreIndex);
                triangles.Add(profileStart + (positiveX ? i : next));
                triangles.Add(profileStart + (positiveX ? next : i));
            }
        }

        private static void AddPathCap(
            List<Vector3> vertices,
            List<int> triangles,
            int ringStart,
            int radialSegments,
            bool forward)
        {
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < radialSegments; i++) centre += vertices[ringStart + i];
            centre /= radialSegments;
            int centreIndex = vertices.Count;
            vertices.Add(centre);
            for (int i = 0; i < radialSegments; i++)
            {
                int next = (i + 1) % radialSegments;
                triangles.Add(centreIndex);
                triangles.Add(ringStart + (forward ? i : next));
                triangles.Add(ringStart + (forward ? next : i));
            }
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

        private static Mesh BuildSmoothMesh(string meshName, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            // Shared side vertices produce smooth perimeter normals; cap vertices are
            // intentionally duplicated so machined end faces remain visually distinct.
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
