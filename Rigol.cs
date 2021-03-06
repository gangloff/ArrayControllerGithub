using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NationalInstruments.VisaNS;

namespace ArrayDACControl
{
    //Class: Keithley
    //------------
    //represents a Keithley 2100 multimeter
    //------------
    public class Rigol
    {
        //--------------------------------------------------------------------------------------
        //Instance variables
        //--------------------------------------------------------------------------------------

        private string instrumentAddress;


        //--------------------------------------------------------------------------------------
        //Constructors
        //------------
        //proper call: Rigol rigol = new Rigol("USB:0:HX03958:WHATEVER");
        //------------
        //--------------------------------------------------------------------------------------

        public Rigol(string address)
        {
            instrumentAddress = address;
        }


        ///--------------------------------------------------------------------------------------
        //Methods
        //--------------------------------------------------------------------------------------

        //Method: GenerateUserWaveform
        //------------------------
        //proper call: rigol.GenerateUserWaveform(true, 1, 400, 5, 0, 180, "waveform.txt");
        //------------------------
        //generates an arbitrary waveform on the RIGOL machine
        //turns on BURST mode, with type infinity and external trigger source
        //------------------------
        //Argument notes:
        //10MHz clock source- true for external, false for internal
        //channel must be 1 or 2
        //frequency in Hz
        //amplitude in Vpp
        //offset in Vdc
        //phase in degrees
        //text file with ASCII datapoints, one per line
        //-------------------------
        public void GenerateUserWaveform(bool clockSrcExt, int channel, double frequency, double amplitude,
            double offset, double phase, string filename)
        {
            GenerateUserWaveform(clockSrcExt, channel, frequency, amplitude, offset, phase, ReadWaveformFromFile(filename));
        }


        //Method: GenerateUserWaveform
        //------------------------
        //proper call: rigol.GenerateUserWaveform(true, 1, 400, 5, 0, 180, waveform);
        //------------------------
        //overloaded method: takes a user-defined waveform array (max length is 16384) instead of a text file
        //-------------------------
        public void GenerateUserWaveform(bool clockSrcExt, int channel, double frequency, double amplitude,
            double offset, double phase, double[] waveform)
        {
            //establish VISA connection
            MessageBasedSession mbSession = Configure();

            //set clock source
            if (clockSrcExt)
            {
                string commandClock = ":system:roscillator:source external";
                mbSession.Write(commandClock);
            }
            else
            {
                string commandClock = ":system:roscillator:source internal";
                mbSession.Write(commandClock);
            }

            //convert input parameters to strings
            string stringChannel = Convert.ToString(channel);
            string stringFrequency = Convert.ToString(frequency);
            string stringAmplitude = Convert.ToString(amplitude);
            string stringOffset = Convert.ToString(offset);
            string stringPhase = Convert.ToString(phase);

            //set basic parameters
            string commandParameters = "source" + stringChannel + ":apply:user " + stringFrequency + "," +
                stringAmplitude + "," + stringOffset + "," + stringPhase;
            mbSession.Write(commandParameters);

            //turn on linear interpolation
            string commandInterp = "data:points:interpolate linear";
            mbSession.Write(commandInterp);

            //re-scale input waveform array so it fits between -1 and 1
            double max = waveform[0];
            for (int i = 0; i < waveform.Length; i++)
            {
                if (Math.Abs(waveform[i]) > max)
                {
                    max = Math.Abs(waveform[i]);
                }
            }
            for (int i = 0; i < waveform.Length; i++)
            {
                waveform[i] /= max;
            }

            //convert input waveform array into a string
            string stringWaveform = "";
            for (int i = 0; i < waveform.Length - 1; i++)
            {
                //all but the last entry should be followed by a comma
                stringWaveform += (Convert.ToString(waveform[i]) + ",");
            }
            //the last entry
            stringWaveform += Convert.ToString(waveform[waveform.Length - 1]);

            //assign data points
            string commandData = "data volatile," + stringWaveform;
            mbSession.Write(commandData);

            //turn burst ON
            string commandBurstOn = "source" + stringChannel + ":burst ON";
            mbSession.Write(commandBurstOn);

            //set burst mode to infinite
            string commandBurstMode = "source" + stringChannel + ":burst:mode infinity";
            mbSession.Write(commandBurstMode);

            //set burst trigger source to external
            string commandBurstTrigExt = "source" + stringChannel + ":burst:trigger:source external";
            mbSession.Write(commandBurstTrigExt);

            //turn output ON
            string commandOutput = "output" + stringChannel + " ON";
            mbSession.Write(commandOutput);

            //set burst trigger source to manual
            string commandBurstTrigMan = "source" + stringChannel + ":burst:trigger:source manual";
            mbSession.Write(commandBurstTrigMan);

            //set burst trigger source to external
            mbSession.Write(commandBurstTrigExt);
        }

        //Method: GenerateSync
        //------------------------
        //proper call: rigol.GenerateSync(1, 400, 5, 0, 180, waveform);
        //------------------------
        //generates a sync signal- square wave with LO at 0V and HI at 5V- on the RIGOL machine
        //-------------------------
        //Argument notes:
        //channel must be 1 or 2
        //frequency in Hz
        //-------------------------
        public void GenerateSync(int channel, double frequency)
        {
            //establish VISA connection
            MessageBasedSession mbSession = Configure();

            //set clock source to internal
            string commandClock = ":system:roscillator:source internal";
            mbSession.Write(commandClock);
          
            //convert input parameters to strings
            string stringChannel = Convert.ToString(channel);
            string stringFrequency = Convert.ToString(frequency);

            //set basic parameters
            string commandParameters = "source" + stringChannel + ":apply:square " + stringFrequency + ",5,2.5,0";
            mbSession.Write(commandParameters);

            //turn output ON
            string commandOutput = "output" + stringChannel + " ON";
            mbSession.Write(commandOutput);
        }

        //Method: GetAddress
        //------------------
        //proper call: string address = rigol.GetAddress();
        //------------------
        //returns the USB instrument address
        //------------------
        public string GetAddress()
        {
            return instrumentAddress;
        }


        //Method: ReadWaveformFromFile
        //------------------------
        //proper call: double[] waveform = Rigol.ReadWaveformFromFile("waveform.txt");
        //------------------------
        //takes a matlab-generated file containing (only) waveform point values, line-by-line, in ASCII
        //reads the file line-by-line and transfers the data to an array
        //------------------------
        public static double[] ReadWaveformFromFile(string filename)
        {
            //transfers the data first to a temporary ArrayList (preferred to an array, as the former is dynamic)
            //the ArrayList data wil be transferred to an array at the end of the function
            ArrayList temp = new ArrayList();

            //uses the StreamReader class to perform I/O
            using (StreamReader reader = new StreamReader(filename))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    temp.Add(Convert.ToDouble(line));
                }
            }
            
            //return the ArrayList as an array of doubles
            return (double[])temp.ToArray(typeof(double));
        }


        //Method: Configure
        //-----------------
        //private method
        //-----------------
        //initializes a VISA connection to the Rigol machine
        //-----------------
        private MessageBasedSession Configure()
        {
            return (MessageBasedSession)ResourceManager.GetLocalManager().Open(GetAddress());
        }

    }
}