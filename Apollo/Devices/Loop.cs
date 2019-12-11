using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Structures;

namespace Apollo.Devices {
    public class Loop: Device {
        
        Time _duration;
        public Time Duration {
            get => _duration;
            set {
                if (_duration != null) {
                    _duration.FreeChanged -= FreeChanged;
                    _duration.ModeChanged -= ModeChanged;
                    _duration.StepChanged -= StepChanged;
                }

                _duration = value;

                if (_duration != null) {
                    _duration.Minimum = 10;
                    _duration.Maximum = 30000;

                    _duration.FreeChanged += FreeChanged;
                    _duration.ModeChanged += ModeChanged;
                    _duration.StepChanged += StepChanged;
                }
            }
        }
        
        int _amount = 0;
        public int Amount {
            get => _amount;
            set {
                if (0 <= value && value <= 100 && _amount != value) {
                    _amount = value;
                    
                    if (Viewer?.SpecificViewer != null) ((LoopViewer)Viewer.SpecificViewer).SetAmount(Amount);
                }
            }
        }
        
        double _gate;
        public double Gate {
            get => _gate;
            set {
                if (0.01 <= value && value <= 4) {
                    _gate = value;
                    
                    if (Viewer?.SpecificViewer != null) ((LoopViewer)Viewer.SpecificViewer).SetGate(Gate);
                }
            }
        }
        
        ConcurrentDictionary<Signal, List<Courier>> timers = new ConcurrentDictionary<Signal, List<Courier>>();

        void FreeChanged(int value) {
            if (Viewer?.SpecificViewer != null) ((LoopViewer)Viewer.SpecificViewer).SetDurationValue(value);
        }

        void ModeChanged(bool value) {
            if (Viewer?.SpecificViewer != null) ((LoopViewer)Viewer.SpecificViewer).SetMode(value);
        }

        void StepChanged(Length value) {
            if (Viewer?.SpecificViewer != null) ((LoopViewer)Viewer.SpecificViewer).SetDurationStep(value);
        }
        
        public Loop(Time duration = null, double gate = 0, int amount = 0) : base("loop"){
            Duration = duration?? new Time();
            Amount = amount;
            Gate = gate;
        }
        
        public override Device Clone() => new Loop(Duration, Amount);
        
        void Stop(Signal n) {
            if (timers.ContainsKey(n))
                for (int i = 0; i < timers[n].Count; i++)
                    timers[n][i].Dispose();
            
            timers[n] = new List<Courier>();
        }
        
        public override void MIDIProcess(Signal n){
            Stop(n);
            
            for(int i = 1; i <= Amount; i++){
                Courier courier;
                timers[n].Add(courier = new Courier(){
                    AutoReset = false,
                    Info = n,
                    Interval = i * _duration * _gate
                });
                courier.Elapsed += Tick;
                courier.Start();
            }
            
            Signal m = n.Clone();
            InvokeExit(m);
        }
        
        void Tick(object sender, EventArgs e) {
            if(Disposed) return;
            
            Courier courier = (Courier)sender;
            courier.Elapsed -= Tick;
            
            if(courier.Info is Signal n){
                timers[n].Remove(courier);
                courier.Dispose();
            
                Signal m = n.Clone();
                InvokeExit(m);
            }   
        }
        
        protected override void Stop() {
            foreach (List<Courier> i in timers.Values) {
                foreach (Courier j in i) j.Dispose();
                i.Clear();
            }
            timers.Clear();
        }
        
        public override void Dispose() {
            if (Disposed) return;
            
            Stop();

            base.Dispose();
        }
    }
}