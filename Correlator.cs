using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace ArrayDACControl
{
    class Correlator
    {
        //API for each board
        //See OpalKelly.cs for FrontPanel function calls
        public OpalKelly ok;

        public double maxcnts = 0;
        public double mincnts = 0;
        public double avgcnts = 0;
        public double decompMerit = 0;
        public double decompMeritErr = 0;
        public int jl = 0;

        UInt16 first2bytesCh1 = 0;
        UInt16 first2bytesCh2 = 0;
        const UInt16 magicNumber = 60926;  // 16'hFEED in hexadecimal when bytes are swapped  ;)
        //This magic number is a 2-byte code word that can be written as 16'hFEED in hexadecimal representation when the bytes are swapped
        //This was a cryptic way for the people who wrote the original correlator code to signal the beginning of the data feed through the pipe

        //Shift register size
        const int lshiftreg = 26;

        //Integration time
        public int IntTime;


        //Shift register clock divisor
        public int ClkDiv;

        //Figure of merit for compensation
        public int bound1;
        public int bound2;

        //Total count
        public double totalCountsCh1;
        public double totalCountsCh2;

        //Length of data read
        public int lengthRead;

        //Flag to signal when the data was actually fed to avoid sleeping the thread when waiting for the feed data
        public bool feedflagCh1;
        public bool feedflagCh2;

        //Results
        public byte[] bytearrayCh1;
        public byte[] bytearrayCh2;

        public double[] phcountarrayCh1;
        public double[] phcountarrayCh1binned;
        public double[] phcountsubarrayACh1;
        public double[] phcountsubarrayBCh1;

        public double[] phcountarrayCh2;
        public double[] phcountarrayCh2binned;
        public double[] phcountsubarrayACh2;
        public double[] phcountsubarrayBCh2;

        public double binningPhase;

        public Correlator()
        {
            ok = new OpalKelly();
        }

        //~ Correlator()
        //{}

        public bool Init(String BitFile)
        {
            //get device serials
            if (ok.checkIfOpen())
            {
                //Set VCO parameters
                ok.SetVCOParameters();
                //Get resulting frequencies
                ok.GetFrequencies();
                //set configuration to EEPROM
                ok.SetPLLConfig();

                ok.ConfigFPGA(BitFile);
                
                /*
                //set integration time
                uint value = (uint)(IntTime * ok.OutFreq / 4.096);
                //ok.SetWire(0, value, 0xff);
                ok.SetWire(0, value);
                //Activate Trigger (magic numbers from labview code)
                ok.SetTrigger(0x40, 1);
                */

                
                ok.SetTrigger(0x40, 0);   // Trigger 1 resets the FPGA
                
                //set integration time
                uint valueIntTime = (uint)(IntTime*ok.OutFreq * 1000 / 65536);   // // 0.0153 = 10^3/2^16
                ok.SetWire(0, valueIntTime);
                ok.UpdateAllWires();
                ok.SetTrigger(0x40, 1);   // Trigger 1 updates the integration time

                
                //set shift register clock division
                uint valueClkDiv = (uint)(ClkDiv);
                ok.SetWire(0, valueClkDiv);
                ok.SetWire(1, valueClkDiv >> 16);
                ok.UpdateAllWires();
                ok.SetTrigger(0x40, 4);   // Trigger 4 updates the clock divisor
                
                
                return true;
            }
            
            else if(ok.GetDeviceSerials())
            {
                    //open communcication with first one
                    ok.OpenDeviceCom(ok.Serials[0]);
                    //get device information
                    ok.GetCurrentDeviceID();
                    //Set VCO parameters
                    ok.SetVCOParameters();
                    //Get resulting frequencies
                    ok.GetFrequencies();
                    //set configuration to EEPROM
                    ok.SetPLLConfig();
                    //upload bit file to FPGA
                    ok.ConfigFPGA(BitFile);

                    /*
                    //set integration time
                    uint value = (uint)(IntTime * ok.OutFreq / 4.096);
                    //ok.SetWire(0, value, 0xff);
                    ok.SetWire(0, value);
                    //Activate Trigger (magic numbers from labview code)
                    ok.SetTrigger(0x40, 1);
                    */

                    
                   ok.SetTrigger(0x40, 0);   // Trigger 1 resets the FPGA
                
                   //set integration time
                   uint valueIntTime = (uint)(IntTime*ok.OutFreq * 1000 / 65536);   // 0.0153 = 10^3/2^16
                   ok.SetWire(0, valueIntTime);
                   ok.UpdateAllWires();
                   ok.SetTrigger(0x40, 1);   // Trigger 1 updates the integration time


                   //set shift register clock division
                   uint valueClkDiv = (uint)(ClkDiv);
                   ok.SetWire(0, valueClkDiv);
                   ok.SetWire(1, valueClkDiv >> 16);
                   ok.UpdateAllWires();
                   ok.SetTrigger(0x40, 4);   // Trigger 4 updates the clock divisor
                               
                   
                    //define results array
                    //bytearray = new byte[lshiftreg * 4 + 2];
                    bytearrayCh1 = new byte[lshiftreg*2];
                    bytearrayCh2 = new byte[lshiftreg * 2];

                    phcountarrayCh1 = new double[lshiftreg];
                    phcountarrayCh1binned = new double[2];
                    phcountsubarrayACh1 = new double[lshiftreg / 2];
                    phcountsubarrayBCh1 = new double[lshiftreg / 2];

                    phcountarrayCh2 = new double[lshiftreg];
                    phcountarrayCh2binned = new double[2];
                    phcountsubarrayACh2 = new double[lshiftreg / 2];
                    phcountsubarrayBCh2 = new double[lshiftreg / 2];


                    return true;
             }
             else return false;
         }
        

        public void GetResults()
        {
            // Reset these variables:

            // Flag for each channel (pipe) to indicate that data has been fed
            feedflagCh1 = false;
            feedflagCh2 = false;

            // Total counts in each of the channels:
            totalCountsCh1 = 0;
            totalCountsCh2 = 0;

            maxcnts = 0;
            mincnts = 0;
            avgcnts = 0;
            jl = 0;
            decompMerit = 0;
            decompMeritErr = 0;
            double sACh1,sBCh1,sACh2,sBCh2;

            //condition for reading
            byte[] bytepointerCh1 = new byte[2];
            byte[] bytepointerCh2 = new byte[2];
            ok.ReadFromPipeOut(0xA0, 2, bytepointerCh1);
            ok.ReadFromPipeOut(0xAF, 2, bytepointerCh2);

            first2bytesCh1 = (UInt16)( (bytepointerCh1[0] << 8) | bytepointerCh1[1] );
            first2bytesCh2 = (UInt16)((bytepointerCh2[0] << 8) | bytepointerCh2[1]);

            while(first2bytesCh1 == magicNumber && first2bytesCh2 != magicNumber)
            {
                ok.ReadFromPipeOut(0xAF, 2, bytepointerCh2);
                first2bytesCh2 = (UInt16)((bytepointerCh2[0] << 8) | bytepointerCh2[1]);
            }

            while (first2bytesCh1 != magicNumber && first2bytesCh2 == magicNumber)
            {
                ok.ReadFromPipeOut(0xA0, 2, bytepointerCh1);
                first2bytesCh1 = (UInt16)((bytepointerCh1[0] << 8) | bytepointerCh1[1]);
            }


            if(first2bytesCh1 == magicNumber)
            //if (bytepointer[0] == 255)
            {
                //read results from Pipe
                //ok.ReadFromPipeOut(0xA0, lshiftreg*4+2, bytearrayCh1);
                ok.ReadFromPipeOut(0xA0, lshiftreg*2, bytearrayCh1);

                phcountarrayCh1binned[0] = 0;
                phcountarrayCh1binned[1] = 0;
                sACh1 = 0;
                sBCh1 = 0;

                feedflagCh1 = true;

                for (int i = 0; i <= (lshiftreg - 1); i++)    // before, used to be i < (lshiftreg - 1)
                {
                    /*
                    phcountarrayCh1[i] =  (double)((bytearrayCh1[i*4]<<8) | bytearrayCh1[i*4+1]);
                    phcountarrayCh1binned[i] =  (double)((bytearrayCh1[i*4+2]<<8) | bytearrayCh1[i*4+3]);
                    */
                    int subi0 = 0;
                    int subi1 = 0;

                    phcountarrayCh1[i] = (double)((bytearrayCh1[i * 2] << 8) | bytearrayCh1[i * 2 + 1]);
                    //if ((i + 1 < lshiftreg / 2 + 1 + binningPhase / 180 * lshiftreg / 2) && (i + 1 > binningPhase / 180 * lshiftreg / 2))
                    if ((i + 1 < lshiftreg / 2 + 1 + binningPhase) && (i + 1 > binningPhase))
                    {
                        phcountarrayCh1binned[0] += phcountarrayCh1[i];
                        phcountsubarrayACh1[subi0] = phcountarrayCh1[i];
                        subi0++;
                    }
                    else
                    {   
                        phcountarrayCh1binned[1] += phcountarrayCh1[i];
                        phcountsubarrayBCh1[subi1] = phcountarrayCh1[i];
                        subi1++;
                    }


                    totalCountsCh1 += phcountarrayCh1[i];
                    /*
                    if ((i >= bound1) && (i <= bound2))
                    {
                        avgcnts += phcountarrayCh1[i];
                        jl++;
                        if(phcountarrayCh1[i] > maxcnts)
                        {maxcnts = phcountarrayCh1[i];}
                        if(phcountarrayCh1[i] < mincnts)
                        {mincnts = phcountarrayCh1[i];}
                    }
                     */
                }
                
                //if (jl > 0)
                //{ avgcnts /= jl; } 

                phcountarrayCh1binned[0] = phcountarrayCh1binned[0]/lshiftreg * 2;
                phcountarrayCh1binned[1] = phcountarrayCh1binned[1] / lshiftreg * 2;

                for(int subi = 0; subi < (lshiftreg/2-1); subi++)
                {
                    sACh1 += Math.Pow((phcountarrayCh1binned[0]-phcountsubarrayACh1[subi]),2);
                    sBCh1 += Math.Pow((phcountarrayCh1binned[1]-phcountsubarrayBCh1[subi]),2);
                }
                
                if (totalCountsCh1 > 0)
                {
                    decompMerit = Math.Round((phcountarrayCh1binned[1]-phcountarrayCh1binned[0]) / (phcountarrayCh1binned[1]+phcountarrayCh1binned[0]) * 100, 2);   // in percent
                    decompMeritErr = Math.Round(Math.Sqrt(sACh1 + sBCh1) / (phcountarrayCh1binned[1] + phcountarrayCh1binned[0]) * 2 / lshiftreg * 100, 2); // in percent
                }

             }

        if (first2bytesCh2 == magicNumber)
        //if (bytepointer[0] == 255)
        {
            ok.ReadFromPipeOut(0xAF, lshiftreg * 2, bytearrayCh2);

            phcountarrayCh2binned[0] = 0;
            phcountarrayCh2binned[1] = 0;
            sACh2 = 0;
            sBCh2 = 0;

            feedflagCh2 = true;

            for (int i = 0; i <= (lshiftreg - 1); i++)    // before, used to be i < (lshiftreg - 1)
            {
                /*
                phcountarrayCh2[i] =  (double)((bytearrayCh2[i*4]<<8) | bytearrayCh2[i*4+1]);
                phcountarrayCh2binned[i] =  (double)((bytearrayCh2[i*4+2]<<8) | bytearrayCh2[i*4+3]);
                */
                int subi0 = 0;
                int subi1 = 0;

                phcountarrayCh2[i] = (double)((bytearrayCh2[i * 2] << 8) | bytearrayCh2[i * 2 + 1]);
                //if ((i + 1 < lshiftreg / 2 + 1 + binningPhase / 180 * lshiftreg / 2) && (i + 1 > binningPhase / 180 * lshiftreg / 2))
                if ((i + 1 < lshiftreg / 2 + 1 + binningPhase) && (i + 1 > binningPhase))
                {
                    phcountarrayCh2binned[0] += phcountarrayCh2[i];
                    phcountsubarrayACh2[subi0] = phcountarrayCh2[i];
                    subi0++;
                }
                else
                {
                    phcountarrayCh2binned[1] += phcountarrayCh2[i];
                    phcountsubarrayBCh2[subi1] = phcountarrayCh2[i];
                    subi1++;
                }


                totalCountsCh2 += phcountarrayCh2[i];
                /*
                if ((i >= bound1) && (i <= bound2))
                {
                    avgcnts += phcountarrayCh2[i];
                    jl++;
                    if(phcountarrayCh2[i] > maxcnts)
                    {maxcnts = phcountarrayCh2[i];}
                    if(phcountarrayCh2[i] < mincnts)
                    {mincnts = phcountarrayCh2[i];}
                }
                 */
            }

            //if (jl > 0)
            //{ avgcnts /= jl; } 

            phcountarrayCh2binned[0] = phcountarrayCh2binned[0] / lshiftreg * 2;
            phcountarrayCh2binned[1] = phcountarrayCh2binned[1] / lshiftreg * 2;

            for (int subi = 0; subi < (lshiftreg / 2 - 1); subi++)
            {
                sACh2 += Math.Pow((phcountarrayCh2binned[0] - phcountsubarrayACh2[subi]), 2);
                sBCh2 += Math.Pow((phcountarrayCh2binned[1] - phcountsubarrayBCh2[subi]), 2);
            }

            /*
            if (totalCountsCh2 > 0)
            {
                decompMerit = Math.Round((phcountarrayCh2binned[1] - phcountarrayCh2binned[0]) / (phcountarrayCh2binned[1] + phcountarrayCh2binned[0]) * 100, 2);   // in percent
                decompMeritErr = Math.Round(Math.Sqrt(sACh2 + sBCh2) / (phcountarrayCh2binned[1] + phcountarrayCh2binned[0]) * 2 / lshiftreg * 100, 2); // in percent
            }
            */








        }
            
        }

    }
}
