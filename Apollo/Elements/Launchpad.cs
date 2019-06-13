using System.Linq;
using System.Runtime.ExceptionServices;

using Avalonia.Threading;

using RtMidi.Core.Devices;
using RtMidi.Core.Devices.Infos;
using RtMidi.Core.Messages;

using Apollo.Core;
using Apollo.Structures;
using Apollo.Windows;

namespace Apollo.Elements {
    public class Launchpad {
        public LaunchpadWindow Window;
        public PatternWindow PatternWindow;

        public delegate void MultiResetHandler();
        public static event MultiResetHandler MultiReset;

        private IMidiInputDevice Input;
        private IMidiOutputDevice Output;

        public LaunchpadType Type { get; protected set; } = LaunchpadType.Unknown;

        private InputType _format = InputType.DrumRack;
        public InputType InputFormat {
            get => _format;
            set {
                if (_format != value) {
                    _format = value;
                    
                    Preferences.Save();
                }
            }
        }

        private RotationType _rotation = RotationType.D0;
        public RotationType Rotation {
            get => _rotation;
            set {
                if (_rotation != value) {
                    _rotation = value;
                    
                    Preferences.Save();
                }
            }
        }

        public string Name { get; protected set; }
        public bool Available { get; protected set; }

        public delegate void ReceiveEventHandler(Signal n);
        public event ReceiveEventHandler Receive;

        protected Pixel[] screen = new Pixel[100];

        protected void CreateScreen() {
            for (int i = 0; i < 100; i++)
                screen[i] = new Pixel() {MIDIExit = Send};
        }

        public enum LaunchpadType {
            MK2, PRO, CFW, Unknown
        }

        public enum InputType {
            XY, DrumRack
        }

        public enum RotationType {
            D0,
            D90,
            D180,
            D270
        }

        private readonly static SysExMessage Inquiry = new SysExMessage(new byte[] {0x7E, 0x7F, 0x06, 0x01});

        private LaunchpadType AttemptIdentify(SysExMessage response) {
            if (response.Data.Length != 15)
                return LaunchpadType.Unknown;

            if (response.Data[0] != 0x7E || response.Data[2] != 0x06 || response.Data[3] != 0x02)
                return LaunchpadType.Unknown;

            if (response.Data[4] == 0x00 && response.Data[5] == 0x20 && response.Data[6] == 0x29) { // Manufacturer = Novation
                switch (response.Data[7]) {
                    case 0x69: // Launchpad MK2
                        return LaunchpadType.MK2;
                    
                    case 0x51: // Launchpad Pro
                        return (response.Data[12] == 'c' && response.Data[13] == 'f' && response.Data[14] == 'w')? LaunchpadType.CFW : LaunchpadType.PRO;
                }
            }

            return LaunchpadType.Unknown;
        }

        private void WaitForIdentification(object sender, in SysExMessage e) {
            Type = AttemptIdentify(e);
            if (Type != LaunchpadType.Unknown) {
                Input.SysEx -= WaitForIdentification;

                Clear();

                Input.NoteOn += NoteOn;
                Input.NoteOff += NoteOff;
                Input.ControlChange += ControlChange;

                MIDI.DoneIdentifying();
            }
        }

        [HandleProcessCorruptedStateExceptions]
        private bool SysExSend(byte[] raw) {
            if (!Available || Type == LaunchpadType.Unknown) return false;

            SysExMessage msg = new SysExMessage(new byte[] {0x00, 0x20, 0x29, 0x02, (byte)((Type == LaunchpadType.MK2)? 0x18 : 0x10)}.Concat(raw).ToArray());
            try {
                Output.Send(in msg);
            } catch {};
            
            return true;
        }

        public virtual void Send(Signal n) {
            if (!Available || Type == LaunchpadType.Unknown) return;

            Window?.SignalRender(n.Clone());

            if (Rotation == RotationType.D90) {
                n.Index = (byte)((n.Index % 10) * 10 + 9 - n.Index / 10);

            } else if (Rotation == RotationType.D180) {
                n.Index = (byte)((9 - n.Index / 10) * 10 + 9 - n.Index % 10);

            } else if (Rotation == RotationType.D270) {
                n.Index = (byte)((9 - n.Index % 10) * 10 + n.Index / 10);
            }

            int offset = 0;
            if (Type == LaunchpadType.MK2 && 91 <= n.Index && n.Index <= 98) offset = 13;

            SysExSend(new byte[] {0x0B, (byte)(n.Index + offset), n.Color.Red, n.Color.Green, n.Color.Blue});
        }

