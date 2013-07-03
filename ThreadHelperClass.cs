using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Threading;


namespace ArrayDACControl
{
    public class ThreadHelperClass
    {
        //Thread
        public Thread theThread;
        //helper variables for scans
        public int index;
        public int direction;
        public double[] max;
        public double[] min;
        public int numPoints;
        public int numAverage;
        public int delay;
        public bool IsRunningFlag;
        public bool flag;
        public double ScanVariableBefore;
        public string message;
        public string message2;
        public string message3;
        public string message4;
        public string message5;
        //data variables for the thread
        public double SingleDouble;
        public double SingleDouble2;
        public double[,] DoubleData;
        public double[,] DoubleScanVariable;
        public int SingleInt;
        public int[,] IntData;
        public int[,] IntScanVariable;
        public double Background;

        //constructor
        public ThreadHelperClass()
        {
            index = 0;
            IsRunningFlag = false;
            direction = 0;
            flag = false;
            max = new double[1];
            min = new double[1];
        }

        //initialization methods for data variables
        //matrix size datadim x datalength
        public void initDoubleData(int datalength, int datadim, int scanVarNum)
        {
            DoubleData = new double[datadim,datalength];
            DoubleScanVariable = new double[scanVarNum,datalength];
        }

        public void initIntData(int datalength, int datadim, int scanVarNum)
        {
            IntData = new int[datadim,datalength];
            IntScanVariable = new int[scanVarNum,datalength];
        }
    };
}
