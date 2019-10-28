﻿using AlsaSharp;
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
                var midiAccess = new Commons.Music.Midi.Alsa.AlsaMidiAccess();
                Console.WriteLine("Using ALSA Midi Access.");
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
                    Console.Write("Select Input ID: ");
                    var inputId = Console.ReadLine();
                    matchedInput = midiAccess.Inputs.ToList().Find(x => x.Id.ToLower().Contains(inputId));
                } while (matchedInput == null);

                var openedInput = midiAccess.OpenInputAsync(matchedInput.Id).Result;

                openedInput.MessageReceived += Input_MessageReceived;

                Console.WriteLine("MIDI Input Opened. Press <ENTER> to close program.");
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