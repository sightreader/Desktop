using AlsaSharp;
using Commons.Music.Midi;
using System;
using System.Linq;

namespace Desktop
{
    class Program
    {
        static void Main()
        {
            try
            {
                var engine = new DesktopEngine();
                engine.Server.Run(engine);

                Console.Write("Press <ENTER> to exit the program...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message} \n\n {ex.InnerException} \n\n {ex.StackTrace}");
            }
        }

        private static void Input_MessageReceived(object sender, MidiReceivedEventArgs e)
        {
            Console.WriteLine($"Input Message: {e.Data}");
        }
    }
}