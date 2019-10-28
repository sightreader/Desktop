using System;
using Commons.Music.Midi;
using System.Linq;
using SightReader.Engine.ScoreBuilder;
using SightReader.Engine.Interpreter;
using System.Collections.Generic;
using System.IO;
using SightReader.Engine.Server;

namespace Desktop
{
    class Program
    {
        static void Main(string[] args)
        {
            var engine = new DesktopEngine();
            engine.Server.Run(engine);

            Console.Write("Press <ENTER> to exit the program...");
            Console.ReadLine();
        }
    }
}