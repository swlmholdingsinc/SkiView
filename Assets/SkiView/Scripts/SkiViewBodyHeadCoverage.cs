using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkiView
{
    /// <summary>
    /// Removes the body head/neck UV island that Face125 replaces.
    /// The window is the one EnsureFace125 already names: u in [0.505, 0.892] and v above 0.505.
    /// A runtime mesh copy is assigned. The imported asset, the face, and the vest are not written.
    /// </summary>
    public static class SkiViewBodyHeadCoverage
    {
        public const float UMin = 0.505f;
        public const float UMax = 0.892f;
        public const float VMin = 0.505f;
        public const string ClippedSuffix = "_HeadClipped";

        public static bool InHeadIsland(float u, float v)
        {
            return u >= UMin && u <= UMax && v > VMin;
        }

        /// <summary>A triangle draws an island vertex when any corner is inside the window.</summary>
        public static bool DropTriangle(bool a, bool b, bool c)
        {
            return a || b || c;
        }

        public static int Apply(Transform root)
        {
            if (root == null) return 0;
            int removed = 0;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.name.IndexOf("Body_LOD", StringComparison.Ordinal) < 0) continue;
                if (smr.sharedMesh.name.EndsWith(ClippedSuffix, StringComparison.Ordinal)) continue;
                Mesh clipped;
                int dropped;
                if (!TryClip(smr.sharedMesh, out clipped, out dropped)) continue;
                smr.sharedMesh = clipped;
                removed += dropped;
            }
            return removed;
        }

        static void Gather<T>(NativeArray<T> raw, int submesh, Mesh.MeshData srcData, int vc, bool[] inside, System.Collections.Generic.List<int> kept, int[] subStart, int[] subCountOut, MeshTopology[] topo, ref int droppedTriangles) where T : struct
        {
            var desc = srcData.GetSubMesh(submesh);
            topo[submesh] = desc.topology;
            subStart[submesh] = kept.Count;
            int begin = desc.indexStart;
            int end = begin + desc.indexCount;
            int step = desc.topology == MeshTopology.Triangles ? 3 : 1;
            for (int t = begin; t + step <= end; t += step)
            {
                if (step == 3)
                {
                    int a = Index(raw, t) + desc.baseVertex;
                    int b = Index(raw, t + 1) + desc.baseVertex;
                    int c = Index(raw, t + 2) + desc.baseVertex;
                    bool da = a >= 0 && a < vc && inside[a];
                    bool db = b >= 0 && b < vc && inside[b];
                    bool dc = c >= 0 && c < vc && inside[c];
                    if (DropTriangle(da, db, dc)) { droppedTriangles++; continue; }
                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }
                else kept.Add(Index(raw, t) + desc.baseVertex);
            }
            subCountOut[submesh] = kept.Count - subStart[submesh];
        }

        static int Index<T>(NativeArray<T> raw, int i) where T : struct
        {
            object v = raw[i];
            if (v is int n) return n;
            if (v is ushort u) return u;
            return Convert.ToInt32(v);
        }

        public static bool TryClip(Mesh src, out Mesh clipped, out int droppedTriangles)
        {
            clipped = null;
            droppedTriangles = 0;
            if (src == null) return false;
            Mesh.MeshDataArray read = default(Mesh.MeshDataArray);
            Mesh.MeshDataArray write = default(Mesh.MeshDataArray);
            bool haveRead = false, haveWrite = false;
            try
            {
                read = Mesh.AcquireReadOnlyMeshData(src);
                haveRead = true;
                var srcData = read[0];
                int vc = srcData.vertexCount;
                if (vc == 0 || !srcData.HasVertexAttribute(VertexAttribute.TexCoord0)) return false;
                var uv = new NativeArray<Vector2>(vc, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                srcData.GetUVs(0, uv);
                var inside = new bool[vc];
                int marked = 0;
                for (int i = 0; i < vc; i++)
                {
                    if (!InHeadIsland(uv[i].x, uv[i].y)) continue;
                    inside[i] = true;
                    marked++;
                }
                uv.Dispose();
                if (marked == 0) return false;

                int subCount = srcData.subMeshCount;
                bool src32 = srcData.indexFormat == IndexFormat.UInt32;
                var kept = new System.Collections.Generic.List<int>();
                int[] subStart = new int[subCount];
                int[] subCountOut = new int[subCount];
                MeshTopology[] topo = new MeshTopology[subCount];
                if (src32)
                {
                    var raw = srcData.GetIndexData<int>();
                    for (int s = 0; s < subCount; s++)
                        Gather(raw, s, srcData, vc, inside, kept, subStart, subCountOut, topo, ref droppedTriangles);
                }
                else
                {
                    var raw = srcData.GetIndexData<ushort>();
                    for (int s = 0; s < subCount; s++)
                        Gather(raw, s, srcData, vc, inside, kept, subStart, subCountOut, topo, ref droppedTriangles);
                }
                if (droppedTriangles == 0) return false;
                int[] packed = kept.ToArray();

                write = Mesh.AllocateWritableMeshData(1);
                haveWrite = true;
                var dst = write[0];
                var layout = src.GetVertexAttributes();
                dst.SetVertexBufferParams(vc, layout);
                int streams = srcData.vertexBufferCount;
                for (int stream = 0; stream < streams; stream++)
                {
                    var from = srcData.GetVertexData<byte>(stream);
                    var to = dst.GetVertexData<byte>(stream);
                    if (from.Length != to.Length) return false;
                    NativeArray<byte>.Copy(from, to);
                }
                dst.SetIndexBufferParams(packed.Length, IndexFormat.UInt32);
                var ib = dst.GetIndexData<int>();
                for (int i = 0; i < packed.Length; i++) ib[i] = packed[i];
                dst.subMeshCount = subCount;
                for (int s = 0; s < subCount; s++)
                    dst.SetSubMesh(s, new SubMeshDescriptor(subStart[s], subCountOut[s], topo[s]), MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                clipped = new Mesh { name = src.name + ClippedSuffix, hideFlags = HideFlags.HideAndDontSave };
                Mesh.ApplyAndDisposeWritableMeshData(write, clipped, MeshUpdateFlags.DontRecalculateBounds);
                haveWrite = false;
                clipped.bindposes = src.bindposes;
                clipped.bounds = src.bounds;
                read.Dispose();
                haveRead = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[HeadCoverage] " + src.name + " " + ex.Message);
                return false;
            }
            finally
            {
                if (haveWrite) write.Dispose();
                if (haveRead) read.Dispose();
            }
        }
    }
}
