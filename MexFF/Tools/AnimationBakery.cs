using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MexTK.Tools
{
    public class AnimationBakery
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="figatree"></param>
        /// <param name="jobjFrom"></param>
        /// <param name="jobjTo"></param>
        /// <param name="bmFrom"></param>
        /// <param name="bmTo"></param>
        /// <returns></returns>
        public static HSD_FigaTree Port(HSD_FigaTree figatree, HSD_JOBJ jobjFrom, HSD_JOBJ jobjTo, BoneMap bmFrom, BoneMap bmTo)
        {
            AnimationPlayer sourceAnim = new AnimationPlayer(jobjFrom, figatree);
            AnimationPlayer targetAnim = new AnimationPlayer(jobjTo, figatree.FrameCount);

            int jobjIndex = 0;
            foreach(var to in jobjTo.BreathFirstList)
            {
                var name = bmTo.GetName(jobjIndex);
                var index = bmFrom.GetIndex(name);

                FigaTreeNode node = new FigaTreeNode();

                if (index != -1)
                {
                    var jfrom = jobjFrom.BreathFirstList[index];

                    targetAnim.PortBoneTo(to, sourceAnim, jfrom);
                }

                jobjIndex++;
            }

            return targetAnim.ToFigaTree(); ;
        }

    }

    /// <summary>
    /// 
    /// </summary>
    public class AnimationPlayer
    {
        private Dictionary<HSD_JOBJ, TrackNode> jobjToTrack = new Dictionary<HSD_JOBJ, TrackNode>();

        private Dictionary<HSD_JOBJ, HSD_JOBJ> jobjToParent = new Dictionary<HSD_JOBJ, HSD_JOBJ>();

        public int FrameCount { get; internal set; } = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobj"></param>
        /// <param name="figatree"></param>
        public AnimationPlayer(HSD_JOBJ jobj, float frameCount)
        {
            FrameCount = (int)frameCount;
            foreach (var j in jobj.BreathFirstList)
            {
                TrackNode t = new TrackNode(j);
                jobjToTrack.Add(j, t);
            }
            LoadParents(jobj);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobj"></param>
        /// <param name="figatree"></param>
        public AnimationPlayer(HSD_JOBJ jobj, HSD_FigaTree figatree)
        {
            FrameCount = (int)figatree.FrameCount;

            int jobjIndex = 0;
            foreach(var j in jobj.BreathFirstList)
            {
                TrackNode t = new TrackNode(j);

                var node = figatree.Nodes[jobjIndex];

                foreach(var track in node.Tracks)
                {
                    var fobj = track.FOBJ;

                    switch (fobj.JointTrackType)
                    {
                        case JointTrackType.HSD_A_J_TRAX: t.X.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_TRAY: t.Y.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_TRAZ: t.Z.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_ROTX: t.RX.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_ROTY: t.RY.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_ROTZ: t.RZ.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_SCAX: t.SX.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_SCAY: t.SY.Keys = fobj.GetDecodedKeys(); break;
                        case JointTrackType.HSD_A_J_SCAZ: t.SZ.Keys = fobj.GetDecodedKeys(); break;
                    }
                }

                jobjToTrack.Add(j, t);

                jobjIndex++;
            }

            LoadParents(jobj);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobj"></param>
        /// <param name="parent"></param>
        private void LoadParents(HSD_JOBJ jobj, HSD_JOBJ parent = null)
        {
            jobjToParent.Add(jobj, parent);

            foreach (var c in jobj.Children)
                LoadParents(c, jobj);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Matrix4x4 GetAnimatedTransform(HSD_JOBJ jobj, int frame)
        {
            return jobjToTrack[jobj].GetTransformAt(frame);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Matrix4x4 GetAnimatedWorldTransform(HSD_JOBJ jobj, int frame)
        {
            return  jobjToTrack[jobj].GetTransformAt(frame) * (jobjToParent[jobj] != null ? GetAnimatedWorldTransform(jobjToParent[jobj], frame) : Matrix4x4.Identity);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Matrix4x4 GetWorldTransform(HSD_JOBJ jobj)
        {
            return TrackNode.CalculateMatrix(jobj) * (jobjToParent[jobj] != null ? GetWorldTransform(jobjToParent[jobj]) : Matrix4x4.Identity);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobj"></param>
        /// <param name="source"></param>
        /// <param name="source_jobj"></param>
        public void PortBoneTo(HSD_JOBJ jobj, AnimationPlayer source, HSD_JOBJ source_jobj)
        {
            if (jobjToParent[jobj] == null)
                return;

            var targetWorld = GetWorldTransform(jobj);
            var sourceWorld = source.GetWorldTransform(source_jobj);
            Matrix4x4.Invert(sourceWorld, out Matrix4x4 invSourceWorld);

            var targetRotation = TrackNode.FromEulerAngles(jobj.RZ, jobj.RY, jobj.RX);
            var sourceRotation = TrackNode.FromEulerAngles(source_jobj.RZ, source_jobj.RY, source_jobj.RX);
            sourceRotation = Quaternion.Inverse(sourceRotation);

            // if bones have same orientation we can copy keys directly
            if (ApproximatelySameOrientation(targetWorld, sourceWorld))
            {
               jobjToTrack[jobj] = source.jobjToTrack[source_jobj];
              return;
            }

            // otherwise bake
            List<Matrix4x4> transforms = new List<Matrix4x4>();
            for (int i = 0; i <= FrameCount; i++)
            {
                var inTargetParentWorld = GetAnimatedWorldTransform(jobjToParent[jobj], i);
                Matrix4x4.Invert(inTargetParentWorld, out inTargetParentWorld);

                var sourceAnimatedWorld = source.GetAnimatedWorldTransform(source_jobj, i);

                var rel = targetWorld * invSourceWorld * sourceAnimatedWorld;

                var newT = rel * inTargetParentWorld;
                
                var relTranslate = (new Vector3(source_jobj.TX, source_jobj.TY, source_jobj.TZ) - source.GetAnimatedTransform(source_jobj, i).Translation);
                relTranslate = Vector3.Transform(relTranslate, sourceRotation);
                relTranslate = Vector3.Transform(relTranslate, targetRotation);

                newT.Translation = new Vector3(jobj.TX, jobj.TY, jobj.TZ);// + relTranslate;
                
                transforms.Add(newT);
            }

            // then optimize
            jobjToTrack[jobj] = new TrackNode(transforms, source.jobjToTrack[source_jobj], source_jobj, jobj);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <returns></returns>
        private static bool ApproximatelySameOrientation(Matrix4x4 m1, Matrix4x4 m2)
        {
            var error = 0.01f;

            var q1 = Quaternion.CreateFromRotationMatrix(m1);
            var q2 = Quaternion.CreateFromRotationMatrix(m2);

            return Math.Abs(q1.X - q2.X) < error && Math.Abs(q1.Y - q2.Y) < error && Math.Abs(q1.Z - q2.Z) < error && Math.Abs(q1.W - q2.W) < error;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public HSD_FigaTree ToFigaTree()
        {
            List<FigaTreeNode> nodes = new List<FigaTreeNode>();

            foreach(var v in jobjToTrack)
            {
                FigaTreeNode node = new FigaTreeNode();

                CreateTrack(node, JointTrackType.HSD_A_J_TRAX, v.Value.X.Keys, v.Key.TX);
                CreateTrack(node, JointTrackType.HSD_A_J_TRAY, v.Value.Y.Keys, v.Key.TY);
                CreateTrack(node, JointTrackType.HSD_A_J_TRAZ, v.Value.Z.Keys, v.Key.TZ);

                CreateTrack(node, JointTrackType.HSD_A_J_ROTX, v.Value.RX.Keys, v.Key.RX);
                CreateTrack(node, JointTrackType.HSD_A_J_ROTY, v.Value.RY.Keys, v.Key.RY);
                CreateTrack(node, JointTrackType.HSD_A_J_ROTZ, v.Value.RZ.Keys, v.Key.RZ);

                CreateTrack(node, JointTrackType.HSD_A_J_SCAX, v.Value.SX.Keys, v.Key.SX);
                CreateTrack(node, JointTrackType.HSD_A_J_SCAY, v.Value.SY.Keys, v.Key.SY);
                CreateTrack(node, JointTrackType.HSD_A_J_SCAZ, v.Value.SZ.Keys, v.Key.SZ);

                nodes.Add(node);
            }

            HSD_FigaTree tree = new HSD_FigaTree();
            tree.Type = 1;
            tree.FrameCount = FrameCount;
            tree.Nodes = nodes;

            return tree;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="type"></param>
        /// <param name="keys"></param>
        /// <param name="defaultValue"></param>
        private void CreateTrack(FigaTreeNode node, JointTrackType type, List<FOBJKey> keys, float defaultValue)
        {
            // empty track
            if (keys.Count == 0)
                return;

            // skip constant tracks
            if (keys.Count == 1 && Math.Abs(keys[0].Value - defaultValue) < 0.001f)
                return;

            HSD_FOBJ fobj = new HSD_FOBJ();
            fobj.SetKeys(keys, type);

            HSD_Track track = new HSD_Track();
            track.FOBJ = fobj;

            node.Tracks.Add(track);
        }
    }

    public class TrackNode
    {
        public FOBJ_Player X = new FOBJ_Player();
        public FOBJ_Player Y = new FOBJ_Player();
        public FOBJ_Player Z = new FOBJ_Player();
        public FOBJ_Player RX = new FOBJ_Player();
        public FOBJ_Player RY = new FOBJ_Player();
        public FOBJ_Player RZ = new FOBJ_Player();
        public FOBJ_Player SX = new FOBJ_Player();
        public FOBJ_Player SY = new FOBJ_Player();
        public FOBJ_Player SZ = new FOBJ_Player();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="joint"></param>
        public TrackNode(HSD_JOBJ joint)
        {
            X.Keys = GenerateKEY(joint.TX);
            Y.Keys = GenerateKEY(joint.TY);
            Z.Keys = GenerateKEY(joint.TZ);
            RX.Keys = GenerateKEY(joint.RX);
            RY.Keys = GenerateKEY(joint.RY);
            RZ.Keys = GenerateKEY(joint.RZ);
            SX.Keys = GenerateKEY(joint.SX);
            SY.Keys = GenerateKEY(joint.SY);
            SZ.Keys = GenerateKEY(joint.SZ);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mat"></param>
        public TrackNode(List<Matrix4x4> transforms, TrackNode sourceTrack, HSD_JOBJ source, HSD_JOBJ dest)
        {

            List<float> x = new List<float>();
            List<float> y = new List<float>();
            List<float> z = new List<float>();
            List<float> rx = new List<float>();
            List<float> ry = new List<float>();
            List<float> rz = new List<float>();
            List<float> sx = new List<float>();
            List<float> sy = new List<float>();
            List<float> sz = new List<float>();

            foreach (var t in transforms)
            {
                Vector3 sca, tra;
                Quaternion quat;
                Matrix4x4.Decompose(t, out sca, out quat, out tra);
                Vector3 rot = ToEulerAngles(quat);

                tra = t.Translation;

                x.Add(tra.X); y.Add(tra.Y); z.Add(tra.Z);
                rx.Add(rot.X); ry.Add(rot.Y); rz.Add(rot.Z);
                sx.Add(sca.X); sy.Add(sca.Y); sz.Add(sca.Z);
            }

            // check if any existing tracks match up and use those tracks when possible
            var xtrack = sourceTrack.GetExistingTrack(x, source, dest);
            var ytrack = sourceTrack.GetExistingTrack(y, source, dest);
            var ztrack = sourceTrack.GetExistingTrack(z, source, dest);
            var rxtrack = sourceTrack.GetExistingTrack(rx, source, dest);
            var rytrack = sourceTrack.GetExistingTrack(ry, source, dest);
            var rztrack = sourceTrack.GetExistingTrack(rz, source, dest);
            var sxtrack = sourceTrack.GetExistingTrack(sx, source, dest);
            var sytrack = sourceTrack.GetExistingTrack(sy, source, dest);
            var sztrack = sourceTrack.GetExistingTrack(sz, source, dest);

            /*if (dest.ClassName != null && dest.ClassName == "RLegJA")
            {
                System.Diagnostics.Debug.WriteLine(dest.ClassName);

                System.Diagnostics.Debug.WriteLine($"{source.RX} {source.RY} {source.RZ}");
                System.Diagnostics.Debug.WriteLine($"{dest.RX} {dest.RY} {dest.RZ}");

                Matrix4x4.Decompose(transforms[0], out Vector3 relScale, out Quaternion rot, out Vector3 trans);

                System.Diagnostics.Debug.WriteLine($"{TrackNode.ToEulerAngles(rot).ToString()}");
                System.Diagnostics.Debug.WriteLine($"{sourceTrack.RX.Keys[0].Value} {sourceTrack.RY.Keys[0].Value} {sourceTrack.RZ.Keys[0].Value}");

                System.Diagnostics.Debug.WriteLine($"{rxtrack != null} {rytrack != null} {rztrack != null}");
            }*/

            if (xtrack != null || ytrack != null || ztrack != null ||
                rxtrack != null || rytrack != null || rztrack != null ||
                sxtrack != null || sytrack != null || sztrack != null)
            {
                //System.Diagnostics.Debug.WriteLine("Found Existing Track");
            }

            // otherwise we have to approximate

            var trans_error = 0.05f;
            var rot_error = 0.05f;
            var scale_error = 0.1f;
            
            X.Keys = xtrack != null ? xtrack : LineSimplification.Simplify(x, trans_error);
            Y.Keys = ytrack != null ? ytrack : LineSimplification.Simplify(y, trans_error);
            Z.Keys = ztrack != null ? ztrack : LineSimplification.Simplify(z, trans_error);
            RX.Keys = rxtrack != null ? rxtrack : LineSimplification.Simplify(rx, rot_error);
            RY.Keys = rytrack != null ? rytrack : LineSimplification.Simplify(ry, rot_error);
            RZ.Keys = rztrack != null ? rztrack : LineSimplification.Simplify(rz, rot_error);
            SX.Keys = sxtrack != null ? sxtrack : LineSimplification.Simplify(sx, scale_error, true);
            SY.Keys = sytrack != null ? sytrack : LineSimplification.Simplify(sy, scale_error, true);
            SZ.Keys = sztrack != null ? sztrack : LineSimplification.Simplify(sz, scale_error, true);
            
            /*
            X.Keys = xtrack != null && xtrack.Count < transforms.Count * 0.85f ? xtrack : LineSimplification.Simplify(x, trans_error);
            Y.Keys = ytrack != null && ytrack.Count < transforms.Count * 0.85f ? ytrack : LineSimplification.Simplify(y, trans_error);
            Z.Keys = ztrack != null && ztrack.Count < transforms.Count * 0.85f ? ztrack : LineSimplification.Simplify(z, trans_error);
            RX.Keys = rxtrack != null && rxtrack.Count < transforms.Count * 0.85f ? rxtrack : LineSimplification.Simplify(rx, rot_error);
            RY.Keys = rytrack != null && rytrack.Count < transforms.Count * 0.85f ? rytrack : LineSimplification.Simplify(ry, rot_error);
            RZ.Keys = rztrack != null && rztrack.Count < transforms.Count * 0.85f ? rztrack : LineSimplification.Simplify(rz, rot_error);
            SX.Keys = sxtrack != null && sxtrack.Count < transforms.Count * 0.85f ? sxtrack : LineSimplification.Simplify(sx, scale_error, true);
            SY.Keys = sytrack != null && sytrack.Count < transforms.Count * 0.85f ? sytrack : LineSimplification.Simplify(sy, scale_error, true);
            SZ.Keys = sztrack != null && sztrack.Count < transforms.Count * 0.85f ? sztrack : LineSimplification.Simplify(sz, scale_error, true);
            */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        private List<FOBJKey> GetExistingTrack(List<float> keys, HSD_JOBJ source, HSD_JOBJ dest)
        {
            /*if (KeysEqual(keys, X, source.TX, dest.TX)) return X.Keys;
            if (KeysEqual(keys, Y, source.TY, dest.TY)) return Y.Keys;
            if (KeysEqual(keys, Z, source.TZ, dest.TZ)) return Z.Keys;
            if (KeysEqual(keys, RX, source.RX, dest.RX)) return RX.Keys;
            if (KeysEqual(keys, RY, source.RY, dest.RY)) return RY.Keys;
            if (KeysEqual(keys, RZ, source.RZ, dest.RZ)) return RZ.Keys;
            if (KeysEqual(keys, SX, source.SX, dest.SX)) return SX.Keys;
            if (KeysEqual(keys, SY, source.SY, dest.SY)) return SY.Keys;
            if (KeysEqual(keys, SZ, source.SZ, dest.SZ)) return SZ.Keys;*/

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keys1"></param>
        /// <param name="keys2"></param>
        /// <returns></returns>
        private bool KeysEqual(List<float> keys, FOBJ_Player player, float source, float dest)
        {
            if(keys.Count == 0)
                return false;

            var negPass = true;
            var pass = true;

            for (int i = 0; i < keys.Count; i++)
            {
                if (Math.Abs(keys[i] - player.GetValue(i)) > 0.05f)
                    pass = false;

                if (Math.Abs(keys[i] + player.GetValue(i)) > 0.05f)
                    negPass = false;

                if (!pass && !negPass)
                    break;
            }

            if (pass) return true;

            //if (negPass) return true;

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Matrix4x4 GetTransformAt(int frame)
        {
            return Matrix4x4.CreateScale(SX.GetValue(frame), SY.GetValue(frame), SZ.GetValue(frame)) *
                Matrix4x4.CreateFromQuaternion(FromEulerAngles(RZ.GetValue(frame), RY.GetValue(frame), RX.GetValue(frame))) *
                Matrix4x4.CreateTranslation(X.GetValue(frame), Y.GetValue(frame), Z.GetValue(frame));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static Matrix4x4 CalculateMatrix(HSD_JOBJ jobj)
        {
            return Matrix4x4.CreateScale(jobj.SX, jobj.SY, jobj.SZ) *
                Matrix4x4.CreateFromQuaternion(FromEulerAngles(jobj.RZ, jobj.RY, jobj.RX)) *
                Matrix4x4.CreateTranslation(jobj.TX, jobj.TY, jobj.TZ);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        public static Vector3 ToEulerAngles(Quaternion q)
        {
            q = Quaternion.Inverse(q);
            Matrix4x4 mat = Matrix4x4.CreateFromQuaternion(q);
            float x, y, z;

            y = (float)Math.Asin(-Clamp(mat.M31, -1, 1));

            if (Math.Abs(mat.M31) < 0.99999)
            {
                x = (float)Math.Atan2(mat.M32, mat.M33);
                z = (float)Math.Atan2(mat.M21, mat.M11);
            }
            else
            {
                x = 0;
                z = (float)Math.Atan2(-mat.M12, mat.M22);
            }
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="z"></param>
        /// <param name="y"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        public static Quaternion FromEulerAngles(float z, float y, float x)
        {
            Quaternion xRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);
            Quaternion yRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, y);
            Quaternion zRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z);

            Quaternion q = (zRotation * yRotation * xRotation);

            return q;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="trackType"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        private List<FOBJKey> GenerateKEY(float defaultValue)
        {
            List<FOBJKey> keys = new List<FOBJKey>();

            keys.Add(new FOBJKey() { Value = defaultValue, InterpolationType = GXInterpolationType.HSD_A_OP_KEY });
            
            return keys;
        }
    }
}
