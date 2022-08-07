using System;
using System.Collections.Generic;
using MexTK.Commands;
using MexTK.Commands.Animation;
using MexTK.Commands.MEX;

namespace MexTK
{
    class Program
    {
        private static List<ICommand> Commands = new List<ICommand>()
        {
            new CmdFighterFunction(),
            new CmdAddSymbol(),
            new CmdTrimDAT(),
            new CmdCspCompressor(),
            new CmdPortFigatree(),
            new CmdRetargetAnimation(),
            new CmdOptimizeFigatree(),
            // new CmdMoveLogicTemplateGenerator(),
            // new CmdGenerateDatFile(),
            new CmdDebugSymbols(),
            // new CmdFighterAnimInject()
        };

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "-h")
                {
                    foreach (var cmd in Commands)
                    {
                        if (args[1].Equals(cmd.ID()))
                        {
                            Console.WriteLine(cmd.Help());
                            break;
                        }
                    }
                }
                else
                {
                    foreach (var cmd in Commands)
                    {
                        if (args[0].Equals(cmd.ID()))
                        {
                            if (!cmd.DoIt(args))
                            {
                                Console.WriteLine("Command Failed");
                                Console.WriteLine();
                                Console.WriteLine(cmd.Help());
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                PrintInstruction();
                Console.WriteLine();
                Console.WriteLine("exiting...");
                System.Threading.Thread.Sleep(1000);
            }

#if DEBUG
            Console.ReadLine();
#endif
        }
        

        /// <summary>
        /// 
        /// </summary>
        private static void PrintInstruction()
        {
            Console.WriteLine(@"MexTK");

            Console.WriteLine("use -h for more info");
            Console.WriteLine("Ex: MexTK -h -ff");

            Console.WriteLine();

            Console.WriteLine($"{"Commands:", -20}| {"Function Name:", -20}");
            foreach (var cmd in Commands)
            {
                Console.WriteLine($"{cmd.ID(), -20}| {cmd.Name()}");
            }
        }
    }
}
