using HSDRaw.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MexTK.Tools
{
    public class LineSimplification
    {
        public class KeyAnalyzer
        {
            private List<FOBJKey> Keys;

            private FOBJ_Player player = new FOBJ_Player();

            public float FrameCount { get; internal set; }

            public float MaxValue { get; internal set; }

            public float MinValue { get; internal set; }

            public KeyAnalyzer(List<FOBJKey> keys)
            {
                Keys = keys;
                player.Keys = Keys;
                Analyze();
            }

            private float GetValue(float frame)
            {
                return player.GetValue(frame);
            }

            private void Analyze()
            {
                if (Keys.Count == 0)
                    return;
                FrameCount = Keys.Max(e => e.Frame);
                MaxValue = Keys.Max(e => e.Value);
                MinValue = Keys.Min(e => e.Value);
            }

            public static void GetMaxError(KeyAnalyzer line, KeyAnalyzer newKeys, out float error, out int index)
            {
                var fcount = Math.Min(newKeys.FrameCount, line.FrameCount);

                error = 0;
                index = -1;

                for (int i = 0; i < fcount; i++)
                {
                    var orgValue = line.GetValue(i);
                    var newValue = newKeys.GetValue(i);
                    var er = Math.Abs(newValue - orgValue);

                    if (er > error)
                    {
                        error = er;
                        index = i;
                    }
                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keys"></param>
        public static void CalculateSlopes(List<FOBJKey> keys)
        {
            float[] x = keys.Select(e => e.Frame).ToArray();
            float[] y = keys.Select(e => e.Value).ToArray();

            CubicSpline spline = new CubicSpline();
            spline.Fit(x, y);

            float[] slope = spline.EvalSlope(x);

            List<FOBJKey> newKeys = new List<FOBJKey>();

            for (int i = 0; i < x.Length; i++)
                if (!float.IsNaN(slope[i]))
                    keys[i].Tan = slope[i];// (float)Math.Tan(slope[i] * Math.PI / 180);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static FOBJKey CloneKey(FOBJKey key)
        {
            return new FOBJKey()
            {
                Frame = key.Frame,
                Value = key.Value,
                Tan = key.Tan,
                InterpolationType = key.InterpolationType
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
        public static List<FOBJKey> Simplify(List<float> keys, float maxError, bool forceLinear = false)
        {
            return Simplify(keys.Select((e, i) => new FOBJKey() { Frame = i, Value = e, InterpolationType = forceLinear ? HSDRaw.Common.Animation.GXInterpolationType.HSD_A_OP_LIN : HSDRaw.Common.Animation.GXInterpolationType.HSD_A_OP_SPL }).ToList(), maxError);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="values"></param>
        public static List<FOBJKey> Simplify(List<FOBJKey> keys, float maxError)
        {
            // check constant key track{
            {
                bool keyTrack = true;
                for (int i = 1; i < keys.Count; i++)
                {
                    if (Math.Abs(keys[i].Value - keys[0].Value) > 0.001f)
                    {
                        keyTrack = false;
                        break;
                    }
                }
                if (keyTrack)
                {
                    var key = keys[0];
                    key.InterpolationType = HSDRaw.Common.Animation.GXInterpolationType.HSD_A_OP_KEY;
                    keys.Clear();
                    keys.Add(key);
                    return keys;
                }
            }


            CalculateSlopes(keys);
            var newKeys = new List<FOBJKey>();

            // find constant and linear keys
            {
                newKeys.Add(CloneKey(keys[0]));
                for (int i = 1; i < keys.Count - 2; i++)
                {
                    // this key is constant, so skip
                    if (Math.Abs(keys[i].Value - keys[i - 1].Value) < 0.00001f)
                        continue;

                    // step check
                    if (Math.Abs(keys[i].Value - keys[i + 1].Value) < 0.00001f)
                    {
                        /*var nkey = CloneKey(keys[i]);
                        nkey.InterpolationType = HSDRaw.Common.Animation.GXInterpolationType.HSD_A_OP_CON;
                        nkey.Tan = 0;
                        newKeys.Add(nkey);*/
                        continue;
                    }

                    //TODO: lerp detection
                    // linear check
                    /*float FrameDiff = (i + 1) - keys[i].Frame;
                    float Weight = FrameDiff / (keys[i + 1].Frame - keys[i].Frame);

                    var lerpIndex = i + 1;
                    while (lerpIndex < keys.Count - 1 && AnimationInterpolationHelper.Lerp(keys[i].Value, keys[lerpIndex].Value, Weight) == keys[lerpIndex].Value)
                    {
                        lerpIndex++;
                        FrameDiff = lerpIndex - keys[i].Frame;
                        Weight = FrameDiff / (keys[lerpIndex].Frame - keys[i].Frame);
                    }

                    // if this key is linear then skip linear keys and change interpolation type
                    if (lerpIndex > i + 2)
                    {
                        System.Diagnostics.Debug.WriteLine("Linear");
                        var nkey = CloneKey(keys[i]);
                        nkey.InterpolationType = HSDRaw.Common.Animation.GXInterpolationType.HSD_A_OP_LIN;
                        nkey.Tan = 0;
                        i = lerpIndex - 1;
                        newKeys.Add(nkey);
                    }*/
                }
                newKeys.Add(CloneKey(keys[keys.Count - 1]));
            }

            // prepare analyzing
            var analyzer1 = new KeyAnalyzer(keys);
            var analyzer2 = new KeyAnalyzer(newKeys);

            // insert keys until error is reduced within threshold
            float error = float.MaxValue;
            var index = 0;
            int prevIndex = 0;
            while (error > maxError)
            {
                KeyAnalyzer.GetMaxError(analyzer1, analyzer2, out error, out index);

                if (index == prevIndex || index >= keys.Count || index < 0)
                    break;

                var loc = newKeys.FindIndex(e => keys[index].Frame <= e.Frame);

                if (loc != -1)
                    newKeys.Insert(loc, CloneKey(keys[index]));
                else
                    break;

                prevIndex = index;
            }

            int removedLinear = 0;

            // use linear where possible
            {
                for (int j = 0; j < newKeys.Count - 1; j++)
                {
                    var k = newKeys[j];

                    if ((newKeys[j + 1].Frame - k.Frame) <= 2)
                    {
                        k.InterpolationType = HSDRaw.Common.Animation.GXInterpolationType.HSD_A_OP_LIN;
                        removedLinear++;
                    }
                }
                //Console.WriteLine($"Removed {removed} keys");
            }

            int removed = 0;
            // cleanup pass, make sure all frames are necessary
            List<FOBJKey> finalKeys = new List<FOBJKey>();
            finalKeys.Add(keys[0]);
            for (int j = 1; j < newKeys.Count - 1; j++)
            {
                var k = newKeys[j];
                analyzer1 = new KeyAnalyzer(newKeys.Where(e => e != k).ToList());

                KeyAnalyzer.GetMaxError(analyzer1, analyzer2, out error, out index);

                if (error < maxError)
                    removed++;
                else
                    finalKeys.Add(k);
            }
            finalKeys.Add(keys[keys.Count - 1]);
            newKeys = finalKeys;

            Debug.WriteLine($"Removed {removed} keys converted {removedLinear} to linear");

            // make sure to always have final key
            //if (!newKeys.Contains(keys[keys.Count - 1]))
            //    newKeys.Add(keys[keys.Count - 1]);

            return newKeys;
        }
    }
    /// <summary>
    /// Cubic spline interpolation.
    /// Call Fit (or use the corrector constructor) to compute spline coefficients, then Eval to evaluate the spline at other X coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is implemented based on the wikipedia article:
    /// http://en.wikipedia.org/wiki/Spline_interpolation
    /// I'm not sure I have the right to include a copy of the article so the equation numbers referenced in 
    /// comments will end up being wrong at some point.
    /// </para>
    /// <para>
    /// This is not optimized, and is not MT safe.
    /// This can extrapolate off the ends of the splines.
    /// You must provide points in X sort order.
    /// </para>
    /// </remarks>
    public class CubicSpline
    {
        #region Fields

        // N-1 spline coefficients for N points
        private float[] a;
        private float[] b;

        // Save the original x and y for Eval
        private float[] xOrig;
        private float[] yOrig;

        #endregion

        #region Ctor

        /// <summary>
        /// Default ctor.
        /// </summary>
        public CubicSpline()
        {
        }

        /// <summary>
        /// Construct and call Fit.
        /// </summary>
        /// <param name="x">Input. X coordinates to fit.</param>
        /// <param name="y">Input. Y coordinates to fit.</param>
        /// <param name="startSlope">Optional slope constraint for the first point. Single.NaN means no constraint.</param>
        /// <param name="endSlope">Optional slope constraint for the final point. Single.NaN means no constraint.</param>
        /// <param name="debug">Turn on console output. Default is false.</param>
        public CubicSpline(float[] x, float[] y, float startSlope = float.NaN, float endSlope = float.NaN, bool debug = false)
        {
            Fit(x, y, startSlope, endSlope, debug);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Throws if Fit has not been called.
        /// </summary>
        private void CheckAlreadyFitted()
        {
            if (a == null) throw new Exception("Fit must be called before you can evaluate.");
        }

        private int _lastIndex = 0;

        /// <summary>
        /// Find where in xOrig the specified x falls, by simultaneous traverse.
        /// This allows xs to be less than x[0] and/or greater than x[n-1]. So allows extrapolation.
        /// This keeps state, so requires that x be sorted and xs called in ascending order, and is not multi-thread safe.
        /// </summary>
        private int GetNextXIndex(float x)
        {
            if (x < xOrig[_lastIndex])
            {
                throw new ArgumentException("The X values to evaluate must be sorted.");
            }

            while ((_lastIndex < xOrig.Length - 2) && (x > xOrig[_lastIndex + 1]))
            {
                _lastIndex++;
            }

            return _lastIndex;
        }

        /// <summary>
        /// Evaluate the specified x value using the specified spline.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="j">Which spline to use.</param>
        /// <param name="debug">Turn on console output. Default is false.</param>
        /// <returns>The y value.</returns>
        private float EvalSpline(float x, int j, bool debug = false)
        {
            float dx = xOrig[j + 1] - xOrig[j];
            float t = (x - xOrig[j]) / dx;
            float y = (1 - t) * yOrig[j] + t * yOrig[j + 1] + t * (1 - t) * (a[j] * (1 - t) + b[j] * t); // equation 9
            if (debug) Console.WriteLine("xs = {0}, j = {1}, t = {2}", x, j, t);
            return y;
        }

        #endregion

        #region Fit*

        /// <summary>
        /// Fit x,y and then eval at points xs and return the corresponding y's.
        /// This does the "natural spline" style for ends.
        /// This can extrapolate off the ends of the splines.
        /// You must provide points in X sort order.
        /// </summary>
        /// <param name="x">Input. X coordinates to fit.</param>
        /// <param name="y">Input. Y coordinates to fit.</param>
        /// <param name="xs">Input. X coordinates to evaluate the fitted curve at.</param>
        /// <param name="startSlope">Optional slope constraint for the first point. Single.NaN means no constraint.</param>
        /// <param name="endSlope">Optional slope constraint for the final point. Single.NaN means no constraint.</param>
        /// <param name="debug">Turn on console output. Default is false.</param>
        /// <returns>The computed y values for each xs.</returns>
        public float[] FitAndEval(float[] x, float[] y, float[] xs, float startSlope = float.NaN, float endSlope = float.NaN, bool debug = false)
        {
            Fit(x, y, startSlope, endSlope, debug);
            return Eval(xs, debug);
        }

        /// <summary>
        /// Compute spline coefficients for the specified x,y points.
        /// This does the "natural spline" style for ends.
        /// This can extrapolate off the ends of the splines.
        /// You must provide points in X sort order.
        /// </summary>
        /// <param name="x">Input. X coordinates to fit.</param>
        /// <param name="y">Input. Y coordinates to fit.</param>
        /// <param name="startSlope">Optional slope constraint for the first point. Single.NaN means no constraint.</param>
        /// <param name="endSlope">Optional slope constraint for the final point. Single.NaN means no constraint.</param>
        /// <param name="debug">Turn on console output. Default is false.</param>
        public void Fit(float[] x, float[] y, float startSlope = float.NaN, float endSlope = float.NaN, bool debug = false)
        {
            if (Single.IsInfinity(startSlope) || Single.IsInfinity(endSlope))
            {
                throw new Exception("startSlope and endSlope cannot be infinity.");
            }

            // Save x and y for eval
            this.xOrig = x;
            this.yOrig = y;

            int n = x.Length;
            float[] r = new float[n]; // the right hand side numbers: wikipedia page overloads b

            TriDiagonalMatrixF m = new TriDiagonalMatrixF(n);
            float dx1, dx2, dy1, dy2;

            // First row is different (equation 16 from the article)
            if (float.IsNaN(startSlope))
            {
                dx1 = x[1] - x[0];
                m.C[0] = 1.0f / dx1;
                m.B[0] = 2.0f * m.C[0];
                r[0] = 3 * (y[1] - y[0]) / (dx1 * dx1);
            }
            else
            {
                m.B[0] = 1;
                r[0] = startSlope;
            }

            // Body rows (equation 15 from the article)
            for (int i = 1; i < n - 1; i++)
            {
                dx1 = x[i] - x[i - 1];
                dx2 = x[i + 1] - x[i];

                m.A[i] = 1.0f / dx1;
                m.C[i] = 1.0f / dx2;
                m.B[i] = 2.0f * (m.A[i] + m.C[i]);

                dy1 = y[i] - y[i - 1];
                dy2 = y[i + 1] - y[i];
                r[i] = 3 * (dy1 / (dx1 * dx1) + dy2 / (dx2 * dx2));
            }

            // Last row also different (equation 17 from the article)
            if (float.IsNaN(endSlope))
            {
                dx1 = x[n - 1] - x[n - 2];
                dy1 = y[n - 1] - y[n - 2];
                m.A[n - 1] = 1.0f / dx1;
                m.B[n - 1] = 2.0f * m.A[n - 1];
                r[n - 1] = 3 * (dy1 / (dx1 * dx1));
            }
            else
            {
                m.B[n - 1] = 1;
                r[n - 1] = endSlope;
            }

            //if (debug) Console.WriteLine("Tri-diagonal matrix:\n{0}", m.ToDisplayString(":0.0000", "  "));
            //if (debug) Console.WriteLine("r: {0}", ArrayUtil.ToString<float>(r));

            // k is the solution to the matrix
            float[] k = m.Solve(r);
            //if (debug) Console.WriteLine("k = {0}", ArrayUtil.ToString<float>(k));

            // a and b are each spline's coefficients
            this.a = new float[n - 1];
            this.b = new float[n - 1];

            for (int i = 1; i < n; i++)
            {
                dx1 = x[i] - x[i - 1];
                dy1 = y[i] - y[i - 1];
                a[i - 1] = k[i - 1] * dx1 - dy1; // equation 10 from the article
                b[i - 1] = -k[i] * dx1 + dy1; // equation 11 from the article
            }

            //if (debug) Console.WriteLine("a: {0}", ArrayUtil.ToString<float>(a));
            //if (debug) Console.WriteLine("b: {0}", ArrayUtil.ToString<float>(b));
        }

        #endregion

        #region Eval*

        /// <summary>
        /// Evaluate the spline at the specified x coordinates.
        /// This can extrapolate off the ends of the splines.
        /// You must provide X's in ascending order.
        /// The spline must already be computed before calling this, meaning you must have already called Fit() or FitAndEval().
        /// </summary>
        /// <param name="x">Input. X coordinates to evaluate the fitted curve at.</param>
        /// <param name="debug">Turn on console output. Default is false.</param>
        /// <returns>The computed y values for each x.</returns>
        public float[] Eval(float[] x, bool debug = false)
        {
            CheckAlreadyFitted();

            int n = x.Length;
            float[] y = new float[n];
            _lastIndex = 0; // Reset simultaneous traversal in case there are multiple calls

            for (int i = 0; i < n; i++)
            {
                // Find which spline can be used to compute this x (by simultaneous traverse)
                int j = GetNextXIndex(x[i]);

                // Evaluate using j'th spline
                y[i] = EvalSpline(x[i], j, debug);
            }

            return y;
        }

        /// <summary>
        /// Evaluate (compute) the slope of the spline at the specified x coordinates.
        /// This can extrapolate off the ends of the splines.
        /// You must provide X's in ascending order.
        /// The spline must already be computed before calling this, meaning you must have already called Fit() or FitAndEval().
        /// </summary>
        /// <param name="x">Input. X coordinates to evaluate the fitted curve at.</param>
        /// <param name="debug">Turn on console output. Default is false.</param>
        /// <returns>The computed y values for each x.</returns>
        public float[] EvalSlope(float[] x, bool debug = false)
        {
            CheckAlreadyFitted();

            int n = x.Length;
            float[] qPrime = new float[n];
            _lastIndex = 0; // Reset simultaneous traversal in case there are multiple calls

            for (int i = 0; i < n; i++)
            {
                // Find which spline can be used to compute this x (by simultaneous traverse)
                int j = GetNextXIndex(x[i]);

                // Evaluate using j'th spline
                float dx = xOrig[j + 1] - xOrig[j];
                float dy = yOrig[j + 1] - yOrig[j];
                float t = (x[i] - xOrig[j]) / dx;

                // From equation 5 we could also compute q' (qp) which is the slope at this x
                qPrime[i] = dy / dx
                    + (1 - 2 * t) * (a[j] * (1 - t) + b[j] * t) / dx
                    + t * (1 - t) * (b[j] - a[j]) / dx;

                if (debug) Console.WriteLine("[{0}]: xs = {1}, j = {2}, t = {3}", i, x[i], j, t);
            }

            return qPrime;
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Static all-in-one method to fit the splines and evaluate at X coordinates.
        /// </summary>
        /// <param name="x">Input. X coordinates to fit.</param>
        /// <param name="y">Input. Y coordinates to fit.</param>
        /// <param name="xs">Input. X coordinates to evaluate the fitted curve at.</param>
        /// <param name="startSlope">Optional slope constraint for the first point. Single.NaN means no constraint.</param>
        /// <param name="endSlope">Optional slope constraint for the final point. Single.NaN means no constraint.</param>
        /// <param name="debug">Turn on console output. Default is false.</param>
        /// <returns>The computed y values for each xs.</returns>
        public static float[] Compute(float[] x, float[] y, float[] xs, float startSlope = float.NaN, float endSlope = float.NaN, bool debug = false)
        {
            CubicSpline spline = new CubicSpline();
            return spline.FitAndEval(x, y, xs, startSlope, endSlope, debug);
        }

        /// <summary>
        /// Fit the input x,y points using the parametric approach, so that y does not have to be an explicit
        /// function of x, meaning there does not need to be a single value of y for each x.
        /// </summary>
        /// <param name="x">Input x coordinates.</param>
        /// <param name="y">Input y coordinates.</param>
        /// <param name="nOutputPoints">How many output points to create.</param>
        /// <param name="xs">Output (interpolated) x values.</param>
        /// <param name="ys">Output (interpolated) y values.</param>
        /// <param name="firstDx">Optionally specifies the first point's slope in combination with firstDy. Together they
        /// are a vector describing the direction of the parametric spline of the starting point. The vector does
        /// not need to be normalized. If either is NaN then neither is used.</param>
        /// <param name="firstDy">See description of dx0.</param>
        /// <param name="lastDx">Optionally specifies the last point's slope in combination with lastDy. Together they
        /// are a vector describing the direction of the parametric spline of the last point. The vector does
        /// not need to be normalized. If either is NaN then neither is used.</param>
        /// <param name="lastDy">See description of dxN.</param>
        public static void FitParametric(float[] x, float[] y, int nOutputPoints, out float[] xs, out float[] ys,
            float firstDx = Single.NaN, float firstDy = Single.NaN, float lastDx = Single.NaN, float lastDy = Single.NaN)
        {
            // Compute distances
            int n = x.Length;
            float[] dists = new float[n]; // cumulative distance
            dists[0] = 0;
            float totalDist = 0;

            for (int i = 1; i < n; i++)
            {
                float dx = x[i] - x[i - 1];
                float dy = y[i] - y[i - 1];
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                totalDist += dist;
                dists[i] = totalDist;
            }

            // Create 'times' to interpolate to
            float dt = totalDist / (nOutputPoints - 1);
            float[] times = new float[nOutputPoints];
            times[0] = 0;

            for (int i = 1; i < nOutputPoints; i++)
            {
                times[i] = times[i - 1] + dt;
            }

            // Normalize the slopes, if specified
            NormalizeVector(ref firstDx, ref firstDy);
            NormalizeVector(ref lastDx, ref lastDy);

            // Spline fit both x and y to times
            CubicSpline xSpline = new CubicSpline();
            xs = xSpline.FitAndEval(dists, x, times, firstDx / dt, lastDx / dt);

            CubicSpline ySpline = new CubicSpline();
            ys = ySpline.FitAndEval(dists, y, times, firstDy / dt, lastDy / dt);
        }

        private static void NormalizeVector(ref float dx, ref float dy)
        {
            if (!Single.IsNaN(dx) && !Single.IsNaN(dy))
            {
                float d = (float)Math.Sqrt(dx * dx + dy * dy);

                if (d > Single.Epsilon) // probably not conservative enough, but catches the (0,0) case at least
                {
                    dx = dx / d;
                    dy = dy / d;
                }
                else
                {
                    throw new ArgumentException("The input vector is too small to be normalized.");
                }
            }
            else
            {
                // In case one is NaN and not the other
                dx = dy = Single.NaN;
            }
        }

        #endregion
    }


    /// <summary>
    /// A tri-diagonal matrix has non-zero entries only on the main diagonal, the diagonal above the main (super), and the
    /// diagonal below the main (sub).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is based on the wikipedia article: http://en.wikipedia.org/wiki/Tridiagonal_matrix_algorithm
    /// </para>
    /// <para>
    /// The entries in the matrix on a particular row are A[i], B[i], and C[i] where i is the row index.
    /// B is the main diagonal, and so for an NxN matrix B is length N and all elements are used.
    /// So for row 0, the first two values are B[0] and C[0].
    /// And for row N-1, the last two values are A[N-1] and B[N-1].
    /// That means that A[0] is not actually on the matrix and is therefore never used, and same with C[N-1].
    /// </para>
    /// </remarks>
    public class TriDiagonalMatrixF
    {
        /// <summary>
        /// The values for the sub-diagonal. A[0] is never used.
        /// </summary>
        public float[] A;

        /// <summary>
        /// The values for the main diagonal.
        /// </summary>
        public float[] B;

        /// <summary>
        /// The values for the super-diagonal. C[C.Length-1] is never used.
        /// </summary>
        public float[] C;

        /// <summary>
        /// The width and height of this matrix.
        /// </summary>
        public int N
        {
            get { return (A != null ? A.Length : 0); }
        }

        /// <summary>
        /// Indexer. Setter throws an exception if you try to set any not on the super, main, or sub diagonals.
        /// </summary>
        public float this[int row, int col]
        {
            get
            {
                int di = row - col;

                if (di == 0)
                {
                    return B[row];
                }
                else if (di == -1)
                {
                    Debug.Assert(row < N - 1);
                    return C[row];
                }
                else if (di == 1)
                {
                    Debug.Assert(row > 0);
                    return A[row];
                }
                else return 0;
            }
            set
            {
                int di = row - col;

                if (di == 0)
                {
                    B[row] = value;
                }
                else if (di == -1)
                {
                    Debug.Assert(row < N - 1);
                    C[row] = value;
                }
                else if (di == 1)
                {
                    Debug.Assert(row > 0);
                    A[row] = value;
                }
                else
                {
                    throw new ArgumentException("Only the main, super, and sub diagonals can be set.");
                }
            }
        }

        /// <summary>
        /// Construct an NxN matrix.
        /// </summary>
        public TriDiagonalMatrixF(int n)
        {
            this.A = new float[n];
            this.B = new float[n];
            this.C = new float[n];
        }

        /// <summary>
        /// Produce a string representation of the contents of this matrix.
        /// </summary>
        /// <param name="fmt">Optional. For String.Format. Must include the colon. Examples are ':0.000' and ',5:0.00' </param>
        /// <param name="prefix">Optional. Per-line indentation prefix.</param>
        public string ToDisplayString(string fmt = "", string prefix = "")
        {
            if (this.N > 0)
            {
                var s = new StringBuilder();
                string formatString = "{0" + fmt + "}";

                for (int r = 0; r < N; r++)
                {
                    s.Append(prefix);

                    for (int c = 0; c < N; c++)
                    {
                        s.AppendFormat(formatString, this[r, c]);
                        if (c < N - 1) s.Append(", ");
                    }

                    s.AppendLine();
                }

                return s.ToString();
            }
            else
            {
                return prefix + "0x0 Matrix";
            }
        }

        /// <summary>
        /// Solve the system of equations this*x=d given the specified d.
        /// </summary>
        /// <remarks>
        /// Uses the Thomas algorithm described in the wikipedia article: http://en.wikipedia.org/wiki/Tridiagonal_matrix_algorithm
        /// Not optimized. Not destructive.
        /// </remarks>
        /// <param name="d">Right side of the equation.</param>
        public float[] Solve(float[] d)
        {
            int n = this.N;

            if (d.Length != n)
            {
                throw new ArgumentException("The input d is not the same size as this matrix.");
            }

            // cPrime
            float[] cPrime = new float[n];
            cPrime[0] = C[0] / B[0];

            for (int i = 1; i < n; i++)
            {
                cPrime[i] = C[i] / (B[i] - cPrime[i - 1] * A[i]);
            }

            // dPrime
            float[] dPrime = new float[n];
            dPrime[0] = d[0] / B[0];

            for (int i = 1; i < n; i++)
            {
                dPrime[i] = (d[i] - dPrime[i - 1] * A[i]) / (B[i] - cPrime[i - 1] * A[i]);
            }

            // Back substitution
            float[] x = new float[n];
            x[n - 1] = dPrime[n - 1];

            for (int i = n - 2; i >= 0; i--)
            {
                x[i] = dPrime[i] - cPrime[i] * x[i + 1];
            }

            return x;
        }
    }
}