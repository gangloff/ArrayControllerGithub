using System;
using System.Collections.Generic;

using System.Text;
using System.Runtime.InteropServices;
using NationalInstruments.DAQmx;

namespace ArrayDACControl
{
    public class DACController
    {
        [DllImport("inpout32.dll", EntryPoint = "Out32")]
        private static extern void Output(int adress, int value);    // declare Out32 method from the library under name "Output"
        [DllImport("inpout32.dll", EntryPoint = "Inp32")]
        private static extern void Input(int adress);       // declare Out32 method from the library under name "Output"
        private const int parallelPortAddress = 0x378;
        private int DACinstructionTicks;

        const byte clockMask = 0x40;
        const byte dataMask = 0x10;
        const byte CSBARMask = 0x01;

        const byte LDACBARMask = 0x4;
        const byte CLRBARMask = 0x2;

        const byte DACmask = clockMask | dataMask | CSBARMask | LDACBARMask | CLRBARMask;

        private byte DACbitsPrivate = CLRBARMask | LDACBARMask;

        private byte currentMask;

        private Task doTask;
        private const double DACControlSampleRate = 350000;
        private List<byte> doBuffer;

        public DACController()
        {
            Mask = 0;
            doBuffer = new List<byte>();
            
            Clear();
            outputElectrodeVoltage(0, 1);                        
            DACinstructionTicks = doBuffer.Count;
            Clear();          

            initDigitalOutput();
            Reset();
        }

        public void Clear()
        {
            doBuffer.Clear();
        }

        private byte DACbits
        {
            get { return DACbitsPrivate; }
            set { DACbitsPrivate = value; }
        }

        public byte Mask
        {
            get { return currentMask; }
            set { currentMask = (byte)(DACbits | (~DACmask & value)); }
        }

        private void doOutput(int b)
        {
            doBuffer.Add((byte)(b));
        }

        private void outputBit(bool bit)
        {
            if (!bit)
            {
                doOutput((~dataMask) & (clockMask | Mask));
                doOutput((~dataMask) & (~clockMask & Mask));
            }
            else
            {
                doOutput(dataMask | (clockMask | Mask));
                doOutput(dataMask | (~clockMask & Mask));
            }
        }

        private void outputBitsFromUInt(uint bits, int nBits)
        {
            uint mask = (uint)(1 << (nBits - 1));

            for (; mask != 0; )
            {
                outputBit((bits & mask) != 0);
                mask = (mask >> 1);
            }
        }

        public void outputStdOffsetWord()
        {

            doOutput((~CSBARMask) & Mask);    // set CSBAR low
            outputBit(false); outputBit(true); outputBit(true);         // set control bits for write-through            
            outputBitsFromUInt(32, 6);                                // output requested DAC address
            outputBitsFromUInt(0, 7);                                   // don't care bits
            outputBitsFromUInt(1 << 14, 16);                                // output value
            doOutput((CSBARMask) | Mask);     // set CSBAR high

        }

        // outputs the val word to channel nDAC of the DAC
        public void outputInt(uint nDAC, uint val)
        {
            if (nDAC > 31 || nDAC < 0)
                throw new IndexOutOfRangeException();

            doOutput((~CSBARMask) & Mask);    // set CSBAR low
            outputBit(false); outputBit(true); outputBit(true);         // set control bits for write-through            
            outputBitsFromUInt(nDAC, 6);                                // output requested DAC address
            outputBitsFromUInt(0, 7);                                   // don't care bits
            outputBitsFromUInt(val, 16);                                // output value
            doOutput((CSBARMask) | Mask);     // set CSBAR high
        }

        public void outputNOP()
        {
            doOutput((~CSBARMask) & Mask);    // set CSBAR low
            for (int i = 0; i < 32; i++)
                outputBit(false);
            doOutput((CSBARMask) | Mask);     // set CSBAR high 
        }

        public void outBlanks(int n)
        {
            for (int i = 0; i < n; i++)
                doBuffer.Add((byte)(Mask));
        }

