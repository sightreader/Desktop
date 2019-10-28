using Commons.Music.Midi;
using System;

namespace Desktop
{
    class Program
    {
        static void Main()
        {
            var midiAccess = new Commons.Music.Midi.Alsa.AlsaMidiAccess();
            foreach (var input in midiAccess.Inputs)
            {
                Console.WriteLine($"Input: {input.Name}");
            }
            foreach (var output in midiAccess.Outputs)
            {
                Console.WriteLine($"Output: {output.Name}");
            }
            foreach (var input in midiAccess.Inputs)
            {
                if (input.Name.ToLower().Contains("pixel"))
                {
                    Console.WriteLine($"Attempting to open input: {input.Name}");
                    midiAccess.OpenInputAsync(input.Id);
                }
            }


            var engine = new DesktopEngine();
            engine.Server.Run(engine);

            Console.Write("Press <ENTER> to exit the program...");
            Console.ReadLine();
        }
    }
}