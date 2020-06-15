using HSDRaw.Common;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MexTK.Tools
{
    /// <summary>
    /// TODO: This does not work properly
    /// </summary>
    public class SkeletonPorter
    {
        /// <summary>
        /// 
        /// </summary>
        private Dictionary<string, Matrix4x4> boneToReorient = new Dictionary<string, Matrix4x4>();
        private Dictionary<string, Matrix4x4> boneToReorientParent = new Dictionary<string, Matrix4x4>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="boneMapFrom"></param>
        /// <param name="boneMapTo"></param>
        public SkeletonPorter(HSD_JOBJ from, HSD_JOBJ to, BoneMap boneMapFrom, BoneMap boneMapTo)
        {
            var bonesFrom = from.BreathFirstList;
            var bonesTo = to.BreathFirstList;

            var fromDict = GetWorldTransforms(from, Matrix4x4.Identity);
            var toDict = GetWorldTransforms(to, Matrix4x4.Identity);

            foreach (var t in boneMapTo.GetNames())
            {
                var toIndex = boneMapTo.GetIndex(t);
                var fromIndex = boneMapFrom.GetIndex(t);
                if (toIndex != -1 && fromIndex != -1)
                {
                    var jobjFrom = bonesFrom[fromIndex];
                    var jobjTo = bonesTo[toIndex];

                    var transformFrom = fromDict[jobjFrom];
                    var transformTo = toDict[jobjTo];

                    System.Diagnostics.Debug.WriteLine(t + " " + transformFrom + " " + transformTo);

                    Matrix4x4.Invert(transformFrom.Item1, out Matrix4x4 invFrom1);
                    Matrix4x4.Invert(transformFrom.Item2, out Matrix4x4 invFrom2);

                    boneToReorient.Add(t,
                        invFrom1 *
                        transformTo.Item1);

                    boneToReorientParent.Add(t,
                        invFrom2 * 
                        transformTo.Item2);
                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bone"></param>
        /// <returns></returns>
        public bool HasBone(string name)
        {
            return boneToReorient.ContainsKey(name);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public Vector3 ReOrient(Vector3 v, string name)
        {
            if (HasBone(name))
                return Vector3.TransformNormal(v, boneToReorient[name]);

            return v;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public Vector3 ReOrientParent(Vector3 v, string name)
        {
            if (HasBone(name))
                return Vector3.TransformNormal(v, boneToReorientParent[name]);

            return v;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="current"></param>
        /// <param name="parent"></param>
        /// <param name="dict"></param>
        /// <returns></returns>
        private static Dictionary<HSD_JOBJ, Tuple<Matrix4x4, Matrix4x4>> GetWorldTransforms(HSD_JOBJ current, Matrix4x4 parent, Dictionary<HSD_JOBJ, Tuple<Matrix4x4, Matrix4x4>> dict = null)
        {
            if (dict == null)
                dict = new Dictionary<HSD_JOBJ, Tuple<Matrix4x4, Matrix4x4>>();

            Matrix4x4 Transform = Matrix4x4.CreateFromQuaternion(FromEulerAngles(current.RZ, current.RY, current.RX)) * parent;
            
            dict.Add(current, new Tuple<Matrix4x4, Matrix4x4>(Transform, parent));

            foreach (var c in current.Children)
                GetWorldTransforms(c, Transform, dict);

            return dict;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="z"></param>
        /// <param name="y"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        private static Quaternion FromEulerAngles(float z, float y, float x)
        {
            Quaternion xRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);
            Quaternion yRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, y);
            Quaternion zRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z);

            Quaternion q = (zRotation * yRotation * xRotation);

            return Quaternion.Inverse(q);
        }
    }
}
