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
        public string threadName;
        //name of Data Stream
        public string dataStreamName;
        public string slider1Name;
        public string slider2Name;
        public string folderPathExtra;
        //helper variables for scans
        public int index;
        public int index2;
        public int direction;
        public int numScanVar;
        public double[] max;
        public double[] min;
        public int numPoints;
        public int numAverage;
        public int delay;
        public bool ShouldBeRunningFlag;
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
        public double SingleDouble3;
        public double[] KeepDoubles;
        public double[] DoubleArray;
        public double[,] DoubleData;
        public double[,] DoubleScanVariable;
        public double[][,] DoubleDataArray;
        public int SingleInt;
        public int[,] IntData;
        public int[,] IntScanVariable;
        public double Background;
        //Slider associated with a scan
        public AdjustableSlider theSlider;
        public AdjustableSlider theSlider2;
        public AdjustableSlider theSlider3;
        //Button associated with a scan
        public Button theButton;

        //constructor
        public ThreadHelperClass(string theName)
        {
            index = 0;
            ShouldBeRunningFlag = false;
            direction = 0;
            flag = false;
            max = new double[1];
            min = new double[1];
            threadName = theName;
            DoubleArray = new double[50];
            folderPathExtra = "";
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
