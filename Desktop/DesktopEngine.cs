using Commons.Music.Midi;
using SightReader.Engine;
using SightReader.Engine.Interpreter;
using SightReader.Engine.Introducer;
using SightReader.Engine.Server;
using System;
using System.Collections.Generic;
using System.Text;

namespace Desktop
{
    public class DesktopEngine : IEngineContext
    {
        public List<IMidiInput> MidiInputs { get; set; } = new List<IMidiInput>();
        public List<IMidiOutput> MidiOutputs { get; set; } = new List<IMidiOutput>();
        public Interpreter Interpreter { get; set; } = new Interpreter();
        public IntroducerClient Introducer { get; set; } /* Not implemented */
        public CommandServer Server { get; set; } = new CommandServer();
    }
}
