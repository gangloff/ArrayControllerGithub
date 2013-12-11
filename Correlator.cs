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
        //const int lshiftreg = 26;
        public int lshiftreg;

        //Integration time
        public int IntTime;

        //Sync source boolean
        public bool syncSrcChoose;

        //Shift register clock divisor
        public uint ClkDiv;

        // Pulsed output signal characteristics
        public uint PulseClkDiv;
        public uint[] onTimeOut = new uint[4];
        public uint[] onTimeIn = new uint[2];
        public uint[] delayOut = new uint[4];
        public uint[] delayIn = new uint[2];

        public uint slow_PulseClkDiv;
        public uint[] slow_onTimeOut = new uint[4];
        public uint[] slow_onTimeIn = new uint[2];
        public uint[] slow_delayOut = new uint[4];
        public uint[] slow_delayIn = new uint[2];

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
                initHelper(BitFile);
                
                return true;
            }
            
            else if(ok.GetDeviceSerials())
            {
                    //open communcication with first one
                    ok.OpenDeviceCom(ok.Serials[0]);
                    //get device information
                    ok.GetCurrentDeviceID();
                    //Set VCO parameters
                    
                    initHelper(BitFile);

                    return true;
             }
             else return false;
         }

        private void initHelper(String BitFile)
        {
            //Set VCO parameters
            ok.SetVCOParameters();
            //Get resulting frequencies
            ok.GetFrequencies();
            //set configuration to EEPROM
            ok.SetPLLConfig();
            // Configure the FPGA with the bitfile
            ok.ConfigFPGA(BitFile);
            //Reset the FPGA (trigger 0)
            ok.SetTrigger(0x40, 0);

            // Set integration time via OK wires
            updateCorrIntTimeExecute();

            // Set the sync source ext vs int
            updateSyncSourceExecute();

            //set shift register clock division
            //uint valueClkDiv = (uint)(ClkDiv);
            ok.SetWire(0, ClkDiv);
            ok.SetWire(1, ClkDiv >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x40, 2);   // Trigger 2 updates the clock divisor
            
            //set pulsed output frequency and duty cycle
            updateAllSignalsExecute();

            //define results array
            //bytearray = new byte[lshiftreg * 4 + 2];
            bytearrayCh1 = new byte[lshiftreg * 2];
            bytearrayCh2 = new byte[lshiftreg * 2];

            phcountarrayCh1 = new double[lshiftreg];
            phcountarrayCh1binned = new double[2];
            phcountsubarrayACh1 = new double[lshiftreg / 2];
            phcountsubarrayBCh1 = new double[lshiftreg / 2];

            phcountarrayCh2 = new double[lshiftreg];
            phcountarrayCh2binned = new double[2];
            phcountsubarrayACh2 = new double[lshiftreg / 2];
            phcountsubarrayBCh2 = new double[lshiftreg / 2];
        }

        /////////////////////////////////////////////////////////
        public void updateCorrIntTimeLive()
        {
            if (ok.checkIfOpen())
            { updateCorrIntTimeExecute(); }
        }

        private void updateCorrIntTimeExecute()
        {
            //set integration time
            uint valueIntTime = (uint)(IntTime * ok.OutFreq * 1000 / 65536);   // // 0.0153 = 10^3/2^16
            ok.SetWire(0, valueIntTime);
            ok.UpdateAllWires();
            ok.SetTrigger(0x40, 1);   // Trigger 1 signals update of integration time
        }
        //////////////////////////////////////////////////////////

        /////////////////////////////////////////////////////////
        public void updateSyncSourceLive()
        {
            if (ok.checkIfOpen())
            { updateSyncSourceExecute(); }
        }

        private void updateSyncSourceExecute()
        {
            if (syncSrcChoose) { ok.SetWire(0, 1); }  //binary 11; Sync source = EXT
            else { ok.SetWire(0, 0); }  //binary 01; Sync source = INT
            ok.UpdateAllWires();
            ok.SetTrigger(0x40, 4);   // Trigger 4 signals update of sync source
        }
        //////////////////////////////////////////////////////////

        /*
        public void updateCorrPulseClkDivLive()
        {
            if (ok.checkIfOpen())
            { updateCorrPulseClkDivExecute(); }
        }

        public void updateCorrPulseClkDivExecute()
        {
            uint valuePulsedClkDiv = (uint)(PulseClkDiv);
            uint valuePulseWidthDiv = (uint)(PulseWidthDiv);
            ok.SetWire(0, valuePulsedClkDiv);
            ok.SetWire(1, valuePulseWidthDiv);
            //selects whether to collect according to duty cycle of probe
            //if collectDutyCycle is true, collect according to duty cycle
            if (collectDutyCycle)
            {
                if (syncSrcChoose) ok.SetWire(2, 3);  //binary 11
                else ok.SetWire(2, 1);  //binary 01
            }
            else
            {
                if (syncSrcChoose) ok.SetWire(2, 2);  //binary 10
                else ok.SetWire(2, 0);  //binary 00
            }
            ok.UpdateAllWires();
            ok.SetTrigger(0x40, 6);   // Trigger 6 updates pulsed output signal characteristics
        }
         */
        //////////////////////////////////////////////////

        public void updateAllSignalsLive()
        {
            if (ok.checkIfOpen())
            { updateAllSignalsExecute(); }
        }

        public void updateAllSignalsExecute()
        {
            ok.SetWire(0, PulseClkDiv);
            ok.SetWire(1, PulseClkDiv >> 16);
            ok.SetWire(2, onTimeOut[0]);
            ok.SetWire(3, onTimeOut[0] >> 16);
            ok.SetWire(4, delayOut[0]);
            ok.SetWire(5, delayOut[0] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 0);   // Trigger 0 updates output channel 1

            ok.SetWire(0, PulseClkDiv);
            ok.SetWire(1, PulseClkDiv >> 16);
            ok.SetWire(2, onTimeOut[1]);
            ok.SetWire(3, onTimeOut[1] >> 16);
            ok.SetWire(4, delayOut[1]);
            ok.SetWire(5, delayOut[1] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 1);   // Trigger 1 updates output channel 2

            ok.SetWire(0, PulseClkDiv);
            ok.SetWire(1, PulseClkDiv >> 16);
            ok.SetWire(2, onTimeOut[2]);
            ok.SetWire(3, onTimeOut[2] >> 16);
            ok.SetWire(4, delayOut[2]);
            ok.SetWire(5, delayOut[2] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 2);   // Trigger 2 updates output channel 3

            ok.SetWire(0, PulseClkDiv);
            ok.SetWire(1, PulseClkDiv >> 16);
            ok.SetWire(2, onTimeOut[3]);
            ok.SetWire(3, onTimeOut[3] >> 16);
            ok.SetWire(4, delayOut[3]);
            ok.SetWire(5, delayOut[3] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 3);   // Trigger 3 updates output channel 4

            ok.SetWire(0, PulseClkDiv);
            ok.SetWire(1, PulseClkDiv >> 16);
            ok.SetWire(2, onTimeIn[0]);
            ok.SetWire(3, onTimeIn[0] >> 16);
            ok.SetWire(4, delayIn[0]);
            ok.SetWire(5, delayIn[0] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 4);   // Trigger 7 updates input channel 1

            ok.SetWire(0, PulseClkDiv);
            ok.SetWire(1, PulseClkDiv >> 16);
            ok.SetWire(2, onTimeIn[1]);
            ok.SetWire(3, onTimeIn[1] >> 16);
            ok.SetWire(4, delayIn[1]);
            ok.SetWire(5, delayIn[1] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 5);   // Trigger 8 updates input channel 2


            //////////// SLOW SEQUENCER WIRES: //////////////////////

            ok.SetWire(0, slow_PulseClkDiv);
            ok.SetWire(1, slow_PulseClkDiv >> 16);
            ok.SetWire(2, slow_onTimeOut[0]);
            ok.SetWire(3, slow_onTimeOut[0] >> 16);
            ok.SetWire(4, slow_delayOut[0]);
            ok.SetWire(5, slow_delayOut[0] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 6);   // Trigger 6 updates slow output channel 1

            ok.SetWire(0, slow_PulseClkDiv);
            ok.SetWire(1, slow_PulseClkDiv >> 16);
            ok.SetWire(2, slow_onTimeOut[1]);
            ok.SetWire(3, slow_onTimeOut[1] >> 16);
            ok.SetWire(4, slow_delayOut[1]);
            ok.SetWire(5, slow_delayOut[1] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 7);   // Trigger 7 updates slow output channel 2

            ok.SetWire(0, slow_PulseClkDiv);
            ok.SetWire(1, slow_PulseClkDiv >> 16);
            ok.SetWire(2, slow_onTimeOut[2]);
            ok.SetWire(3, slow_onTimeOut[2] >> 16);
            ok.SetWire(4, slow_delayOut[2]);
            ok.SetWire(5, slow_delayOut[2] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 8);   // Trigger 8 updates slow output channel 3

            ok.SetWire(0, slow_PulseClkDiv);
            ok.SetWire(1, slow_PulseClkDiv >> 16);
            ok.SetWire(2, slow_onTimeOut[3]);
            ok.SetWire(3, slow_onTimeOut[3] >> 16);
            ok.SetWire(4, slow_delayOut[3]);
            ok.SetWire(5, slow_delayOut[3] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 9);   // Trigger 9 updates slow output channel 4

            ok.SetWire(0, slow_PulseClkDiv);
            ok.SetWire(1, slow_PulseClkDiv >> 16);
            ok.SetWire(2, slow_onTimeIn[0]);
            ok.SetWire(3, slow_onTimeIn[0] >> 16);
            ok.SetWire(4, slow_delayIn[0]);
            ok.SetWire(5, slow_delayIn[0] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 10);   // Trigger 10 updates input channel 1

            ok.SetWire(0, slow_PulseClkDiv);
            ok.SetWire(1, slow_PulseClkDiv >> 16);
            ok.SetWire(2, slow_onTimeIn[1]);
            ok.SetWire(3, slow_onTimeIn[1] >> 16);
            ok.SetWire(4, slow_delayIn[1]);
            ok.SetWire(5, slow_delayIn[1] >> 16);
            ok.UpdateAllWires();
            ok.SetTrigger(0x41, 11);   // Trigger 11 updates input channel 2
        }
        //////////////////////////////////////////////////

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
