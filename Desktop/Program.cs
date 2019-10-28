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
                var midiAccess = new Commons.Music.Midi.RtMidi.RtMidiAccess();
                Console.WriteLine("Using RtMidi.");
                foreach (var input in midiAccess.Inputs)
                {
                    Console.WriteLine($"Input: {input.Id} - {input.Name}");
                }
                foreach (var output in midiAccess.Outputs)
                {
                    Console.WriteLine($"Output: {output.Id} - {output.Name}");
                }

                IMidiPortDetails matchedInput = null;
                do
                {
                    Console.Write("Select Input ID:");
                    var inputId = Console.ReadLine();
                    matchedInput = midiAccess.Inputs.ToList().Find(x => x.Id.ToLower().Contains(inputId));
                } while (matchedInput != null);

                midiAccess.OpenInputAsync(matchedInput.Id).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Caught exception: {ex.Message} \n\n {ex.InnerException} \n\n {ex.StackTrace}");
            }
        }
    }
}