        public void Wait(ref double T)
        {
            int fillerTicks = (int)(T * DACControlSampleRate);
            T = fillerTicks / DACControlSampleRate;
            outBlanks(fillerTicks);
        }

        public void Reset()
        {
            Clear();
            doOutput(~CLRBARMask);
            Commit();
            System.Threading.Thread.Sleep(10);
            Clear();
            doOutput(Mask);
            outputNOP();
            outputStdOffsetWord();
            outputStdOffsetWord();
            for (uint i = 0; i < 31; i++)
                outputElectrodeVoltage(i, 0);
            Commit();
        }


        private const double ampgain = 9.6;
        private const double vref = 3.0;
        private const double gain = 3.33333;
        private const double datamax = 0xFFFF;
        private const double offsetcode = 0x4000;
        private const double voltslim = 30;

        public void outputOneOffsetElectrodeVoltage(uint DAC, double val)
        {
            outputElectrodeVoltage(DAC - 1, val);
        }
        public void outputOneOffsetElectrodeVoltageNow(uint DAC, double val)
        {
            Clear();
            outputElectrodeVoltage(DAC - 1, val);
            Commit();
        }

        // outputs a real electrode voltage (as measured after DAC box)
        public void outputElectrodeVoltage(uint DAC, double val)
        {
            double offset = 0;

            if (DAC < 0 || DAC > 31)
                throw new IndexOutOfRangeException();

            if (val > 24 || val < -24)
               throw new IndexOutOfRangeException();

            //Software hardcoded channel offsets to compensate output amplifier offsets in the DAC box
            switch (DAC)
            {
                case 0: offset = 0.008; break;
                case 1: offset = 0.060; break;
                case 2: offset = 0.009; break;
                case 3: offset = 0.076; break;
                case 4: offset = 0.023; break;
                case 5: offset = 0; break;
                case 6: offset = 0.037; break;
                case 7: offset = 0.058; break;
                case 8: offset = 0.04; break;
                case 9: offset = 0.04; break;
                case 10: offset = 0.06; break;
                case 11: offset = 0.03; break;
                case 12: offset = 0.03; break;
                case 13: offset = 0.055; break;
                case 14: offset = 0.058; break;
                case 15: offset = 0.04; break;
                case 16: offset = 0.035; break;
                case 17: offset = 0.025; break;
                case 18: offset = 0.045; break;
                case 19: offset = 0.053; break;
                case 20: offset = 0.025; break;
                case 21: offset = 0.035; break;
                case 22: offset = -0.005; break;
                case 23: offset = 0.023; break;
                case 24: offset = 0; break; //unused channel, offset was not calibrated
                case 25: offset = 0; break; //unused channel, offset was not calibrated
                case 26: offset = 0; break; //unused channel, offset was not calibrated
                case 27: offset = 0.062-0.062 +0.013; break;
                case 28: offset = 0; break;
                case 29: offset = 0.113; break;
                case 30: offset = 0.033; break;
                case 31: offset = 0.013-0.013 +0.062; break;
            }
            val = val - offset;

            double codeout = (datamax * val / ampgain) / (gain * vref) + offsetcode + 1;
            uint codeoutUint = System.Convert.ToUInt32(codeout);

            outputInt(DAC, codeoutUint);
        }

        private void initDigitalOutput()
        {
            doTask = new Task();
            //initialize doTask to use line 0 
            doTask.DOChannels.CreateChannel("Dev2/port0:1", "", ChannelLineGrouping.OneChannelForAllLines);
        }
        