        public virtual void Clear() {
            CreateScreen();

            SysExSend(new byte[] {0x0E, 0x00});
            Send(new Signal(this, 99, new Color(0)));
        }

        public virtual void Render(Signal n) => screen[n.Index]?.MIDIEnter(n);

        public Launchpad() => CreateScreen();

        public Launchpad(IMidiInputDeviceInfo input, IMidiOutputDeviceInfo output) {
            Input = input.CreateDevice();
            Output = output.CreateDevice();

            Name = input.Name;

            Input.Open();
            Output.Open();

            Program.Log($"MIDI Created {Name}");

            Available = true;

            Input.SysEx += WaitForIdentification;            
            Output.Send(in Inquiry);
        }

        public Launchpad(string name, InputType format = InputType.DrumRack, RotationType rotation = RotationType.D0) {
            CreateScreen();
            
            Name = name;
            _format = format;
            _rotation = rotation;

            Available = false;
        }

        public virtual void Connect(IMidiInputDeviceInfo input, IMidiOutputDeviceInfo output) {
            Input = input.CreateDevice();
            Output = output.CreateDevice();

            Input.Open();
            Output.Open();

            Program.Log($"MIDI Connected {Name}");

            Available = true;

            if (Type == LaunchpadType.Unknown) {
                Input.SysEx += WaitForIdentification;
                Output.Send(in Inquiry);
            } else {
                Clear();

                Input.NoteOn += NoteOn;
                Input.NoteOff += NoteOff;
                Input.ControlChange += ControlChange;
            }
        }

        public virtual void Disconnect() {
            if (Input.IsOpen) Input.Close();
            Input.Dispose();

            if (Output.IsOpen) Output.Close();
            Output.Dispose();

            Dispatcher.UIThread.InvokeAsync(() => Window?.Close());

            Program.Log($"MIDI Disconnected {Name}");

            Available = false;
        }

        public void HandleMessage(Signal n) {
            if (Available) {
                if (Rotation == RotationType.D90) {
                    n.Index = (byte)((9 - n.Index % 10) * 10 + n.Index / 10);

                } else if (Rotation == RotationType.D180) {
                    n.Index = (byte)((9 - n.Index / 10) * 10 + 9 - n.Index % 10);

                } else if (Rotation == RotationType.D270) {
                    n.Index = (byte)((n.Index % 10) * 10 + 9 - n.Index / 10);
                }

                if (PatternWindow == null) Receive?.Invoke(n);
                else PatternWindow.MIDIEnter(n);
            }
        }

        public void NoteOn(object sender, in NoteOnMessage e) => HandleMessage(new Signal(
            InputFormat,
            this,
            (byte)e.Key,
            new Color((byte)(e.Velocity >> 1))
        ));

        private void NoteOff(object sender, in NoteOffMessage e) => HandleMessage(new Signal(
            InputFormat,
            this,
            (byte)e.Key,
            new Color(0)
        ));

        private void ControlChange(object sender, in ControlChangeMessage e) {
            switch (Type) {
                case LaunchpadType.MK2:
                    if (104 <= e.Control && e.Control <= 111)
                        HandleMessage(new Signal(
                            InputType.XY,
                            this,
                            (byte)(e.Control - 13),
                            new Color((byte)(e.Value >> 1))
                        ));
                    break;

                case LaunchpadType.PRO:
                case LaunchpadType.CFW:
                    if (e.Control == 121) {
                        MultiReset?.Invoke();
                        return;
                    }

                    HandleMessage(new Signal(
                        InputType.XY,
                        this,
                        (byte)e.Control,
                        new Color((byte)(e.Value >> 1))
                    ));
                    break;
            }
        }

        public override string ToString() => (Available? "" : "(unavailable) ") + Name;
    }
}