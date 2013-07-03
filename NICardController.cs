using System;
using System.Collections.Generic;

using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NationalInstruments.DAQmx;

namespace ArrayDACControl
{
    public class NICardController
    {
        private Task myTask;
        private AnalogSingleChannelWriter AOwriter;
        private DigitalSingleChannelWriter DOwriter;
        private AnalogSingleChannelReader AIreader;

        /// <summary>
        /// Creates analog output channel physicalChannelName
        /// </summary>
        /// <param name="physicalChannelName">physical name of the Channel from NI card</param>
        /// <param name="minimumValue">minimum output value of channel</param>
        /// <param name="maximumValue">maximum output value of channel</param>
        public NICardController()
        {
            myTask = new Task();
        }

        // Initialize Analog Channel "ChannelName"
        public void InitAnalogOutput(string physicalChannelName, double minimumValue, double maximumValue)
        {
            try
            {
                myTask.AOChannels.CreateVoltageChannel(physicalChannelName, "AO" + physicalChannelName[physicalChannelName.Length-1],
                    minimumValue, maximumValue, AOVoltageUnits.Volts);
                AOwriter = new AnalogSingleChannelWriter(myTask.Stream);
            }
            catch (DaqException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Initialize Analog Channel Input "ChannelName"
        public void InitAnalogInput(string physicalChannelName, double minimumValue, double maximumValue)
        {
            try
            {
                myTask.AIChannels.CreateVoltageChannel(physicalChannelName, "AI" + physicalChannelName[physicalChannelName.Length - 1],
                    AITerminalConfiguration.Differential, minimumValue, maximumValue, AIVoltageUnits.Volts);
                //myTask.Timing.ConfigureSampleClock
                AIreader = new AnalogSingleChannelReader(myTask.Stream);
            }
            catch (DaqException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void InitDigitalOutput(string physicalLineName)
        {
            try
            {
                myTask.DOChannels.CreateChannel(physicalLineName, "D0" + physicalLineName[physicalLineName.Length - 1], ChannelLineGrouping.OneChannelForEachLine);
                DOwriter = new DigitalSingleChannelWriter(myTask.Stream);
            }
            catch (DaqException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public double ReadAnalogValue()
        {
            double result = 0;

            try
            {
                IAsyncResult iasyncresult = AIreader.BeginReadSingleSample(null, null);
                result = AIreader.EndReadSingleSample(iasyncresult);
            }
            catch (DaqException ex)
            {
                MessageBox.Show(ex.Message);
            }

            return result;
        }
                

        public void OutputAnalogValue(double Vout)
        {
            try
            {
                AOwriter.WriteSingleSample(true, Vout);
            }
            catch (DaqException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void OutputDigitalValue(bool data)
        {
            try
            {
                DOwriter.WriteSingleSampleSingleLine(true, data);
            }
            catch (DaqException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    };
}
