using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NationalInstruments.VisaNS;
using System.Windows.Forms;

namespace ArrayDACControl
{
    //Class: Rigol
    //------------
    //represents a Rigol DG4000 function generator
    //straight outta China
    //------------
    public class Keithley
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

        public Keithley(string address)
        {
            instrumentAddress = address;
        }


        ///--------------------------------------------------------------------------------------
        //Methods
        //--------------------------------------------------------------------------------------


        //Method: Setup Measurement
        //------------------------
        //proper call: keithley.SetupMeasurement
        //------------------------
        //
        //-------------------------
        public void SetupMeasurement(string measurementType)
        {
            //establish VISA connection
            MessageBasedSession mbSession = Configure();
            //strings
            string command, output;
            
            command = "MEASure:RESistance 10K 4";
            mbSession.Write(command);
            output = mbSession.ReadString(); MessageBox.Show(output);
            

            command = "SYSTem:ERRor";
            mbSession.Write(command);
            output = mbSession.ReadString(); MessageBox.Show(output);

            
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