        /// <summary>
        /// adds to the output buffer the bits necessary to 
        /// output a ramp on the given electrodes between the given values
        /// </summary>
        /// <param name="electrodes">list of electrode numbers to output to</param>
        /// <param name="V0">list of voltages at the beginning of the ramp</param>
        /// <param name="V1">list of voltages at the end of the ramp</param>
        /// <param name="T">actual duration of the ramp written</param>
        /// <param name="samples">actual number of samples written</param>
        public void Ramp(int[] electrodes, double[] V0, double[] V1, ref double T, ref int samples)
        {
            int nrElectrodes = electrodes.GetLength(0);
            if (
                nrElectrodes != V0.GetLength(0) ||
                nrElectrodes != V1.GetLength(0))
                throw new Exception("Number of electrodes does not match number of specified start/stop voltages");

            // figure #clock ticks / point            
            int ticksPerPoint = nrElectrodes * this.DACinstructionTicks;            

            double timePerPoint = ticksPerPoint / DACControlSampleRate;
            if (T / samples < timePerPoint)
                throw new Exception("Given ramp time resolution too fast for chosen DAC serial clk rate");

            int fillerTicks = (int)((T / samples) * DACControlSampleRate) - ticksPerPoint;
            ticksPerPoint += fillerTicks;

            samples = (int)((T * DACControlSampleRate) / ticksPerPoint);

            for (int j = 0; j < samples; j++)
            {
                for (int i = 0; i < nrElectrodes; i++)
                    outputOneOffsetElectrodeVoltage((uint)electrodes[i], V0[i] + (V1[i] - V0[i]) * (j) / (samples - 1));
                outBlanks(fillerTicks);
            }
        }
        /// <summary>
        /// adds to the output buffer the bits necessary to 
        /// output the given array of analog values to the given electrodes
        /// </summary>
        /// <param name="electrodes">list of electrode numbers to output to</param>
        /// <param name="Vs">matrix of values to output</param>
        /// <param name="nSampleRate">sample rate in samples/s</param>
        public void AnalogOutput(int[] electrodes, double[,] Vs, int nSampleRate)
        {
            int nrElectrodes = electrodes.GetLength(0);
            if (
                nrElectrodes != Vs.GetLength(1)
                )
                throw new Exception("Number of specified electrodes does not match the given data");

            int ticksPerPoint = nrElectrodes * this.DACinstructionTicks;
            int fillerTicks = (int)((1.0 / nSampleRate) * DACControlSampleRate) - ticksPerPoint;           
            
            double timePerPoint = ticksPerPoint / DACControlSampleRate;

            if (1.0 / nSampleRate < timePerPoint)
                throw new Exception("Requested sampling rate too fast for chosen DAC serial clk rate");

            for (int i = 0; i < Vs.GetLength(0); i++)
            {
                for (int j = 0; j < Vs.GetLength(1); j++)
                    outputOneOffsetElectrodeVoltage((uint)electrodes[j], Vs[i, j]);
                outBlanks(fillerTicks);
            }

        }
        // fix the length of the digital output buffer to fit on 32-bit word boundaries
        // as required by DAQmx API
        private void fixBufferLength()
        {
            if ((doBuffer.Count % 4) != 0)
            {
                int padLength = 4 - doBuffer.Count % 4;
                outBlanks(padLength);
            }

        }
        public void Commit()
        {

            fixBufferLength();

            //doTask uses internal clock
            try
            {
                doTask.Timing.ConfigureSampleClock("", DACControlSampleRate, SampleClockActiveEdge.Rising, SampleQuantityMode.FiniteSamples, doBuffer.Count);
            }
            catch (DaqException ex)
            {
                int a = 5;
                int b = 6;
                a += b;
            }
            //Verify Task
            try
            {
                doTask.Control(TaskAction.Verify);
            }
            catch (DaqException ex)
            {
                int a = 5;
                int b = 6;
                a += b;
            }
            //write data from compiler to task
            DigitalSingleChannelWriter dWriter = new DigitalSingleChannelWriter(doTask.Stream);
            List<int> newBuffer = new List<int>();
            for (int i = 0; i < doBuffer.Count; i++)
                newBuffer.Add(doBuffer[i] << 8);
            try
            {
                dWriter.WriteMultiSamplePort(false, newBuffer.ToArray());
                doTask.Start();
                doTask.WaitUntilDone(20000);
                doTask.Stop();
                doBuffer.Clear();
            }
            catch (DaqException ex) { }

            
        }

    };
}
