using HSDRaw.Common;
using HSDRaw.MEX.Menus;
using MexTK.Tools;
using System.Threading;

namespace MexTK.Commands.MEX
{
    public class CmdCspCompressor : ICommand
    {
        public bool DoIt(string[] args)
        {
            if (args.Length < 3)
                return false;

            // load file
            var file = new HSDRaw.HSDRawFile(args[1]);
            var node = file["mexSelectChr"];
            if (node == null)
                return false;

            // parse percentage
            if (!float.TryParse(args[2], out percentage))
                return false;
            percentage /= 100;


            var mexNode = node.Data as MEX_mexSelectChr;

            if (mexNode != null)
            {
                var textures = mexNode.CSPMatAnim.TextureAnimation.ToTOBJs();

                for (int i = 0; i < textures.Length; i++)
                {
                    ThreadPool.QueueUserWorkItem(Process, textures[i]);
                }

                int toProcess = textures.Length;
                using (ManualResetEvent resetEvent = new ManualResetEvent(false))
                {
                    for (int i = 0; i < textures.Length; i++)
                        ThreadPool.QueueUserWorkItem(
                            x =>
                            {
                                var texture = (HSD_TOBJ)x;
                                using (var csp = texture.ToBitmap())
                                using (var shrink = ImageTools.ResizeImage(csp, (int)(csp.Width * percentage), (int)(csp.Height * percentage)))
                                {
                                    texture.EncodeImageData(shrink.GetBGRAData(), shrink.Width, shrink.Height, HSDRaw.GX.GXTexFmt.CI8, HSDRaw.GX.GXTlutFmt.RGB5A3);
                                }

                                if (Interlocked.Decrement(ref toProcess) == 0)
                                    resetEvent.Set();

                            }, textures[i]);

                    resetEvent.WaitOne();
                }

                mexNode.CSPMatAnim.TextureAnimation.FromTOBJs(textures, false);
            }

            file.TrimData();
            file.Save(args.Length > 3 ? args[3] : args[1]);

            return true;
        }

        private static float percentage = 1;

        static void Process(object callback)
        {
            var textures = (HSD_TOBJ)callback;

        }

        public string Help()
        {
            return @"MexTK.exe -cspcomp (MnSlcChr file) (percentage)";
        }

        public string ID()
        {
            return "-cspcomp";
        }

        public string Name()
        {
            return "MEX CSP Compressor";
        }
    }
}
