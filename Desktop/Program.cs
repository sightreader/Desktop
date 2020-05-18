using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commons.Music.Midi;
using SightReader.Engine.Interpreter;
using SightReader.Engine.ScoreBuilder;
using CommandLine;

namespace Desktop
{
    [Verb("debug", HelpText = "Print developer debug information.")]
    class DebugOptions
    {
    }

    [Verb("play", HelpText = "Runs in the default networked mode.")]
    class PlayOptions
    {
    }

    [Verb("play-direct", HelpText = "Runs sightreader assistance on the specified sheet music with the specified MIDI inputs and outputs.")]
    class PlayDirectOptions
    {
        [Option('i', "inputs", Required = true, HelpText = "The MIDI inputs to use. Each partial match which be added as an input.")]
        public IEnumerable<string> MidiInputs { get; set; }

        [Option('o', "outputs", Required = true, HelpText = "The MIDI output to use. Each partial match will be added as an output.")]
        public IEnumerable<string> MidiOutputs { get; set; }

        [Option('f', "file", Required = true, HelpText = "Path to MusicXML sheet music for direct mode.")]
        public string FilePath { get; set; }
    }

    class Program
    {
        static int RunDebug(DebugOptions options)
        {
            var engine = new DesktopEngine();

            var midiAccess = MidiAccessManager.Default;
            
            Console.WriteLine("MIDI Inputs");
            Console.WriteLine("-----------");
            foreach (var input in midiAccess.Inputs)
            {
                Console.WriteLine($"\t{input.Id}\t{input.Name}");
            }

            Console.WriteLine();

            Console.WriteLine("MIDI Outputs");
            Console.WriteLine("------------");
            foreach (var output in midiAccess.Outputs)
            {
                Console.WriteLine($"\t{output.Id}\t{output.Name}");
            }

            return 0;
        }

        static int RunPlay(PlayOptions options)
        {
            var engine = new DesktopEngine();
            engine.Server.Run(engine);

            return 0;
        }

        static int RunPlayDirect(PlayDirectOptions options)
        {
            var engine = new DesktopEngine();

            if (!File.Exists(options.FilePath))
            {
                Console.Error.WriteLine($"Could not find sheet music file '{options.FilePath}'.");
                return 1;
            }

            var midiAccess = MidiAccessManager.Default;
            foreach (var input in options.MidiInputs) {
                var foundMidiInput = midiAccess.Inputs.Where(x => x.Name.ToLower().Contains(input)).FirstOrDefault();

                if (foundMidiInput == null)
                {
                    Console.Error.WriteLine($"Did not find any MIDI input partially matching '{input}'.");
                }
                engine.MidiInputs.Add(midiAccess.OpenInputAsync(foundMidiInput.Id).Result);
            }

            foreach (var output in options.MidiOutputs)
            {
                var foundMidiOutput = midiAccess.Outputs.Where(x => x.Name.ToLower().Contains(output)).FirstOrDefault();

                if (foundMidiOutput == null)
                {
                    Console.Error.WriteLine($"Did not find any MIDI output partially matching '{output}'.");
                }
                engine.MidiOutputs.Add(midiAccess.OpenOutputAsync(foundMidiOutput.Id).Result);
            }

            var scoreFilePath = options.FilePath;

            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(scoreFilePath, FileMode.Open);
            } catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not open sheet music file path {scoreFilePath}: {ex.ToString()}");
            }

            if (fileStream == null)
            {
                return 2;
            }

            ScoreBuilder scoreBuilder = null;
            Score score = null;

            try
            {
                scoreBuilder = new ScoreBuilder(fileStream);
                score = scoreBuilder.Build();
            } catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not build sheet music score from file path {scoreFilePath}: {ex.ToString()}");
            }

            if (scoreBuilder == null || score == null)
            {
                return 3;
            }

            engine.Interpreter.SetScore(score, scoreFilePath);

            engine.MidiInputs[0].MessageReceived += (object sender, MidiReceivedEventArgs e) =>
            {
                switch (e.Data[0])
                {
                    case MidiEvent.NoteOff:
                        {
                            var pitch = e.Data[1];
                            engine.Interpreter.Input(new NoteRelease()
                            {
                                Pitch = pitch
                            });
                        }
                        break;
                    case MidiEvent.NoteOn:
                        {
                            var pitch = e.Data[1];
                            var velocity = e.Data[2];
                            engine.Interpreter.Input(new NotePress()
                            {
                                Pitch = pitch,
                                Velocity = velocity
                            });
                        }
                        break;
                    case MidiEvent.CC:
                        {
                            var pedalKind = e.Data[1];
                            var position = e.Data[2];
                            engine.Interpreter.Input(new PedalChange()
                            {
                                Pedal = PedalKind.Sustain,
                                Position = position
                            });
                        }
                        break;
                }
            };

            engine.Interpreter.Output += (IPianoEvent e) =>
            {
                foreach (var output in engine.MidiOutputs)
                {
                    switch (e)
                    {
                        case PedalChange pedal:
                            Console.WriteLine($"Pedal: {pedal.Pedal}");
                            output.Send(new byte[]
                            {
                                MidiEvent.CC,
                                pedal.Pedal switch
                                {
                                   PedalKind.UnaCorda => 67,
                                   PedalKind.Sostenuto => 66,
                                   PedalKind.Sustain => 64,
                                   _ => 64
                                },
                                pedal.Position
                            }, 0, 3, 0);
                            break;
                        case NoteRelease release:
                            Console.WriteLine($"Release: {release.Pitch}");
                            output.Send(new byte[]
                            {
                                MidiEvent.NoteOff,
                                release.Pitch,
                                64 /* Default release velocity */
                            }, 0, 3, 0);
                            break;
                        case NotePress press:
                            var measureNumbers = engine.Interpreter.GetMeasureNumbers();
                            Console.WriteLine($"Press: {press.Pitch} {measureNumbers[0]}, {measureNumbers[1]}");
                            output.Send(new byte[]
                            {
                                MidiEvent.NoteOn,
                                press.Pitch,
                                press.Velocity
                            }, 0, 3, 0);
                            break;
                    }
                }
            };

            Console.WriteLine();

            while (true)
            {
                Console.Write("Jump to Measure Number: ");
                var seekToMeasureNumberInput = Console.ReadLine();
                int.TryParse(seekToMeasureNumberInput, out var seekToMeasureNumber);
                engine.Interpreter.SeekMeasure(seekToMeasureNumber);
            }

            return 0;
        }

        static int OnConsoleArgsParseError(IEnumerable<Error> errors)
        {
            return 1;
        }

        static void Main(string[] args)
        {
            Console.Title = "SightReader Piano Assistant";

            Parser.Default.ParseArguments<DebugOptions, PlayOptions, PlayDirectOptions>(args)
            .MapResult(
                (DebugOptions o) => RunDebug(o),
                (PlayOptions o) => RunPlay(o),
                (PlayDirectOptions o) => RunPlayDirect(o),
                errors => OnConsoleArgsParseError(errors));
        }
    }
}