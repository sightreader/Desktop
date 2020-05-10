using System;

namespace Desktop
{
    class Program
    {
        static void Main()
        {
            Console.Title = "SightReader Piano Assistant";

            var engine = new DesktopEngine();
            engine.Server.Run(engine);

            Console.Write("Press <ENTER> to exit the program...");
            Console.ReadLine();
        }
    }
}