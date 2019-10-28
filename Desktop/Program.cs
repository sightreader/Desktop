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

                var api = new AlsaMidiApi();
                foreach (var port in api.EnumerateAvailableInputPorts())
                    Console.Error.WriteLine("Input: " + port.Id + " : " + port.Name);
                foreach (var port in api.EnumerateAvailableOutputPorts())
                    Console.Error.WriteLine("Output: " + port.Id + " : " + port.Name);

                var input = api.CreateInputConnectedPort(api.EnumerateAvailableInputPorts().Last());

                Console.WriteLine("MIDI Input Opened. Press <ENTER> to close program.");
                Console.ReadLine();
                input.Dispose();
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