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

            var midiAccess = MidiAccessManager.Default;
            foreach (var input in options.MidiInputs) {
                var foundMidiInput = midiAccess.Inputs.Where(x => x.Name.ToLower().Contains(input.ToLower())).FirstOrDefault();

                if (foundMidiInput == null)
                {
                    Console.Error.WriteLine($"Did not find any MIDI input partially matching '{input}'.");
                    return 1;
                } else
                {
                    Console.WriteLine($"Using MIDI input '{foundMidiInput.Name}'.");
                }
                engine.MidiInputs.Add(midiAccess.OpenInputAsync(foundMidiInput.Id).Result);
            }

            foreach (var output in options.MidiOutputs)
            {
                var foundMidiOutput = midiAccess.Outputs.Where(x => x.Name.ToLower().Contains(output.ToLower())).FirstOrDefault();

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

                            /** The Yamaha P-45 sends Note Off messages as Note On 
                             * messages with zero velocity. */
                            var isNoteOnActuallyNoteOff = velocity == 0;

                            if (isNoteOnActuallyNoteOff)
                            {
                                engine.Interpreter.Input(new NoteRelease()
                                {
                                    Pitch = pitch
                                });
                            }
                            else
                            {
                                engine.Interpreter.Input(new NotePress()
                                {
                                    Pitch = pitch,
                                    Velocity = velocity
                                });
                            }
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
                            output.Send(new byte[]
                            {
                                MidiEvent.NoteOff,
                                release.Pitch,
                                64 /* Default release velocity */
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
                Console.Write("Jump to Measure Number (or load sheet music): ");
                var seekToMeasureNumberInput = Console.ReadLine();
                if (int.TryParse(seekToMeasureNumberInput, out var seekToMeasureNumber))
                {
                    engine.Interpreter.SeekMeasure(seekToMeasureNumber);
                } else
                {
                    scoreFilePath = seekToMeasureNumberInput.Replace('"', ' ').Trim();


                    if (!File.Exists(scoreFilePath))
                    {
                        Console.Error.WriteLine($"Could not find sheet music file '{scoreFilePath}'.");
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
                    }

                    if (scoreBuilder == null || score == null)
                    {
                        return 3;
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
                                64 /* Default release velocity */
                            }, 0, 3, 0);
                            break;
                        case MidiEvent.NoteOn:
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
            Console.ReadLine();
            Console.WriteLine("Digital piano pass-thru mode is now active. Press <ENTER> to quit the program at any time.");

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

            Console.ReadLine();
            Console.WriteLine();
            Console.WriteLine("Press <ENTER> to exit the program.");    
        }
    }
}