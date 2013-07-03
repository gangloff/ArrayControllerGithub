using System;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using com.opalkelly.frontpanel;

namespace ArrayDACControl
{
    class OpalKelly
    {
        //okCUsbFrontPanel m_dev; //Old Front Panel API
        okCFrontPanel m_dev;
        okCPLL22150 PLLobject;

        public String errorMsg, currentDeviceInformation;
        public String[] Serials;

        //PLL parameters
        public int P, Q, Div1N, Div2N;

        const double fref = 48;

        //frequencies
        public double OutFreq, RefFreq, VCOFreq;

        public OpalKelly()
        {
            try
            {
                m_dev = new okCFrontPanel();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            try
            {
                PLLobject = new okCPLL22150();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            currentDeviceInformation = String.Empty;
        }

        public bool GetDeviceSerials()
        {
            int DeviceCount = m_dev.GetDeviceCount();

            if (DeviceCount > 0)
            {
                
                Serials = new String[DeviceCount];

                for (int i = 0; i < DeviceCount; i++)
                {
                    Serials[i] = m_dev.GetDeviceListSerial(i);
                    //currentDeviceInformation += "Device " + i.ToString() + " Serial: " + Serials[i] + "\n";
                }
                return true;
            }
            else return false;
        }

        public bool checkIfOpen()
        {
                return m_dev.IsOpen();
        }

        public void OpenDeviceCom(String serial)
        {
            if (okCFrontPanel.ErrorCode.NoError != m_dev.OpenBySerial(serial))
            {
                errorMsg += "Device could not be opened\n";
            }
        }

        public void GetCurrentDeviceID()
        {
            currentDeviceInformation += "Device ID: " + m_dev.GetDeviceID() + "\n";
            currentDeviceInformation += "Device firmware version: " + m_dev.GetDeviceMajorVersion().ToString() + "." + m_dev.GetDeviceMinorVersion().ToString() + "\n";
            currentDeviceInformation += "Device serial number: " + m_dev.GetSerialNumber().ToString() + "\n";
        }

        public void LoadDefaultPLLConfig()
        {
            // Setup the PLL from defaults.
            m_dev.LoadDefaultPLLConfiguration();
        }

        public void GetPLLConfig()
        {
            if (okCFrontPanel.ErrorCode.NoError != m_dev.GetEepromPLL22150Configuration(PLLobject))
            {
                errorMsg += "Could not get PLL Configuration\n";
            }
        }

        public void SetPLLConfig()
        {
            if (okCFrontPanel.ErrorCode.NoError != m_dev.SetEepromPLL22150Configuration(PLLobject))
            {
                errorMsg += "Could not set EEPROM PLL Configuration\n";
            }

            m_dev.SetPLL22150Configuration(PLLobject);

            if (okCFrontPanel.ErrorCode.NoError != m_dev.SetPLL22150Configuration(PLLobject))
            {
                errorMsg += "Could not set PLL Configuration\n";
            }
        }

        public void SetVCOParameters()
        {
            PLLobject.SetReference(fref, false);
            PLLobject.SetVCOParameters(P, Q);
            PLLobject.SetDiv1(okCPLL22150.DividerSource.DivSrc_VCO, Div1N);
            PLLobject.SetDiv2(okCPLL22150.DividerSource.DivSrc_VCO, Div2N);            
            PLLobject.SetOutputSource(0, okCPLL22150.ClockSource.ClkSrc_Div1By2);
            PLLobject.SetOutputEnable(0, true);
            //PLLobject.SetOutputSource(5, okCPLL22150.ClockSource.ClkSrc_Div1ByN);
            //PLLobject.SetOutputEnable(5, true);
        }

        public void GetFrequencies()
        {
            OutFreq = PLLobject.GetOutputFrequency(0);
            RefFreq = PLLobject.GetReference();
            VCOFreq = PLLobject.GetVCOFrequency();
        }

        public void ConfigFPGA(String path)
        {
            if (okCFrontPanel.ErrorCode.NoError != m_dev.ConfigureFPGA(path))
            {
                errorMsg += "Could not configure FPGA\n";
            }

            // Check for FrontPanel support in the FPGA configuration.
            if (false == m_dev.IsFrontPanelEnabled())
            {
                errorMsg += "FrontPanel support is not available\n";
            }

            //m_dev.EnableAsynchronousTransfers(true);
        }

        /*
        public void SetWire(int address, uint value, uint mask)
        {
            m_dev.SetWireInValue(address, value, mask);
            m_dev.UpdateWireIns();
        }
        */

        public void SetWire(int address, uint value)
        {
            m_dev.SetWireInValue(address, value);
        //    m_dev.UpdateWireIns();
        }

        public void UpdateAllWires()
        {
            m_dev.UpdateWireIns();
        }

        public void SetTrigger(int address, int bit)
        {
            m_dev.ActivateTriggerIn(address, bit);
        }

        public void ReadFromPipeOut(int address, int length, byte[] bytearray)
        {
            m_dev.ReadFromPipeOut(address, length, bytearray);
        }


        /******************************
         * 
        *******************************/
        /*
        private void EncryptDecrypt(String infile, String outfile)
        {
            Stream fileIn, fileOut;

            try
            {
                fileIn = File.OpenRead(infile);
                fileOut = File.Create(outfile);
            }
            catch (FileNotFoundException)
            {
                return;
            }

            byte[] buf = new byte[2048];

            // Reset the RAM address pointer.
            m_dev.ActivateTriggerIn((short)0x41, (short)0);

            int got, len, i;
            got = 0;
            while (true)
            {
                try
                {
                    got = fileIn.Read(buf, 0, 2048);
                }
                catch (IOException)
                {
                    return;
                }

                if (got <= 0)
                    return;

                if (got < 2048)
                    for (i = got; i < 2048; buf[i++] = 0) ;

                // Write a block of data.
                m_dev.ActivateTriggerIn((short)0x41, (short)0);
                m_dev.WriteToPipeIn((short)0x80, 2048, buf);

                // Perform DES on the block.
                m_dev.ActivateTriggerIn((short)0x40, (short)0);

                // Wait for the TriggerOut indicating DONE.
                for (i = 0; i < 10; i++)
                {
                    m_dev.UpdateTriggerOuts();
                    if (m_dev.IsTriggered((short)0x60, (uint)1))
                        break;
                }

                len = 2048;
                m_dev.ReadFromPipeOut((short)0xa0, len, buf);

                try
                {
                    fileOut.Write(buf, 0, 2048);
                }
                catch (Exception)
                {
                }
            }
        }

        public void Encrypt(String infile, String outfile)
        {
            MessageBox.Show("Encrypting " + infile + " ---> " + outfile);

            // Set the encrypt Wire In.
            m_dev.SetWireInValue((int)0x10, (uint)0x00, (uint)0x10);
            m_dev.UpdateWireIns();

            EncryptDecrypt(infile, outfile);
        }

        public void Decrypt(String infile, String outfile)
        {
            MessageBox.Show("Decrypting " + infile + " ---> " + outfile);

            // Set the decrypt Wire In.
            m_dev.SetWireInValue((int)0x10, (uint)0xff, (uint)0x10);
            m_dev.UpdateWireIns();

            EncryptDecrypt(infile, outfile);
        }

        /*
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            MessageBox.Show("------ DES Encrypt/Decrypt Tester in C# ------");

            DESTester des = new DESTester();
            if (false == des.InitializeDevice())
                return;
            des.ResetDES();

            if (args.GetLength(0) != 4)
            {
                MessageBox.Show("Usage: DESTester [d|e] key infile outfile");
                MessageBox.Show("   [d|e]   - d to decrypt the input file.  e to encrypt it.");
                MessageBox.Show("   key     - 64-bit hexadecimal string used for the key.");
                MessageBox.Show("   infile  - an input file to encrypt/decrypt.");
                MessageBox.Show("   outfile - destination output file.");
                return;
            }

            // Get the hex digits entered as the key
            byte[] key = new byte[8];
            String strkey = args[1];
            for (int i = 0; i < 8; i++)
                key[i] = Byte.Parse(strkey.Substring(i * 2, 2), NumberStyles.HexNumber);
            des.SetKey(key);

            // Encrypt or decrypt
            if (args[0][0] == 'd')
                des.Decrypt(args[2], args[3]);
            else
                des.Encrypt(args[2], args[3]);
        }
          */
    }
}
