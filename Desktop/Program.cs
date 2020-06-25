using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commons.Music.Midi;
using SightReader.Engine.Interpreter;
using SightReader.Engine.ScoreBuilder;
using CommandLine;
using System.Text;

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

        [Option('f', "file", Required = false, HelpText = "Path to MusicXML sheet music for direct mode.")]
        public string FilePath { get; set; }
    }

    [Verb("passthru", HelpText = "Runs like a regular digital piano. All inputs are passed through without interpretation.")]
    class PassThruOptions
    {
        [Option('i', "inputs", Required = true, HelpText = "The MIDI inputs to use. Each partial match which be added as an input.")]
        public IEnumerable<string> MidiInputs { get; set; }

        [Option('o', "outputs", Required = true, HelpText = "The MIDI output to use. Each partial match will be added as an output.")]
        public IEnumerable<string> MidiOutputs { get; set; }
    }

    class Program
    {
        static int counter1 = 1;

        static int RunDebug(DebugOptions options)
        {
            var engine = new DesktopEngine();

            var midiAccess = MidiAccessManager.Default;

            Console.WriteLine("MIDI Inputs");
            Console.WriteLine("-----------");
            Console.WriteLine("");
            Console.WriteLine($"\tId\tName");
            Console.WriteLine($"-----------------------------------");
            foreach (var input in midiAccess.Inputs)
            {
                Console.WriteLine($"\t{input.Id}\t{input.Name}");
            }

            Console.WriteLine();

            Console.WriteLine("MIDI Outputs");
            Console.WriteLine("------------");
            Console.WriteLine("");
            Console.WriteLine($"\tId\tName");
            Console.WriteLine($"-----------------------------------");
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

            var midiAccess = MidiAccessManager.Default;
            foreach (var input in options.MidiInputs)
            {
                var foundMidiInput = midiAccess.Inputs.Where(x => x.Name.ToLower().Contains(input.ToLower()) || x.Id.ToLower().Contains(input.ToLower())).FirstOrDefault();

                if (foundMidiInput == null)
                {
                    Console.Error.WriteLine($"Did not find any MIDI input partially matching '{input}'.");
                    return 1;
                }
                else
                {
                    Console.WriteLine($"Using MIDI input '{foundMidiInput.Name}'.");
                }
                engine.MidiInputs.Add(midiAccess.OpenInputAsync(foundMidiInput.Id).Result);
            }

            foreach (var output in options.MidiOutputs)
            {
                var foundMidiOutput = midiAccess.Outputs.Where(x => x.Name.ToLower().Contains(output.ToLower()) || x.Id.ToLower().Contains(output.ToLower())).FirstOrDefault();

                if (foundMidiOutput == null)
                {
                    Console.Error.WriteLine($"Did not find any MIDI output partially matching '{output}'.");
                    return 1;
                }
                else
                {
                    Console.WriteLine($"Using MIDI output'{foundMidiOutput.Name}'.");
                }
                engine.MidiOutputs.Add(midiAccess.OpenOutputAsync(foundMidiOutput.Id).Result);
            }

            var scoreFilePath = options.FilePath;

            FileStream fileStream = null;

            ScoreBuilder scoreBuilder = null;
            Score score = null;
            Dictionary<byte, byte> noteVelocityMap = new Dictionary<byte, byte>();
            foreach (var index in Enumerable.Range(0, 128))
            {
                noteVelocityMap.Add((byte)index, 0);
            }
            var lastNoteOnVelocities = new Queue<decimal>(3);

            if (scoreFilePath != null)
            {

                if (!File.Exists(options.FilePath))
                {
                    Console.Error.WriteLine($"Could not find sheet music file '{options.FilePath}'.");
                    return 1;
                }

                try
                {
                    fileStream = new FileStream(scoreFilePath, FileMode.Open);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Could not open sheet music file path {scoreFilePath}: {ex}");
                }

                if (fileStream == null)
                {
                    return 2;
                }

                try
                {
                    scoreBuilder = new ScoreBuilder(fileStream);
                    score = scoreBuilder.Build();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Could not build sheet music score from file path {scoreFilePath}: {ex}");
                }

                if (scoreBuilder == null || score == null)
                {
                    return 3;
                }
                else
                {
                    Console.WriteLine($"Successfully loaded sheet music at {scoreFilePath}.");
                }

                engine.Interpreter.SetScore(score, scoreFilePath);
            }

            engine.MidiInputs[0].MessageReceived += (object sender, MidiReceivedEventArgs e) =>
            {
                switch (e.Data[0])
                {
                    case MidiEvent.NoteOff:
                        {
                            var pitch = e.Data[1];
                            Console.WriteLine($"Off (actual): {pitch}");
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

                            var isSimulatedNoteOff = velocity == 0;
                            var isRequestedPitchAlreadyAtZeroVelocity = noteVelocityMap[pitch] == 0;
                            var isRequestedNoteOffAlreadyNoteOff = isRequestedPitchAlreadyAtZeroVelocity;
                            var shouldNoteOffBeNoteOn = isSimulatedNoteOff && isRequestedNoteOffAlreadyNoteOff;

                            if (shouldNoteOffBeNoteOn)
                            {
                                var averagePreviousNoteVelocities = (byte)(lastNoteOnVelocities.Average());
                                Console.WriteLine($"<!!! CAUGHT !!!> On (simulated) {counter1++}: {pitch} at {String.Format("{0:0%}", averagePreviousNoteVelocities / 127.0)}");
                                noteVelocityMap[pitch] = averagePreviousNoteVelocities;
                                engine.Interpreter.Input(new NotePress()
                                {
                                    Pitch = pitch,
                                    Velocity = averagePreviousNoteVelocities
                                });
                            }
                            else
                            {
                                /** The Yamaha P-45 sends Note Off messages as Note On 
                                 * messages with zero velocity. */
                                var isNoteOnActuallyNoteOff = velocity == 0;

                                if (isNoteOnActuallyNoteOff)
                                {
                                    Console.WriteLine($"Off (simulated) {counter1++}: {pitch} at {String.Format("{0:0%}", velocity / 127.0)} <---> (Last) {String.Format("{0:0%}", (byte)noteVelocityMap[pitch] / 127.0)}");
                                    noteVelocityMap[pitch] = 0;
                                    engine.Interpreter.Input(new NoteRelease()
                                    {
                                        Pitch = pitch
                                    });
                                }
                                else
                                {
                                    lastNoteOnVelocities.Enqueue(velocity);
                                    if (lastNoteOnVelocities.Count > 3)
                                    {
                                        lastNoteOnVelocities.Dequeue();
                                    }

                                    noteVelocityMap[pitch] = velocity;
                                    Console.WriteLine($"On {counter1++}: {pitch} at {String.Format("{0:0%}", velocity / 127.0)}");
                                    engine.Interpreter.Input(new NotePress()
                                    {
                                        Pitch = pitch,
                                        Velocity = velocity
                                    });
                                }
                            }
                        }
                        break;
                    case MidiEvent.CC:
                        {
                            var pedalKind = e.Data[1];
                            var position = e.Data[2];

                            // Console.WriteLine($"Pedal {counter1++}: {String.Format("{0:0%}", position / 127.0)}");
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
                                (byte)(127 - pedal.Position)
                            }, 0, 3, 0);
                            break;
                        case NoteRelease release:
                            output.Send(new byte[]
                            {
                                MidiEvent.NoteOff,
                                release.Pitch,
                                0 /* Default release velocity */
                            }, 0, 3, 0);
                            break;
                        case NotePress press:
                            var measureNumbers = engine.Interpreter.GetMeasureNumbers();
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
                Console.Write("Measure Number or Sheet Music:");
                var seekToMeasureNumberInput = Console.ReadLine();
                if (int.TryParse(seekToMeasureNumberInput, out var seekToMeasureNumber))
                {
                    engine.Interpreter.SeekMeasure(seekToMeasureNumber);
                }
                else
                {
                    scoreFilePath = seekToMeasureNumberInput.Replace('"', ' ').Replace('\'', ' ').Trim();


                    if (!File.Exists(scoreFilePath))
                    {
                        Console.Error.WriteLine($"Could not find sheet music file '{scoreFilePath}'.");
                        continue;
                    }

                    try
                    {
                        fileStream = new FileStream(scoreFilePath, FileMode.Open);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Could not open sheet music file path {scoreFilePath}: {ex}");
                    }

                    if (fileStream == null)
                    {
                        continue;
                    }

                    scoreBuilder = null;
                    score = null;

                    try
                    {
                        scoreBuilder = new ScoreBuilder(fileStream);
                        score = scoreBuilder.Build();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Could not build sheet music score from file path {scoreFilePath}: {ex}");
                        continue;
                    }

                    if (scoreBuilder == null || score == null)
                    {
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"Successfully loaded sheet music at {scoreFilePath}.");

                        engine.Interpreter.SetScore(score, scoreFilePath);

                    }
                }
            }
        }

        static int RunPassThru(PassThruOptions options)
        {
            var engine = new DesktopEngine();

            var midiAccess = MidiAccessManager.Default;
            foreach (var input in options.MidiInputs)
            {
                var foundMidiInput = midiAccess.Inputs.Where(x => x.Name.ToLower().Contains(input.ToLower()) || x.Id.ToLower().Contains(input.ToLower())).FirstOrDefault();

                if (foundMidiInput == null)
                {
                    Console.Error.WriteLine($"Did not find any MIDI input partially matching '{input}'.");
                }
                engine.MidiInputs.Add(midiAccess.OpenInputAsync(foundMidiInput.Id).Result);
            }

            foreach (var output in options.MidiOutputs)
            {
                var foundMidiOutput = midiAccess.Outputs.Where(x => x.Name.ToLower().Contains(output.ToLower()) || x.Id.ToLower().Contains(output.ToLower())).FirstOrDefault();

                if (foundMidiOutput == null)
                {
                    Console.Error.WriteLine($"Did not find any MIDI output partially matching '{output}'.");
                }
                engine.MidiOutputs.Add(midiAccess.OpenOutputAsync(foundMidiOutput.Id).Result);
            }

            engine.MidiInputs[0].MessageReceived += (object sender, MidiReceivedEventArgs e) =>
            {
                foreach (var output in engine.MidiOutputs)
                {
                    switch (e.Data[0])
                    {
                        case MidiEvent.CC:
                            output.Send(new byte[]
                            {
                                MidiEvent.CC,
                                e.Data[1],
                                e.Data[2]
                            }, 0, 3, 0);
                            break;
                        case MidiEvent.NoteOff:
                            output.Send(new byte[]
                            {
                                MidiEvent.NoteOff,
                                e.Data[1],
                                0 /* Default release velocity */
                            }, 0, 3, 0);
                            break;
                        case MidiEvent.NoteOn:
                            var byteData = new byte[]
                            {
                                MidiEvent.NoteOn,
                                e.Data[1],
                                e.Data[2]
                            };
                            Console.WriteLine($"Raw Note: {String.Join(' ', byteData.Select(b => b.ToString()))}");
                            output.Send(new byte[]
                            {
                                MidiEvent.NoteOn,
                                e.Data[1],
                                e.Data[2]
                            }, 0, 3, 0);
                            break;
                    }
                }
            };

            Console.WriteLine();
            Console.WriteLine("Digital piano pass-thru mode is now active. Press <ENTER> to quit the program at any time.");
            Console.ReadLine();

            return 0;
        }

        static int OnConsoleArgsParseError(IEnumerable<Error> errors)
        {
            return 1;
        }

        static void Main(string[] args)
        {
            Console.Title = "SightReader Piano Assistant";

            Parser.Default.ParseArguments<DebugOptions, PlayOptions, PlayDirectOptions, PassThruOptions>(args)
            .MapResult(
                (DebugOptions o) => RunDebug(o),
                (PlayOptions o) => RunPlay(o),
                (PlayDirectOptions o) => RunPlayDirect(o),
                (PassThruOptions o) => RunPassThru(o),
                errors => OnConsoleArgsParseError(errors));

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("Press <ENTER> to exit the program.");
        }
    }
}