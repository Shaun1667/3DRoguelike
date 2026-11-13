using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 월드의 진행 길. 위에서 본 꺾은선(x, z)이고, 길 시작에서 잰 거리로 자리를 말한다.
    /// 지형(골짜기 폭) · 던전 입구 · 관문 · 적 무리 자리가 모두 이 거리로 정해진다.
    /// </summary>
    public sealed class WorldRoute
    {
        private readonly float[] xs;
        private readonly float[] zs;
        private readonly float[] starts; // 각 구간이 시작하는 길 거리

        public WorldRoute(IReadOnlyList<(float x, float z)> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 2) throw new ArgumentException("A route needs at least two points.", nameof(points));
            xs = new float[points.Count];
            zs = new float[points.Count];
            starts = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                xs[i] = points[i].x;
                zs[i] = points[i].z;
                if (i > 0) starts[i] = starts[i - 1] + Distance(xs[i - 1], zs[i - 1], xs[i], zs[i]);
            }
            Length = starts[points.Count - 1];
            if (Length <= 0f) throw new ArgumentException("A route needs a length.", nameof(points));
        }

        /// <summary>길 전체 길이(m).</summary>
        public float Length { get; }

        public int PointCount => xs.Length;

        public (float x, float z) Point(int index) => (xs[index], zs[index]);

        /// <summary>길 거리의 자리. 0보다 작거나 길이를 넘으면 끝에 붙인다.</summary>
        public (float x, float z) PointAt(float distance)
        {
            int i = SegmentAt(distance, out float t);
            return (xs[i] + (xs[i + 1] - xs[i]) * t, zs[i] + (zs[i + 1] - zs[i]) * t);
        }

        /// <summary>길 거리에서 나아가는 방향(길이 1).</summary>
        public (float x, float z) DirectionAt(float distance)
        {
            int i = SegmentAt(distance, out _);
            float dx = xs[i + 1] - xs[i];
            float dz = zs[i + 1] - zs[i];
            float length = (float)Math.Sqrt(dx * dx + dz * dz);
            return (dx / length, dz / length);
        }

        /// <summary>길 거리에서 옆으로 비킨 자리. side가 +면 나아가는 방향의 오른쪽.</summary>
        public (float x, float z) BesideAt(float distance, float side)
        {
            (float x, float z) = PointAt(distance);
            (float dx, float dz) = DirectionAt(distance);
            // 위에서 볼 때(+z가 앞) 오른쪽 = (dz, -dx)
            return (x + dz * side, z - dx * side);
        }

        /// <summary>(x, z)에서 길까지 가장 가까운 거리. along에 그 가장 가까운 점의 길 거리.</summary>
        public float DistanceFrom(float x, float z, out float along)
        {
            float best = float.MaxValue;
            along = 0f;
            for (int i = 0; i < xs.Length - 1; i++)
            {
                float ax = xs[i], az = zs[i];
                float dx = xs[i + 1] - ax, dz = zs[i + 1] - az;
                float lengthSq = dx * dx + dz * dz;
                float t = lengthSq > 0f ? ((x - ax) * dx + (z - az) * dz) / lengthSq : 0f;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                float distance = Distance(x, z, ax + dx * t, az + dz * t);
                if (distance >= best) continue;
                best = distance;
                along = starts[i] + (starts[i + 1] - starts[i]) * t;
            }
            return best;
        }

        private int SegmentAt(float distance, out float t)
        {
            if (distance <= 0f)
            {
                t = 0f;
                return 0;
            }
            for (int i = 0; i < xs.Length - 1; i++)
            {
                if (distance > starts[i + 1]) continue;
                float span = starts[i + 1] - starts[i];
                t = span > 0f ? (distance - starts[i]) / span : 0f;
                return i;
            }
            t = 1f;
            return xs.Length - 2;
        }

        private static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
